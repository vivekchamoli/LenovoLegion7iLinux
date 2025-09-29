using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.Services.Linux
{
    public class LinuxHardwareService : IHardwareService
    {
        private const string DMI_PATH = "/sys/class/dmi/id";
        private const string LEGION_MODULE_PATH = "/sys/kernel/legion_laptop";
        private HardwareInfo? _cachedHardwareInfo;
        private DeviceCapabilities? _cachedCapabilities;

        public async Task<HardwareInfo> GetHardwareInfoAsync()
        {
            if (_cachedHardwareInfo != null)
                return _cachedHardwareInfo;

            var info = new HardwareInfo();

            try
            {
                // Read DMI information
                info.Model = await ReadDmiAsync("product_name") ?? "Unknown";
                info.Manufacturer = await ReadDmiAsync("sys_vendor") ?? "Lenovo";
                info.BiosVersion = await ReadDmiAsync("bios_version") ?? "Unknown";
                info.BiosDate = await ReadDmiAsync("bios_date") ?? "Unknown";
                info.SerialNumber = await ReadDmiAsync("product_serial") ?? "Unknown";

                // Detect Legion generation from model
                info.Generation = DetectGeneration(info.Model);

                // Get CPU info
                info.CpuModel = await GetCpuModelAsync();

                // Get GPU info
                info.GpuModel = await GetGpuModelAsync();

                // Get RAM size
                info.RamSizeGB = await GetRamSizeAsync();

                // Get kernel version
                info.KernelVersion = LinuxPlatform.KernelVersion;

                // Get distribution
                info.Distribution = LinuxPlatform.Distribution;

                // Check Legion kernel module
                info.HasLegionKernelModule = await CheckKernelModuleAsync();

                // Get capabilities
                var caps = await DetectCapabilitiesAsync();
                foreach (var feature in Enum.GetValues<HardwareFeature>())
                {
                    info.Capabilities.Add(new HardwareCapability
                    {
                        Name = feature.ToString(),
                        IsSupported = caps.Features.GetValueOrDefault(feature, false),
                        Description = GetFeatureDescription(feature)
                    });
                }

                _cachedHardwareInfo = info;
                Logger.Info($"Hardware detected: {info.Model} ({info.Generation}), BIOS: {info.BiosVersion}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get hardware info", ex);
            }

            return info;
        }

        public async Task<DeviceCapabilities> DetectCapabilitiesAsync()
        {
            if (_cachedCapabilities != null)
                return _cachedCapabilities;

            var caps = new DeviceCapabilities();

            try
            {
                // Check each feature's availability
                caps.Features[HardwareFeature.PowerModeControl] =
                    File.Exists("/sys/kernel/legion_laptop/power_mode") ||
                    File.Exists("/sys/firmware/acpi/platform_profile");

                caps.Features[HardwareFeature.BatteryConservation] =
                    File.Exists("/sys/bus/platform/drivers/ideapad_acpi/VPC2004:00/conservation_mode");

                caps.Features[HardwareFeature.RapidCharge] =
                    File.Exists("/sys/kernel/legion_laptop/rapid_charge");

                caps.Features[HardwareFeature.ThermalMonitoring] =
                    Directory.Exists("/sys/class/hwmon") ||
                    Directory.Exists("/sys/class/thermal");

                caps.Features[HardwareFeature.FanControl] =
                    File.Exists("/sys/kernel/legion_laptop/fan1_speed") ||
                    Directory.GetFiles("/sys/class/hwmon", "fan*_input", SearchOption.AllDirectories).Any();

                caps.Features[HardwareFeature.KeyboardBacklight] =
                    Directory.Exists("/sys/class/leds") &&
                    Directory.GetDirectories("/sys/class/leds", "*kbd_backlight*").Any();

                caps.Features[HardwareFeature.RgbKeyboard] =
                    File.Exists("/sys/kernel/legion_laptop/rgb_keyboard") ||
                    File.Exists("/sys/class/leds/platform::kbd_backlight/multi_intensity");

                caps.Features[HardwareFeature.HybridMode] =
                    File.Exists("/sys/kernel/legion_laptop/hybrid_mode") ||
                    File.Exists("/sys/devices/platform/ideapad/hybrid_graphics");

                caps.Features[HardwareFeature.OverDrive] =
                    File.Exists("/sys/kernel/legion_laptop/overdrive");

                caps.Features[HardwareFeature.DiscreteGpuControl] =
                    File.Exists("/sys/kernel/legion_laptop/dgpu_disable");

                caps.Features[HardwareFeature.DisplayRefreshRate] =
                    File.Exists("/sys/kernel/legion_laptop/panel_refresh_rate");

                caps.Features[HardwareFeature.TouchpadLock] =
                    File.Exists("/sys/kernel/legion_laptop/touchpad");

                caps.Features[HardwareFeature.FnLock] =
                    File.Exists("/sys/kernel/legion_laptop/fn_lock");

                caps.Features[HardwareFeature.WinKeyDisable] =
                    File.Exists("/sys/kernel/legion_laptop/win_key");

                caps.Features[HardwareFeature.CameraPrivacy] =
                    File.Exists("/sys/kernel/legion_laptop/camera_power");

                caps.Features[HardwareFeature.MicrophoneMute] =
                    File.Exists("/sys/kernel/legion_laptop/microphone_mute");

                // Detect supported power modes
                if (caps.Features[HardwareFeature.PowerModeControl])
                {
                    caps.SupportedPowerModes = new List<PowerMode>
                    {
                        PowerMode.Quiet,
                        PowerMode.Balanced,
                        PowerMode.Performance
                    };

                    if (File.Exists("/sys/kernel/legion_laptop/custom_mode"))
                    {
                        caps.SupportedPowerModes.Add(PowerMode.Custom);
                    }
                }

                // Check for battery
                caps.HasBattery = Directory.Exists("/sys/class/power_supply/BAT0");

                // Check for discrete GPU
                caps.HasDiscreteGpu = await HasDiscreteGpuAsync();

                // Detect keyboard backlight type
                caps.KeyboardType = await DetectKeyboardTypeAsync();
                caps.HasKeyboardBacklight = caps.KeyboardType != KeyboardBacklightType.None;

                // Set max fan speed (typical for Legion laptops)
                caps.MaxFanSpeed = 5000;

                _cachedCapabilities = caps;

                // Log detected capabilities
                var supportedFeatures = caps.Features.Where(f => f.Value).Select(f => f.Key.ToString());
                Logger.Info($"Detected capabilities: {string.Join(", ", supportedFeatures)}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to detect capabilities", ex);
            }

            return caps;
        }

        public async Task<bool> IsFeatureSupportedAsync(HardwareFeature feature)
        {
            var caps = await DetectCapabilitiesAsync();
            return caps.Features.GetValueOrDefault(feature, false);
        }

        public async Task<string?> GetBiosVersionAsync()
        {
            return await ReadDmiAsync("bios_version");
        }

        public async Task<string?> GetSerialNumberAsync()
        {
            return await ReadDmiAsync("product_serial");
        }

        public async Task<bool> CheckKernelModuleAsync()
        {
            return await Task.FromResult(LinuxPlatform.HasLegionKernelModule);
        }

        public async Task<bool> LoadKernelModuleAsync()
        {
            return await LinuxPlatform.LoadLegionModuleAsync();
        }

        public async Task<Dictionary<string, string>> GetSystemInfoAsync()
        {
            var info = new Dictionary<string, string>();

            try
            {
                var hw = await GetHardwareInfoAsync();

                info["Model"] = hw.Model;
                info["Manufacturer"] = hw.Manufacturer;
                info["BIOS Version"] = hw.BiosVersion;
                info["BIOS Date"] = hw.BiosDate;
                info["Serial Number"] = hw.SerialNumber;
                info["Generation"] = hw.Generation;
                info["CPU"] = hw.CpuModel;
                info["GPU"] = hw.GpuModel;
                info["RAM"] = $"{hw.RamSizeGB} GB";
                info["Kernel"] = hw.KernelVersion;
                info["Distribution"] = hw.Distribution;
                info["Legion Module"] = hw.HasLegionKernelModule ? "Loaded" : "Not Loaded";

                // Add current user
                info["User"] = Environment.UserName;

                // Add uptime
                var uptimePath = "/proc/uptime";
                if (File.Exists(uptimePath))
                {
                    var content = await File.ReadAllTextAsync(uptimePath);
                    var parts = content.Split(' ');
                    if (double.TryParse(parts[0], out var seconds))
                    {
                        var uptime = TimeSpan.FromSeconds(seconds);
                        info["Uptime"] = $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get system info", ex);
            }

            return info;
        }

        private async Task<string?> ReadDmiAsync(string field)
        {
            try
            {
                var path = Path.Combine(DMI_PATH, field);
                if (File.Exists(path))
                {
                    var value = await LinuxPlatform.ReadSysfsAsync(path);
                    return value?.Trim();
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to read DMI field {field}: {ex.Message}");
            }

            return null;
        }

        private string DetectGeneration(string model)
        {
            if (string.IsNullOrEmpty(model))
                return "Unknown";

            // Legion patterns: Y540, Y740, 5-15, 5i-15, 5 Pro, 7i Gen 6, etc.
            var patterns = new Dictionary<string, string>
            {
                { @"Y5[234]0", "Gen 4" },
                { @"Y[67][234]0", "Gen 5" },
                { @"5.*15\w+H.*202[01]", "Gen 6" },
                { @"5.*15\w+H.*2022", "Gen 7" },
                { @"5.*15\w+H.*2023", "Gen 8" },
                { @"5.*15\w+H.*2024", "Gen 9" },
                { @"5i?.*Gen\s*6", "Gen 6" },
                { @"5i?.*Gen\s*7", "Gen 7" },
                { @"5i?.*Gen\s*8", "Gen 8" },
                { @"5i?.*Gen\s*9", "Gen 9" },
                { @"7i?.*Gen\s*6", "Gen 6" },
                { @"7i?.*Gen\s*7", "Gen 7" },
                { @"7i?.*Gen\s*8", "Gen 8" },
                { @"7i?.*Gen\s*9", "Gen 9" },
                { @"9i?.*Gen\s*9", "Gen 9" },
                { @"Pro.*7.*Gen\s*8", "Gen 8" },
                { @"Pro.*7.*Gen\s*9", "Gen 9" },
                { @"Slim", "Slim Series" },
                { @"LOQ", "LOQ Series" }
            };

            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(model, pattern.Key, RegexOptions.IgnoreCase))
                {
                    return pattern.Value;
                }
            }

            // Try to extract year
            var yearMatch = Regex.Match(model, @"20(2[0-9])");
            if (yearMatch.Success)
            {
                var year = int.Parse("20" + yearMatch.Groups[1].Value);
                return year switch
                {
                    2020 or 2021 => "Gen 6",
                    2022 => "Gen 7",
                    2023 => "Gen 8",
                    2024 => "Gen 9",
                    _ => $"MY{year}"
                };
            }

            return "Unknown";
        }

        private async Task<string> GetCpuModelAsync()
        {
            try
            {
                var cpuInfoPath = "/proc/cpuinfo";
                if (File.Exists(cpuInfoPath))
                {
                    var lines = await File.ReadAllLinesAsync(cpuInfoPath);
                    var modelLine = lines.FirstOrDefault(l => l.StartsWith("model name"));
                    if (!string.IsNullOrEmpty(modelLine))
                    {
                        var parts = modelLine.Split(':', 2);
                        if (parts.Length > 1)
                        {
                            return parts[1].Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to get CPU model: {ex.Message}");
            }

            return "Unknown CPU";
        }

        private async Task<string> GetGpuModelAsync()
        {
            try
            {
                // Try lspci first
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "lspci",
                    Arguments = "-mm",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var lines = output.Split('\n');

                    // Look for VGA or 3D controller
                    foreach (var line in lines)
                    {
                        if (line.Contains("VGA") || line.Contains("3D"))
                        {
                            // Extract GPU model (usually in quotes)
                            var matches = Regex.Matches(line, "\"([^\"]+)\"");
                            if (matches.Count >= 3)
                            {
                                var vendor = matches[1].Value.Trim('"');
                                var model = matches[2].Value.Trim('"');

                                // Clean up common patterns
                                model = model.Replace("[", "").Replace("]", "");

                                if (vendor.Contains("NVIDIA") || model.Contains("NVIDIA"))
                                {
                                    var gpuMatch = Regex.Match(model, @"(GeForce|Quadro|Tesla).*?(RTX|GTX|MX)?\s*\d+\w*");
                                    if (gpuMatch.Success)
                                        return gpuMatch.Value;
                                }
                                else if (vendor.Contains("AMD") || model.Contains("AMD"))
                                {
                                    var gpuMatch = Regex.Match(model, @"(Radeon|Vega).*?(RX)?\s*\d+\w*");
                                    if (gpuMatch.Success)
                                        return gpuMatch.Value;
                                }

                                return model;
                            }
                        }
                    }
                }
            }
            catch
            {
                // lspci not available
            }

            // Fallback: Check for NVIDIA via nvidia-smi
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = "--query-gpu=name --format=csv,noheader",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process != null)
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode == 0)
                    {
                        var output = await process.StandardOutput.ReadToEndAsync();
                        return output.Trim();
                    }
                }
            }
            catch
            {
                // nvidia-smi not available
            }

            return "Unknown GPU";
        }

        private async Task<int> GetRamSizeAsync()
        {
            try
            {
                var meminfoPath = "/proc/meminfo";
                if (File.Exists(meminfoPath))
                {
                    var lines = await File.ReadAllLinesAsync(meminfoPath);
                    var memTotalLine = lines.FirstOrDefault(l => l.StartsWith("MemTotal:"));
                    if (!string.IsNullOrEmpty(memTotalLine))
                    {
                        var match = Regex.Match(memTotalLine, @"(\d+)");
                        if (match.Success && long.TryParse(match.Groups[1].Value, out var kb))
                        {
                            // Convert KB to GB and round to nearest standard size
                            var gb = kb / (1024.0 * 1024.0);
                            var standardSizes = new[] { 4, 8, 16, 32, 64, 128 };
                            return standardSizes.OrderBy(s => Math.Abs(s - gb)).First();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to get RAM size: {ex.Message}");
            }

            return 16; // Default fallback
        }

        private async Task<bool> HasDiscreteGpuAsync()
        {
            try
            {
                // Check for NVIDIA
                var nvidiaPath = "/sys/bus/pci/drivers/nvidia";
                if (Directory.Exists(nvidiaPath))
                    return true;

                // Check for AMD discrete GPU
                var amdgpuPath = "/sys/bus/pci/drivers/amdgpu";
                if (Directory.Exists(amdgpuPath))
                {
                    // Check if it's not just integrated graphics
                    var gpuModel = await GetGpuModelAsync();
                    if (gpuModel.Contains("RX") || gpuModel.Contains("Radeon Pro"))
                        return true;
                }

                // Check via lspci for multiple VGA controllers
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "lspci",
                    Arguments = "-d ::0300",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var gpuCount = output.Split('\n').Count(l => !string.IsNullOrWhiteSpace(l));
                    return gpuCount > 1;
                }
            }
            catch
            {
                // Ignore detection errors
            }

            return false;
        }

        private async Task<KeyboardBacklightType> DetectKeyboardTypeAsync()
        {
            try
            {
                // Check for RGB per-key
                if (File.Exists("/sys/kernel/legion_laptop/rgb_keyboard"))
                    return KeyboardBacklightType.RGBPerKey;

                // Check for 4-zone RGB
                if (File.Exists("/sys/kernel/legion_laptop/rgb_4zone"))
                    return KeyboardBacklightType.RGB4Zone;

                // Check for white backlight
                var kbdBacklightDirs = Directory.GetDirectories("/sys/class/leds", "*kbd_backlight*");
                if (kbdBacklightDirs.Any())
                {
                    var brightnessPath = Path.Combine(kbdBacklightDirs[0], "max_brightness");
                    if (File.Exists(brightnessPath))
                    {
                        var maxBrightness = await LinuxPlatform.ReadSysfsAsync(brightnessPath);
                        if (int.TryParse(maxBrightness, out var max))
                        {
                            return max > 1 ? KeyboardBacklightType.WhiteMultiLevel : KeyboardBacklightType.White;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to detect keyboard type: {ex.Message}");
            }

            return KeyboardBacklightType.None;
        }

        private string GetFeatureDescription(HardwareFeature feature)
        {
            return feature switch
            {
                HardwareFeature.PowerModeControl => "Switch between power modes (Quiet, Balanced, Performance)",
                HardwareFeature.BatteryConservation => "Limit battery charge to preserve battery life",
                HardwareFeature.RapidCharge => "Enable faster battery charging",
                HardwareFeature.ThermalMonitoring => "Monitor CPU, GPU, and system temperatures",
                HardwareFeature.FanControl => "Control fan speeds and modes",
                HardwareFeature.KeyboardBacklight => "Control keyboard backlight brightness",
                HardwareFeature.RgbKeyboard => "Customize RGB keyboard lighting",
                HardwareFeature.HybridMode => "Switch between integrated and discrete graphics",
                HardwareFeature.OverDrive => "Enable display overdrive for reduced ghosting",
                HardwareFeature.DiscreteGpuControl => "Enable or disable discrete GPU",
                HardwareFeature.PanelOverdrive => "Control display panel overdrive",
                HardwareFeature.DisplayRefreshRate => "Change display refresh rate",
                HardwareFeature.TouchpadLock => "Lock or unlock the touchpad",
                HardwareFeature.FnLock => "Toggle Fn key lock state",
                HardwareFeature.WinKeyDisable => "Disable Windows/Super key",
                HardwareFeature.CameraPrivacy => "Control camera privacy mode",
                HardwareFeature.MicrophoneMute => "Mute or unmute microphone",
                _ => "Unknown feature"
            };
        }
    }
}