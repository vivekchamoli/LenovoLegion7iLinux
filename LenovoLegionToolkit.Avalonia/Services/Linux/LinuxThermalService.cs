using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.Services.Linux
{
    public class LinuxThermalService : IThermalService
    {
        private const string HWMON_PATH = "/sys/class/hwmon";
        private const string THERMAL_ZONE_PATH = "/sys/class/thermal";
        private const string LEGION_FAN1_SPEED = "/sys/kernel/legion_laptop/fan1_speed";
        private const string LEGION_FAN2_SPEED = "/sys/kernel/legion_laptop/fan2_speed";
        private const string LEGION_FAN_MODE = "/sys/kernel/legion_laptop/fan_mode";

        private readonly Dictionary<string, string> _hwmonSensors = new();
        private readonly Dictionary<string, HwmonDevice> _hwmonDevices = new();
        private readonly IFileSystemService _fileSystem;
        private readonly IProcessRunner _processRunner;
        private System.Timers.Timer? _monitoringTimer;
        private bool _isMonitoring;

        public event EventHandler<ThermalInfo>? ThermalInfoUpdated;

        public LinuxThermalService() : this(new FileSystemService(), new ProcessRunner())
        {
        }

        public LinuxThermalService(IFileSystemService fileSystem, IProcessRunner processRunner)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
            Task.Run(async () => await DiscoverHwmonSensorsAsync());
        }

        private class HwmonDevice
        {
            public string Path { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public Dictionary<string, string> TempSensors { get; set; } = new();
            public Dictionary<string, string> FanSensors { get; set; } = new();
        }

        public async Task<ThermalInfo?> GetThermalInfoAsync()
        {
            try
            {
                var info = new ThermalInfo
                {
                    CpuTemperature = await GetCpuTemperatureAsync(),
                    GpuTemperature = await GetGpuTemperatureAsync(),
                    Fans = await GetFansInfoAsync(),
                    ThermalZones = await GetThermalZonesAsync(),
                    Timestamp = DateTime.Now
                };

                // Get system temperature (average of all thermal zones)
                if (info.ThermalZones.Any())
                {
                    info.SystemTemperature = info.ThermalZones.Average(z => z.Temperature);
                }

                return info;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get thermal info", ex);
                return null;
            }
        }

        public async Task<double> GetCpuTemperatureAsync()
        {
            try
            {
                // Try Legion-specific sensor first
                var legionCpuTemp = "/sys/kernel/legion_laptop/cpu_temp";
                if (File.Exists(legionCpuTemp))
                {
                    var value = await LinuxPlatform.ReadSysfsAsync(legionCpuTemp);
                    if (double.TryParse(value, out var temp))
                        return temp;
                }

                // Look for coretemp or k10temp in hwmon
                foreach (var device in _hwmonDevices.Values)
                {
                    if (device.Name == "coretemp" || device.Name == "k10temp" || device.Name == "zenpower")
                    {
                        // Find package temperature or core 0
                        var packageTemp = device.TempSensors.FirstOrDefault(s =>
                            s.Value.Contains("Package") || s.Value.Contains("Tctl"));

                        if (packageTemp.Key != null)
                        {
                            var tempPath = Path.Combine(device.Path, packageTemp.Key);
                            var value = await LinuxPlatform.ReadSysfsAsync(tempPath);
                            if (double.TryParse(value, out var temp))
                                return temp / 1000.0; // Convert from millidegrees
                        }

                        // Fallback to first core temp
                        var coreTemp = device.TempSensors.FirstOrDefault();
                        if (coreTemp.Key != null)
                        {
                            var tempPath = Path.Combine(device.Path, coreTemp.Key);
                            var value = await LinuxPlatform.ReadSysfsAsync(tempPath);
                            if (double.TryParse(value, out var temp))
                                return temp / 1000.0;
                        }
                    }
                }

                // Fallback to thermal zone
                var thermalZones = await GetThermalZonesAsync();
                var cpuZone = thermalZones.FirstOrDefault(z =>
                    z.Type.Contains("cpu", StringComparison.OrdinalIgnoreCase) ||
                    z.Type.Contains("x86_pkg_temp", StringComparison.OrdinalIgnoreCase));

                if (cpuZone != null)
                    return cpuZone.Temperature;

                Logger.Warning("No CPU temperature sensor found");
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get CPU temperature", ex);
                return 0;
            }
        }

        public async Task<double> GetGpuTemperatureAsync()
        {
            try
            {
                // Try Legion-specific sensor first
                var legionGpuTemp = "/sys/kernel/legion_laptop/gpu_temp";
                if (File.Exists(legionGpuTemp))
                {
                    var value = await LinuxPlatform.ReadSysfsAsync(legionGpuTemp);
                    if (double.TryParse(value, out var temp))
                        return temp;
                }

                // Look for NVIDIA or AMD GPU in hwmon
                foreach (var device in _hwmonDevices.Values)
                {
                    if (device.Name == "nvidia" || device.Name.Contains("gpu"))
                    {
                        var gpuTemp = device.TempSensors.FirstOrDefault();
                        if (gpuTemp.Key != null)
                        {
                            var tempPath = Path.Combine(device.Path, gpuTemp.Key);
                            var value = await LinuxPlatform.ReadSysfsAsync(tempPath);
                            if (double.TryParse(value, out var temp))
                                return temp / 1000.0;
                        }
                    }
                    else if (device.Name == "amdgpu" || device.Name == "radeon")
                    {
                        var edgeTemp = device.TempSensors.FirstOrDefault(s => s.Value.Contains("edge"));
                        if (edgeTemp.Key != null)
                        {
                            var tempPath = Path.Combine(device.Path, edgeTemp.Key);
                            var value = await LinuxPlatform.ReadSysfsAsync(tempPath);
                            if (double.TryParse(value, out var temp))
                                return temp / 1000.0;
                        }
                    }
                }

                // Try NVIDIA via nvidia-smi (if available)
                try
                {
                    var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "nvidia-smi",
                        Arguments = "--query-gpu=temperature.gpu --format=csv,noheader,nounits",
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
                            if (double.TryParse(output.Trim(), out var temp))
                                return temp;
                        }
                    }
                }
                catch
                {
                    // nvidia-smi not available
                }

                Logger.Debug("No GPU temperature sensor found");
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get GPU temperature", ex);
                return 0;
            }
        }

        public async Task<List<FanInfo>> GetFansInfoAsync()
        {
            var fans = new List<FanInfo>();

            try
            {
                // Try Legion-specific fan controls first
                if (File.Exists(LEGION_FAN1_SPEED))
                {
                    var fan1 = new FanInfo
                    {
                        Id = 0,
                        Name = "CPU Fan",
                        MaxSpeed = 5000
                    };

                    var speed1 = await LinuxPlatform.ReadSysfsAsync(LEGION_FAN1_SPEED);
                    if (int.TryParse(speed1, out var rpm1))
                        fan1.CurrentSpeed = rpm1;

                    fans.Add(fan1);
                }

                if (File.Exists(LEGION_FAN2_SPEED))
                {
                    var fan2 = new FanInfo
                    {
                        Id = 1,
                        Name = "GPU Fan",
                        MaxSpeed = 5000
                    };

                    var speed2 = await LinuxPlatform.ReadSysfsAsync(LEGION_FAN2_SPEED);
                    if (int.TryParse(speed2, out var rpm2))
                        fan2.CurrentSpeed = rpm2;

                    fans.Add(fan2);
                }

                // Get fan mode if available
                if (File.Exists(LEGION_FAN_MODE) && fans.Any())
                {
                    var mode = await LinuxPlatform.ReadSysfsAsync(LEGION_FAN_MODE);
                    var isAuto = mode == "0" || mode?.ToLower() == "auto";
                    foreach (var fan in fans)
                    {
                        fan.IsAutomatic = isAuto;
                    }
                }

                // If no Legion fans found, try generic hwmon
                if (!fans.Any())
                {
                    foreach (var device in _hwmonDevices.Values)
                    {
                        foreach (var fanSensor in device.FanSensors)
                        {
                            var fanPath = Path.Combine(device.Path, fanSensor.Key);
                            var value = await LinuxPlatform.ReadSysfsAsync(fanPath);

                            if (int.TryParse(value, out var rpm))
                            {
                                var fanId = fans.Count;
                                fans.Add(new FanInfo
                                {
                                    Id = fanId,
                                    Name = fanSensor.Value ?? $"Fan {fanId + 1}",
                                    CurrentSpeed = rpm,
                                    MaxSpeed = 5000,
                                    IsAutomatic = true
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get fan info", ex);
            }

            return fans;
        }

        public async Task<bool> SetFanSpeedAsync(int fanId, int speed)
        {
            try
            {
                string fanPath = fanId switch
                {
                    0 => "/sys/kernel/legion_laptop/fan1_target",
                    1 => "/sys/kernel/legion_laptop/fan2_target",
                    _ => null
                } ?? string.Empty;

                if (!File.Exists(fanPath))
                {
                    Logger.Warning($"Fan control not available for fan {fanId}");
                    return false;
                }

                // Set manual mode first
                if (File.Exists(LEGION_FAN_MODE))
                {
                    await LinuxPlatform.WriteSysfsAsync(LEGION_FAN_MODE, "1");
                }

                // Set target speed
                var success = await LinuxPlatform.WriteSysfsAsync(fanPath, speed.ToString());

                if (success)
                {
                    Logger.Info($"Fan {fanId} speed set to {speed} RPM");
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set fan {fanId} speed", ex);
                return false;
            }
        }

        public async Task<bool> SetFanModeAsync(int fanId, FanMode mode)
        {
            try
            {
                if (!File.Exists(LEGION_FAN_MODE))
                {
                    Logger.Warning("Fan mode control not available");
                    return false;
                }

                var modeValue = mode switch
                {
                    FanMode.Auto => "0",
                    FanMode.Manual => "1",
                    FanMode.Custom => "2",
                    _ => "0"
                };

                var success = await LinuxPlatform.WriteSysfsAsync(LEGION_FAN_MODE, modeValue);

                if (success)
                {
                    Logger.Info($"Fan mode set to {mode}");
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set fan mode to {mode}", ex);
                return false;
            }
        }

        public async Task<bool> SetFanModeAsync(FanMode mode)
        {
            // Set fan mode for all fans (fanId 0 is typically all fans)
            return await SetFanModeAsync(0, mode);
        }

        public async Task<bool> SetFanProfileAsync(int fanId, FanProfile profile)
        {
            try
            {
                // Fan profiles would need custom implementation
                // For now, just apply the curve points
                if (profile.Points.Any())
                {
                    // Find current temperature
                    var currentTemp = fanId == 0
                        ? await GetCpuTemperatureAsync()
                        : await GetGpuTemperatureAsync();

                    // Find appropriate speed from curve
                    var targetPoint = profile.Points
                        .Where(p => p.Temperature <= currentTemp)
                        .OrderByDescending(p => p.Temperature)
                        .FirstOrDefault();

                    if (targetPoint != null)
                    {
                        return await SetFanSpeedAsync(fanId, targetPoint.FanSpeed);
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set fan profile for fan {fanId}", ex);
                return false;
            }
        }

        public async Task<List<ThermalZone>> GetThermalZonesAsync()
        {
            var zones = new List<ThermalZone>();

            try
            {
                if (!Directory.Exists(THERMAL_ZONE_PATH))
                    return zones;

                var zoneDirs = Directory.GetDirectories(THERMAL_ZONE_PATH, "thermal_zone*");

                foreach (var zoneDir in zoneDirs)
                {
                    var zone = new ThermalZone
                    {
                        Path = zoneDir
                    };

                    // Read zone type
                    var typePath = Path.Combine(zoneDir, "type");
                    if (File.Exists(typePath))
                    {
                        zone.Type = await LinuxPlatform.ReadSysfsAsync(typePath) ?? "unknown";
                        zone.Name = zone.Type;
                    }

                    // Read temperature
                    var tempPath = Path.Combine(zoneDir, "temp");
                    if (File.Exists(tempPath))
                    {
                        var value = await LinuxPlatform.ReadSysfsAsync(tempPath);
                        if (double.TryParse(value, out var temp))
                            zone.Temperature = temp / 1000.0; // Convert from millidegrees
                    }

                    // Read trip points
                    var trip0Path = Path.Combine(zoneDir, "trip_point_0_temp");
                    if (File.Exists(trip0Path))
                    {
                        var value = await LinuxPlatform.ReadSysfsAsync(trip0Path);
                        if (double.TryParse(value, out var temp))
                            zone.CriticalTemp = temp / 1000.0;
                    }

                    zones.Add(zone);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get thermal zones", ex);
            }

            return zones;
        }

        public async Task<bool> SetFanControlAsync(bool automatic)
        {
            try
            {
                var mode = automatic ? FanMode.Auto : FanMode.Manual;
                return await SetFanModeAsync(mode);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set fan control to {(automatic ? "automatic" : "manual")}", ex);
                return false;
            }
        }

        public async Task<bool> GetFanControlStateAsync()
        {
            try
            {
                if (!File.Exists(LEGION_FAN_MODE))
                    return true; // Default to automatic

                var value = await LinuxPlatform.ReadSysfsAsync(LEGION_FAN_MODE);
                return value == "0"; // 0 = auto, 1 = manual, 2 = custom
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get fan control state", ex);
                return true; // Default to automatic
            }
        }

        public async Task<bool> StartMonitoringAsync(TimeSpan interval)
        {
            if (_isMonitoring)
                return true;

            try
            {
                _monitoringTimer = new System.Timers.Timer(interval.TotalMilliseconds);
                _monitoringTimer.Elapsed += async (s, e) => await OnMonitoringTimerElapsed();
                _monitoringTimer.Start();
                _isMonitoring = true;

                Logger.Info($"Thermal monitoring started with interval: {interval}");

                // Trigger initial update
                await OnMonitoringTimerElapsed();

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to start thermal monitoring", ex);
                return false;
            }
        }

        public async Task StopMonitoringAsync()
        {
            if (!_isMonitoring)
                return;

            _monitoringTimer?.Stop();
            _monitoringTimer?.Dispose();
            _monitoringTimer = null;
            _isMonitoring = false;

            Logger.Info("Thermal monitoring stopped");
            await Task.CompletedTask;
        }

        private async Task OnMonitoringTimerElapsed()
        {
            try
            {
                var info = await GetThermalInfoAsync();
                if (info != null)
                {
                    ThermalInfoUpdated?.Invoke(this, info);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error during thermal monitoring update", ex);
            }
        }

        private async Task DiscoverHwmonSensorsAsync()
        {
            try
            {
                if (!Directory.Exists(HWMON_PATH))
                {
                    Logger.Warning("hwmon not available");
                    return;
                }

                var hwmonDirs = Directory.GetDirectories(HWMON_PATH, "hwmon*");

                foreach (var hwmonDir in hwmonDirs)
                {
                    var device = new HwmonDevice
                    {
                        Path = hwmonDir
                    };

                    // Read device name
                    var namePath = Path.Combine(hwmonDir, "name");
                    if (File.Exists(namePath))
                    {
                        device.Name = await LinuxPlatform.ReadSysfsAsync(namePath) ?? "unknown";
                    }

                    // Discover temperature sensors
                    var tempInputs = Directory.GetFiles(hwmonDir, "temp*_input");
                    foreach (var tempInput in tempInputs)
                    {
                        var baseName = Path.GetFileName(tempInput).Replace("_input", "");
                        var labelPath = Path.Combine(hwmonDir, $"{baseName}_label");

                        var label = "Temperature";
                        if (File.Exists(labelPath))
                        {
                            label = await LinuxPlatform.ReadSysfsAsync(labelPath) ?? baseName;
                        }

                        device.TempSensors[Path.GetFileName(tempInput)] = label;
                    }

                    // Discover fan sensors
                    var fanInputs = Directory.GetFiles(hwmonDir, "fan*_input");
                    foreach (var fanInput in fanInputs)
                    {
                        var baseName = Path.GetFileName(fanInput).Replace("_input", "");
                        var labelPath = Path.Combine(hwmonDir, $"{baseName}_label");

                        var label = "Fan";
                        if (File.Exists(labelPath))
                        {
                            label = await LinuxPlatform.ReadSysfsAsync(labelPath) ?? baseName;
                        }

                        device.FanSensors[Path.GetFileName(fanInput)] = label;
                    }

                    _hwmonDevices[device.Name] = device;
                    Logger.Debug($"Discovered hwmon device: {device.Name} with {device.TempSensors.Count} temp sensors and {device.FanSensors.Count} fan sensors");
                }

                Logger.Info($"Discovered {_hwmonDevices.Count} hwmon devices");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to discover hwmon sensors", ex);
            }
        }
    }
}