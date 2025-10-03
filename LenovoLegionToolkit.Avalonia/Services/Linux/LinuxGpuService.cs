using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.Services.Linux
{
    public class LinuxGpuService : IGpuService
    {
        private const string PCI_DEVICES_PATH = "/sys/bus/pci/devices";
        private const string DRM_PATH = "/sys/class/drm";
        private const string LEGION_DGPU_PATH = "/sys/kernel/legion_laptop/dgpu_disable";
        private const string VGA_SWITCHEROO_PATH = "/sys/kernel/debug/vgaswitcheroo/switch";
        private const string NVIDIA_PROC_PATH = "/proc/driver/nvidia/gpus";

        private readonly Subject<GpuInfo> _gpuStateChanged = new();

        public IObservable<GpuInfo> GpuStateChanged => _gpuStateChanged;

        public async Task<List<GpuInfo>> GetGpuInfoAsync()
        {
            var gpus = new List<GpuInfo>();

            try
            {
                // Scan PCI devices for VGA controllers
                if (Directory.Exists(PCI_DEVICES_PATH))
                {
                    foreach (var devicePath in Directory.GetDirectories(PCI_DEVICES_PATH))
                    {
                        var classFile = Path.Combine(devicePath, "class");
                        if (!File.Exists(classFile))
                            continue;

                        var deviceClass = await File.ReadAllTextAsync(classFile);
                        // 0x030000 = VGA controller, 0x030200 = 3D controller
                        if (deviceClass.StartsWith("0x0300") || deviceClass.StartsWith("0x0302"))
                        {
                            var gpu = await ParseGpuInfoAsync(devicePath);
                            if (gpu != null)
                                gpus.Add(gpu);
                        }
                    }
                }

                // Enhance with runtime information
                await EnhanceWithNvidiaInfo(gpus);
                await EnhanceWithAmdInfo(gpus);
                await EnhanceWithIntelInfo(gpus);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get GPU info", ex);
            }

            return gpus;
        }

        private async Task<GpuInfo?> ParseGpuInfoAsync(string devicePath)
        {
            try
            {
                var gpu = new GpuInfo();

                // Read PCI ID
                var vendorFile = Path.Combine(devicePath, "vendor");
                var deviceFile = Path.Combine(devicePath, "device");

                if (File.Exists(vendorFile) && File.Exists(deviceFile))
                {
                    var vendorId = (await File.ReadAllTextAsync(vendorFile)).Trim();
                    var deviceId = (await File.ReadAllTextAsync(deviceFile)).Trim();
                    gpu.PciId = $"{vendorId}:{deviceId}";

                    // Determine vendor
                    gpu.Vendor = vendorId switch
                    {
                        "0x10de" => GpuVendor.NVIDIA,
                        "0x1002" => GpuVendor.AMD,
                        "0x8086" => GpuVendor.Intel,
                        _ => GpuVendor.Unknown
                    };
                }

                // Read bus ID
                gpu.BusId = Path.GetFileName(devicePath);

                // Check if active
                var enableFile = Path.Combine(devicePath, "enable");
                if (File.Exists(enableFile))
                {
                    var enabled = await File.ReadAllTextAsync(enableFile);
                    gpu.IsActive = enabled.Trim() == "1";
                }

                // Determine type based on vendor and bus location
                gpu.Type = DetermineGpuType(gpu.Vendor, gpu.BusId);

                // Get driver
                var driverLink = Path.Combine(devicePath, "driver");
                if (Directory.Exists(driverLink))
                {
                    var driverPath = Path.GetFullPath(driverLink);
                    gpu.Driver = Path.GetFileName(driverPath);
                }

                return gpu;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse GPU at {devicePath}", ex);
                return null;
            }
        }

        private GpuType DetermineGpuType(GpuVendor vendor, string busId)
        {
            // Intel GPUs are typically integrated
            if (vendor == GpuVendor.Intel)
                return GpuType.Integrated;

            // Check bus ID - integrated GPUs usually on lower bus numbers
            if (busId.StartsWith("0000:00:"))
                return GpuType.Integrated;

            return GpuType.Discrete;
        }

        private async Task EnhanceWithNvidiaInfo(List<GpuInfo> gpus)
        {
            var nvidiaGpus = gpus.Where(g => g.Vendor == GpuVendor.NVIDIA).ToList();
            if (!nvidiaGpus.Any())
                return;

            try
            {
                // Try nvidia-smi for detailed info
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "nvidia-smi",
                        Arguments = "--query-gpu=name,temperature.gpu,power.draw,memory.used,memory.total --format=csv,noheader,nounits",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < Math.Min(lines.Length, nvidiaGpus.Count); i++)
                    {
                        var parts = lines[i].Split(',').Select(p => p.Trim()).ToArray();
                        if (parts.Length >= 5)
                        {
                            nvidiaGpus[i].Name = parts[0];
                            if (double.TryParse(parts[1], out var temp))
                                nvidiaGpus[i].Temperature = temp;
                            if (int.TryParse(parts[2], out var power))
                                nvidiaGpus[i].PowerDraw = power;
                            if (int.TryParse(parts[3], out var memUsed))
                                nvidiaGpus[i].MemoryUsed = memUsed;
                            if (int.TryParse(parts[4], out var memTotal))
                                nvidiaGpus[i].MemoryTotal = memTotal;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"nvidia-smi not available or failed: {ex.Message}");
            }
        }

        private async Task EnhanceWithAmdInfo(List<GpuInfo> gpus)
        {
            var amdGpus = gpus.Where(g => g.Vendor == GpuVendor.AMD).ToList();
            if (!amdGpus.Any())
                return;

            // Read from sysfs for AMD GPUs
            foreach (var gpu in amdGpus)
            {
                try
                {
                    var cardPath = $"/sys/class/drm/card{gpu.BusId.Substring(5, 2)}/device";

                    // Temperature
                    var tempFile = Path.Combine(cardPath, "hwmon/hwmon0/temp1_input");
                    if (File.Exists(tempFile))
                    {
                        var temp = await File.ReadAllTextAsync(tempFile);
                        if (int.TryParse(temp, out var tempValue))
                            gpu.Temperature = tempValue / 1000.0;
                    }

                    // Power
                    var powerFile = Path.Combine(cardPath, "hwmon/hwmon0/power1_average");
                    if (File.Exists(powerFile))
                    {
                        var power = await File.ReadAllTextAsync(powerFile);
                        if (int.TryParse(power, out var powerValue))
                            gpu.PowerDraw = powerValue / 1000000; // Convert from microwatts
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Failed to read AMD GPU info for {gpu.BusId}: {ex.Message}");
                }
            }
        }

        private async Task EnhanceWithIntelInfo(List<GpuInfo> gpus)
        {
            var intelGpus = gpus.Where(g => g.Vendor == GpuVendor.Intel).ToList();
            if (!intelGpus.Any())
                return;

            foreach (var gpu in intelGpus)
            {
                gpu.Name = "Intel Integrated Graphics";
                // Intel GPU info typically available via i915 driver sysfs
                // Implementation would go here
            }
        }

        public async Task<HybridModeState> GetHybridModeAsync()
        {
            try
            {
                // Check Legion-specific control first
                if (File.Exists(LEGION_DGPU_PATH))
                {
                    var dgpuDisabled = await File.ReadAllTextAsync(LEGION_DGPU_PATH);
                    return dgpuDisabled.Trim() == "1"
                        ? HybridModeState.OnIGPUOnly
                        : HybridModeState.On;
                }

                // Check vga_switcheroo
                if (File.Exists(VGA_SWITCHEROO_PATH))
                {
                    var switcherooContent = await File.ReadAllTextAsync(VGA_SWITCHEROO_PATH);
                    if (switcherooContent.Contains("DIS:+"))
                        return HybridModeState.On;
                    if (switcherooContent.Contains("DIS: Off"))
                        return HybridModeState.OnIGPUOnly;
                }

                // Check if discrete GPU is active
                var gpus = await GetGpuInfoAsync();
                var discreteGpu = gpus.FirstOrDefault(g => g.Type == GpuType.Discrete);

                if (discreteGpu != null)
                {
                    return discreteGpu.IsActive ? HybridModeState.On : HybridModeState.OnIGPUOnly;
                }

                return HybridModeState.On;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get hybrid mode", ex);
                return HybridModeState.On;
            }
        }

        public async Task<bool> SetHybridModeAsync(HybridModeState mode)
        {
            try
            {
                // Try Legion-specific control first
                if (File.Exists(LEGION_DGPU_PATH))
                {
                    var value = mode == HybridModeState.OnIGPUOnly ? "1" : "0";
                    await File.WriteAllTextAsync(LEGION_DGPU_PATH, value);
                    return true;
                }

                // Try vga_switcheroo
                if (File.Exists(VGA_SWITCHEROO_PATH))
                {
                    var command = mode switch
                    {
                        HybridModeState.OnIGPUOnly => "OFF",
                        HybridModeState.Off => "DIS",
                        _ => "ON"
                    };
                    await File.WriteAllTextAsync(VGA_SWITCHEROO_PATH, command);
                    return true;
                }

                // Try using prime-select for NVIDIA
                if (await IsNvidiaAvailable())
                {
                    var primeMode = mode switch
                    {
                        HybridModeState.OnIGPUOnly => "intel",
                        HybridModeState.Off => "nvidia",
                        _ => "on-demand"
                    };

                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "prime-select",
                        Arguments = primeMode,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });

                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set hybrid mode to {mode}", ex);
                return false;
            }
        }

        public async Task<bool> IsHybridModeSupportedAsync()
        {
            // Check for multiple GPUs
            var gpus = await GetGpuInfoAsync();
            if (gpus.Count < 2)
                return false;

            // Check for switchable graphics support
            return File.Exists(LEGION_DGPU_PATH) ||
                   File.Exists(VGA_SWITCHEROO_PATH) ||
                   await IsNvidiaAvailable();
        }

        public async Task<bool> IsDiscreteGpuActiveAsync()
        {
            var discreteGpu = await GetDiscreteGpuAsync();
            return discreteGpu?.IsActive ?? false;
        }

        public async Task<bool> PowerOffDiscreteGpuAsync()
        {
            try
            {
                // Legion-specific
                if (File.Exists(LEGION_DGPU_PATH))
                {
                    await File.WriteAllTextAsync(LEGION_DGPU_PATH, "1");
                    return true;
                }

                // Generic PCI power control
                var discreteGpu = await GetDiscreteGpuAsync();
                if (discreteGpu != null)
                {
                    var powerControl = $"/sys/bus/pci/devices/{discreteGpu.BusId}/power/control";
                    if (File.Exists(powerControl))
                    {
                        await File.WriteAllTextAsync(powerControl, "auto");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to power off discrete GPU", ex);
                return false;
            }
        }

        public async Task<bool> PowerOnDiscreteGpuAsync()
        {
            try
            {
                // Legion-specific
                if (File.Exists(LEGION_DGPU_PATH))
                {
                    await File.WriteAllTextAsync(LEGION_DGPU_PATH, "0");
                    return true;
                }

                // Generic PCI power control
                var discreteGpu = await GetDiscreteGpuAsync();
                if (discreteGpu != null)
                {
                    var powerControl = $"/sys/bus/pci/devices/{discreteGpu.BusId}/power/control";
                    if (File.Exists(powerControl))
                    {
                        await File.WriteAllTextAsync(powerControl, "on");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to power on discrete GPU", ex);
                return false;
            }
        }

        public async Task<GpuInfo?> GetPrimaryGpuAsync()
        {
            var gpus = await GetGpuInfoAsync();
            return gpus.FirstOrDefault(g => g.IsActive) ?? gpus.FirstOrDefault();
        }

        public async Task<GpuInfo?> GetDiscreteGpuAsync()
        {
            var gpus = await GetGpuInfoAsync();
            return gpus.FirstOrDefault(g => g.Type == GpuType.Discrete);
        }

        private async Task<bool> IsNvidiaAvailable()
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "prime-select",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}