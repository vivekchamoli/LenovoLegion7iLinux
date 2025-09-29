using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.Services.Linux
{
    /// <summary>
    /// OPTIMIZED Linux Thermal Service with proper resource management and performance optimization
    /// Fixes: Memory leaks, async patterns, thread safety, adaptive polling, resource disposal
    /// </summary>
    public class LinuxThermalService : IThermalService, IDisposable
    {
        private const string HWMON_PATH = "/sys/class/hwmon";
        private const string THERMAL_ZONE_PATH = "/sys/class/thermal";
        private const string LEGION_FAN1_SPEED = "/sys/kernel/legion_laptop/fan1_speed";
        private const string LEGION_FAN2_SPEED = "/sys/kernel/legion_laptop/fan2_speed";
        private const string LEGION_FAN_MODE = "/sys/kernel/legion_laptop/fan_mode";

        private readonly ConcurrentDictionary<string, HwmonDevice> _hwmonDevices = new();
        private readonly IFileSystemService _fileSystem;
        private readonly IProcessRunner _processRunner;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly SemaphoreSlim _monitoringSemaphore;
        private readonly SemaphoreSlim _discoveryLock;

        private Timer? _monitoringTimer;
        private bool _isMonitoring;
        private bool _disposed;
        private bool _sensorsDiscovered;
        private readonly object _lockObject = new object();

        public event EventHandler<ThermalInfo>? ThermalInfoUpdated;

        public LinuxThermalService() : this(new FileSystemService(), new ProcessRunner())
        {
        }

        public LinuxThermalService(IFileSystemService fileSystem, IProcessRunner processRunner)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
            _cancellationTokenSource = new CancellationTokenSource();
            _monitoringSemaphore = new SemaphoreSlim(1, 1);
            _discoveryLock = new SemaphoreSlim(1, 1);

            // Start sensor discovery asynchronously without blocking constructor
            _ = Task.Run(async () => await DiscoverHwmonSensorsAsync(), _cancellationTokenSource.Token);
        }

        private class HwmonDevice
        {
            public string Path { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public ConcurrentDictionary<string, string> TempSensors { get; set; } = new();
            public ConcurrentDictionary<string, string> FanSensors { get; set; } = new();
            public DateTime LastAccess { get; set; } = DateTime.Now;
        }

        public async Task<ThermalInfo?> GetThermalInfoAsync()
        {
            if (_disposed) return null;

            try
            {
                // Ensure sensors are discovered
                await EnsureSensorsDiscoveredAsync();

                var info = new ThermalInfo
                {
                    Timestamp = DateTime.Now
                };

                // Parallel execution for better performance
                var tasks = new[]
                {
                    GetCpuTemperatureInternalAsync().ContinueWith(t => info.CpuTemperature = t.Result, TaskScheduler.Current),
                    GetGpuTemperatureInternalAsync().ContinueWith(t => info.GpuTemperature = t.Result, TaskScheduler.Current),
                    GetFansInfoInternalAsync().ContinueWith(t => info.Fans = t.Result, TaskScheduler.Current),
                    GetThermalZonesInternalAsync().ContinueWith(t => info.ThermalZones = t.Result, TaskScheduler.Current)
                };

                await Task.WhenAll(tasks);

                // Calculate system temperature (average of all thermal zones)
                if (info.ThermalZones?.Any() == true)
                {
                    info.SystemTemperature = info.ThermalZones.Average(z => z.Temperature);
                }

                return info;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get thermal info", ex);
                return null;
            }
        }

        public async Task<double> GetCpuTemperatureAsync()
        {
            if (_disposed) return 0;
            return await GetCpuTemperatureInternalAsync();
        }

        private async Task<double> GetCpuTemperatureInternalAsync()
        {
            try
            {
                var token = _cancellationTokenSource.Token;

                // Try Legion-specific sensor first
                var legionCpuTemp = "/sys/kernel/legion_laptop/cpu_temp";
                if (File.Exists(legionCpuTemp))
                {
                    var value = await LinuxPlatform.ReadSysfsAsync(legionCpuTemp, token);
                    if (double.TryParse(value, out var temp))
                        return Math.Clamp(temp, -273, 150); // Sanity check
                }

                // Look for coretemp or k10temp in hwmon
                await EnsureSensorsDiscoveredAsync();

                foreach (var device in _hwmonDevices.Values)
                {
                    if (IsCpuThermalDevice(device.Name))
                    {
                        device.LastAccess = DateTime.Now;

                        // Find package temperature or core 0
                        var packageTemp = device.TempSensors.FirstOrDefault(s =>
                            s.Value.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                            s.Value.Contains("Tctl", StringComparison.OrdinalIgnoreCase));

                        if (packageTemp.Key != null)
                        {
                            var tempPath = Path.Combine(device.Path, packageTemp.Key);
                            var value = await LinuxPlatform.ReadSysfsAsync(tempPath, token);
                            if (double.TryParse(value, out var temp))
                                return Math.Clamp(temp / 1000.0, -273, 150); // Convert from millidegrees
                        }

                        // Fallback to first core temp
                        var coreTemp = device.TempSensors.FirstOrDefault();
                        if (coreTemp.Key != null)
                        {
                            var tempPath = Path.Combine(device.Path, coreTemp.Key);
                            var value = await LinuxPlatform.ReadSysfsAsync(tempPath, token);
                            if (double.TryParse(value, out var temp))
                                return Math.Clamp(temp / 1000.0, -273, 150);
                        }
                    }
                }

                // Fallback to thermal zone
                var thermalZones = await GetThermalZonesInternalAsync();
                var cpuZone = thermalZones?.FirstOrDefault(z =>
                    z.Type.Contains("cpu", StringComparison.OrdinalIgnoreCase) ||
                    z.Type.Contains("x86_pkg_temp", StringComparison.OrdinalIgnoreCase) ||
                    z.Type.Contains("coretemp", StringComparison.OrdinalIgnoreCase));

                if (cpuZone != null)
                    return cpuZone.Temperature;

                Logger.Debug("No CPU temperature sensor found");
                return 0;
            }
            catch (OperationCanceledException)
            {
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
            if (_disposed) return 0;
            return await GetGpuTemperatureInternalAsync();
        }

        private async Task<double> GetGpuTemperatureInternalAsync()
        {
            try
            {
                var token = _cancellationTokenSource.Token;

                // Try Legion-specific sensor first
                var legionGpuTemp = "/sys/kernel/legion_laptop/gpu_temp";
                if (File.Exists(legionGpuTemp))
                {
                    var value = await LinuxPlatform.ReadSysfsAsync(legionGpuTemp, token);
                    if (double.TryParse(value, out var temp))
                        return Math.Clamp(temp, -273, 150);
                }

                // Look for NVIDIA or AMD GPU in hwmon
                await EnsureSensorsDiscoveredAsync();

                foreach (var device in _hwmonDevices.Values)
                {
                    if (IsGpuThermalDevice(device.Name))
                    {
                        device.LastAccess = DateTime.Now;

                        if (device.Name.StartsWith("nvidia", StringComparison.OrdinalIgnoreCase))
                        {
                            var gpuTemp = device.TempSensors.FirstOrDefault();
                            if (gpuTemp.Key != null)
                            {
                                var tempPath = Path.Combine(device.Path, gpuTemp.Key);
                                var value = await LinuxPlatform.ReadSysfsAsync(tempPath, token);
                                if (double.TryParse(value, out var temp))
                                    return Math.Clamp(temp / 1000.0, -273, 150);
                            }
                        }
                        else if (device.Name.StartsWith("amdgpu", StringComparison.OrdinalIgnoreCase) ||
                                 device.Name.StartsWith("radeon", StringComparison.OrdinalIgnoreCase))
                        {
                            var edgeTemp = device.TempSensors.FirstOrDefault(s =>
                                s.Value.Contains("edge", StringComparison.OrdinalIgnoreCase));
                            if (edgeTemp.Key != null)
                            {
                                var tempPath = Path.Combine(device.Path, edgeTemp.Key);
                                var value = await LinuxPlatform.ReadSysfsAsync(tempPath, token);
                                if (double.TryParse(value, out var temp))
                                    return Math.Clamp(temp / 1000.0, -273, 150);
                            }
                        }
                    }
                }

                // Try NVIDIA via nvidia-smi (with timeout)
                var nvidiaTempTask = GetNvidiaTemperatureAsync(token);
                var timeoutTask = Task.Delay(2000, token);
                var completedTask = await Task.WhenAny(nvidiaTempTask, timeoutTask);

                if (completedTask == nvidiaTempTask && !nvidiaTempTask.IsFaulted)
                {
                    var temp = await nvidiaTempTask;
                    if (temp > 0) return temp;
                }

                Logger.Debug("No GPU temperature sensor found");
                return 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get GPU temperature", ex);
                return 0;
            }
        }

        private async Task<double> GetNvidiaTemperatureAsync(CancellationToken cancellationToken)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = "--query-gpu=temperature.gpu --format=csv,noheader,nounits",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process != null)
                {
                    using (cancellationToken.Register(() => { try { process.Kill(); } catch { } }))
                    {
                        await process.WaitForExitAsync(cancellationToken);

                        if (process.ExitCode == 0)
                        {
                            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                            if (double.TryParse(output.Trim(), out var temp))
                                return Math.Clamp(temp, -273, 150);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Debug($"nvidia-smi not available: {ex.Message}");
            }

            return 0;
        }

        public async Task<List<FanInfo>?> GetFansInfoAsync()
        {
            if (_disposed) return null;
            return await GetFansInfoInternalAsync();
        }

        private async Task<List<FanInfo>?> GetFansInfoInternalAsync()
        {
            var fans = new List<FanInfo>();

            try
            {
                var token = _cancellationTokenSource.Token;

                // Try Legion-specific fan controls first
                var legionFanTasks = new[]
                {
                    ReadLegionFanAsync(LEGION_FAN1_SPEED, 0, "CPU Fan", token),
                    ReadLegionFanAsync(LEGION_FAN2_SPEED, 1, "GPU Fan", token)
                };

                var legionFans = await Task.WhenAll(legionFanTasks);
                fans.AddRange(legionFans.Where(f => f != null).Cast<FanInfo>());

                // Get fan mode if available and Legion fans found
                if (File.Exists(LEGION_FAN_MODE) && fans.Any())
                {
                    var mode = await LinuxPlatform.ReadSysfsAsync(LEGION_FAN_MODE, token);
                    var isAuto = mode == "0" || string.Equals(mode, "auto", StringComparison.OrdinalIgnoreCase);
                    foreach (var fan in fans)
                    {
                        fan.IsAutomatic = isAuto;
                    }
                }

                // If no Legion fans found, try generic hwmon
                if (!fans.Any())
                {
                    await EnsureSensorsDiscoveredAsync();

                    foreach (var device in _hwmonDevices.Values)
                    {
                        foreach (var fanSensor in device.FanSensors)
                        {
                            var fanPath = Path.Combine(device.Path, fanSensor.Key);
                            var value = await LinuxPlatform.ReadSysfsAsync(fanPath, token);

                            if (int.TryParse(value, out var rpm) && rpm >= 0)
                            {
                                var fanId = fans.Count;
                                fans.Add(new FanInfo
                                {
                                    Id = fanId,
                                    Name = fanSensor.Value ?? $"Fan {fanId + 1}",
                                    CurrentSpeed = Math.Min(rpm, 10000), // Sanity check
                                    MaxSpeed = 5000,
                                    IsAutomatic = true
                                });
                            }
                        }
                        device.LastAccess = DateTime.Now;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return fans;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get fan info", ex);
            }

            return fans;
        }

        private async Task<FanInfo?> ReadLegionFanAsync(string speedPath, int id, string name, CancellationToken cancellationToken)
        {
            if (!File.Exists(speedPath)) return null;

            try
            {
                var speedValue = await LinuxPlatform.ReadSysfsAsync(speedPath, cancellationToken);
                if (int.TryParse(speedValue, out var rpm) && rpm >= 0)
                {
                    return new FanInfo
                    {
                        Id = id,
                        Name = name,
                        CurrentSpeed = Math.Min(rpm, 10000), // Sanity check
                        MaxSpeed = 5000
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to read Legion fan {name}: {ex.Message}");
            }

            return null;
        }

        public async Task<bool> SetFanSpeedAsync(int fanId, int speed)
        {
            if (_disposed) return false;

            // Validate input
            speed = Math.Clamp(speed, 0, 5000);

            try
            {
                string fanPath = fanId switch
                {
                    0 => "/sys/kernel/legion_laptop/fan1_target",
                    1 => "/sys/kernel/legion_laptop/fan2_target",
                    _ => string.Empty
                };

                if (string.IsNullOrEmpty(fanPath) || !File.Exists(fanPath))
                {
                    Logger.Warning($"Fan control not available for fan {fanId}");
                    return false;
                }

                var token = _cancellationTokenSource.Token;

                // Set manual mode first
                if (File.Exists(LEGION_FAN_MODE))
                {
                    await LinuxPlatform.WriteSysfsAsync(LEGION_FAN_MODE, "1", token);
                }

                // Set target speed
                var success = await LinuxPlatform.WriteSysfsAsync(fanPath, speed.ToString(), token);

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
            if (_disposed) return false;

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

                var success = await LinuxPlatform.WriteSysfsAsync(LEGION_FAN_MODE, modeValue, _cancellationTokenSource.Token);

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
            return await SetFanModeAsync(0, mode);
        }

        public async Task<bool> SetFanProfileAsync(int fanId, FanProfile profile)
        {
            if (_disposed || profile?.Points == null || !profile.Points.Any())
                return false;

            try
            {
                // Find current temperature
                var currentTemp = fanId == 0
                    ? await GetCpuTemperatureInternalAsync()
                    : await GetGpuTemperatureInternalAsync();

                // Find appropriate speed from curve with interpolation
                var targetPoint = FindTargetSpeedFromCurve(profile.Points, currentTemp);

                if (targetPoint > 0)
                {
                    return await SetFanSpeedAsync(fanId, targetPoint);
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set fan profile for fan {fanId}", ex);
                return false;
            }
        }

        private static int FindTargetSpeedFromCurve(IList<FanCurvePoint> points, double currentTemp)
        {
            var sortedPoints = points.OrderBy(p => p.Temperature).ToList();

            // If temperature is below the first point
            if (currentTemp <= sortedPoints[0].Temperature)
                return sortedPoints[0].FanSpeed;

            // If temperature is above the last point
            if (currentTemp >= sortedPoints[^1].Temperature)
                return sortedPoints[^1].FanSpeed;

            // Find the two points to interpolate between
            for (int i = 0; i < sortedPoints.Count - 1; i++)
            {
                var p1 = sortedPoints[i];
                var p2 = sortedPoints[i + 1];

                if (currentTemp >= p1.Temperature && currentTemp <= p2.Temperature)
                {
                    // Linear interpolation
                    var ratio = (currentTemp - p1.Temperature) / (p2.Temperature - p1.Temperature);
                    return (int)(p1.FanSpeed + ratio * (p2.FanSpeed - p1.FanSpeed));
                }
            }

            return sortedPoints[0].FanSpeed;
        }

        public async Task<List<ThermalZone>?> GetThermalZonesAsync()
        {
            if (_disposed) return null;
            return await GetThermalZonesInternalAsync();
        }

        private async Task<List<ThermalZone>?> GetThermalZonesInternalAsync()
        {
            var zones = new List<ThermalZone>();

            try
            {
                if (!Directory.Exists(THERMAL_ZONE_PATH))
                    return zones;

                var zoneDirs = Directory.GetDirectories(THERMAL_ZONE_PATH, "thermal_zone*");
                var token = _cancellationTokenSource.Token;

                // Process thermal zones in parallel for better performance
                var zoneDataTasks = zoneDirs.Select(async zoneDir =>
                {
                    var zone = new ThermalZone
                    {
                        Path = zoneDir
                    };

                    try
                    {
                        // Read zone type
                        var typePath = Path.Combine(zoneDir, "type");
                        if (File.Exists(typePath))
                        {
                            zone.Type = await LinuxPlatform.ReadSysfsAsync(typePath, token) ?? "unknown";
                            zone.Name = zone.Type;
                        }

                        // Read temperature
                        var tempPath = Path.Combine(zoneDir, "temp");
                        if (File.Exists(tempPath))
                        {
                            var value = await LinuxPlatform.ReadSysfsAsync(tempPath, token);
                            if (double.TryParse(value, out var temp))
                                zone.Temperature = Math.Clamp(temp / 1000.0, -273, 150); // Convert from millidegrees
                        }

                        // Read trip points
                        var trip0Path = Path.Combine(zoneDir, "trip_point_0_temp");
                        if (File.Exists(trip0Path))
                        {
                            var value = await LinuxPlatform.ReadSysfsAsync(trip0Path, token);
                            if (double.TryParse(value, out var temp))
                                zone.CriticalTemp = Math.Clamp(temp / 1000.0, 0, 150);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Failed to read thermal zone {zoneDir}: {ex.Message}");
                    }

                    return zone;
                }).ToArray();

                var zoneData = await Task.WhenAll(zoneDataTasks);
                zones.AddRange(zoneData.Where(z => z.Temperature > -100)); // Filter out invalid zones
            }
            catch (OperationCanceledException)
            {
                return zones;
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
            if (_disposed) return true;

            try
            {
                if (!File.Exists(LEGION_FAN_MODE))
                    return true; // Default to automatic

                var value = await LinuxPlatform.ReadSysfsAsync(LEGION_FAN_MODE, _cancellationTokenSource.Token);
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
            if (_disposed || _isMonitoring) return _isMonitoring;

            if (!await _monitoringSemaphore.WaitAsync(5000, _cancellationTokenSource.Token))
                return false;

            try
            {
                if (_isMonitoring) return true;

                // Adaptive interval based on system load
                var actualInterval = AdaptMonitoringInterval(interval);

                _monitoringTimer = new Timer(OnMonitoringTimerElapsed, null, TimeSpan.Zero, actualInterval);
                _isMonitoring = true;

                Logger.Info($"Thermal monitoring started with interval: {actualInterval}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to start thermal monitoring", ex);
                return false;
            }
            finally
            {
                _monitoringSemaphore.Release();
            }
        }

        public async Task StopMonitoringAsync()
        {
            if (_disposed || !_isMonitoring) return;

            if (!await _monitoringSemaphore.WaitAsync(5000, _cancellationTokenSource.Token))
                return;

            try
            {
                if (!_isMonitoring) return;

                _monitoringTimer?.Dispose();
                _monitoringTimer = null;
                _isMonitoring = false;

                Logger.Info("Thermal monitoring stopped");
            }
            finally
            {
                _monitoringSemaphore.Release();
            }
        }

        private async void OnMonitoringTimerElapsed(object? state)
        {
            // This is the only acceptable async void pattern (timer callback)
            // with proper exception handling to prevent crashes
            try
            {
                if (_disposed || _cancellationTokenSource.Token.IsCancellationRequested)
                    return;

                if (!await _monitoringSemaphore.WaitAsync(100, _cancellationTokenSource.Token))
                    return;

                try
                {
                    var info = await GetThermalInfoAsync();
                    if (info != null && !_disposed)
                    {
                        ThermalInfoUpdated?.Invoke(this, info);
                    }
                }
                finally
                {
                    _monitoringSemaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                // Critical: Catch all exceptions to prevent application crash
                Logger.Error("Thermal monitoring timer error", ex);
            }
        }

        private TimeSpan AdaptMonitoringInterval(TimeSpan requestedInterval)
        {
            // Adapt interval based on system resources and sensor availability
            var baseInterval = requestedInterval.TotalMilliseconds;

            // If many sensors are available, increase interval slightly to avoid overloading
            if (_hwmonDevices.Count > 5)
                baseInterval *= 1.2;

            // Ensure reasonable bounds
            baseInterval = Math.Clamp(baseInterval, 1000, 60000); // 1-60 seconds

            return TimeSpan.FromMilliseconds(baseInterval);
        }

        private async Task EnsureSensorsDiscoveredAsync()
        {
            if (_sensorsDiscovered || _disposed) return;

            if (!await _discoveryLock.WaitAsync(10000, _cancellationTokenSource.Token))
                return;

            try
            {
                if (_sensorsDiscovered) return;
                await DiscoverHwmonSensorsAsync();
                _sensorsDiscovered = true;
            }
            finally
            {
                _discoveryLock.Release();
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
                var token = _cancellationTokenSource.Token;

                // Process hwmon devices in parallel
                var deviceTasks = hwmonDirs.Select(async hwmonDir =>
                {
                    var device = new HwmonDevice
                    {
                        Path = hwmonDir
                    };

                    try
                    {
                        // Read device name
                        var namePath = Path.Combine(hwmonDir, "name");
                        if (File.Exists(namePath))
                        {
                            device.Name = await LinuxPlatform.ReadSysfsAsync(namePath, token) ?? "unknown";
                        }

                        // Discover temperature sensors in parallel
                        var tempInputs = Directory.GetFiles(hwmonDir, "temp*_input");
                        var tempTasks = tempInputs.Select(async tempInput =>
                        {
                            try
                            {
                                var baseName = Path.GetFileName(tempInput).Replace("_input", "");
                                var labelPath = Path.Combine(hwmonDir, $"{baseName}_label");

                                var label = "Temperature";
                                if (File.Exists(labelPath))
                                {
                                    label = await LinuxPlatform.ReadSysfsAsync(labelPath, token) ?? baseName;
                                }

                                device.TempSensors[Path.GetFileName(tempInput)] = label;
                            }
                            catch (Exception ex)
                            {
                                Logger.Debug($"Failed to read temp sensor {tempInput}: {ex.Message}");
                            }
                        });

                        // Discover fan sensors in parallel
                        var fanInputs = Directory.GetFiles(hwmonDir, "fan*_input");
                        var fanTasks = fanInputs.Select(async fanInput =>
                        {
                            try
                            {
                                var baseName = Path.GetFileName(fanInput).Replace("_input", "");
                                var labelPath = Path.Combine(hwmonDir, $"{baseName}_label");

                                var label = "Fan";
                                if (File.Exists(labelPath))
                                {
                                    label = await LinuxPlatform.ReadSysfsAsync(labelPath, token) ?? baseName;
                                }

                                device.FanSensors[Path.GetFileName(fanInput)] = label;
                            }
                            catch (Exception ex)
                            {
                                Logger.Debug($"Failed to read fan sensor {fanInput}: {ex.Message}");
                            }
                        });

                        await Task.WhenAll(tempTasks.Concat(fanTasks));
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Failed to process hwmon device {hwmonDir}: {ex.Message}");
                    }

                    return device;
                }).ToArray();

                var devices = await Task.WhenAll(deviceTasks);

                // Add discovered devices to concurrent dictionary
                foreach (var device in devices.Where(d => d.TempSensors.Any() || d.FanSensors.Any()))
                {
                    _hwmonDevices[device.Name] = device;
                    Logger.Debug($"Discovered hwmon device: {device.Name} with {device.TempSensors.Count} temp sensors and {device.FanSensors.Count} fan sensors");
                }

                Logger.Info($"Discovered {_hwmonDevices.Count} hwmon devices");

                // Start periodic cleanup of unused devices
                _ = Task.Run(async () => await PeriodicCleanupAsync(), _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to discover hwmon sensors", ex);
            }
        }

        private async Task PeriodicCleanupAsync()
        {
            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), _cancellationTokenSource.Token);

                    // Remove devices not accessed in last 10 minutes
                    var cutoff = DateTime.Now.AddMinutes(-10);
                    var devicesToRemove = _hwmonDevices
                        .Where(kvp => kvp.Value.LastAccess < cutoff)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var deviceName in devicesToRemove)
                    {
                        if (_hwmonDevices.TryRemove(deviceName, out _))
                        {
                            Logger.Debug($"Cleaned up unused hwmon device: {deviceName}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                Logger.Error("Error during hwmon devices cleanup", ex);
            }
        }

        private static bool IsCpuThermalDevice(string deviceName)
        {
            return deviceName switch
            {
                "coretemp" or "k10temp" or "zenpower" => true,
                _ => false
            };
        }

        private static bool IsGpuThermalDevice(string deviceName)
        {
            return deviceName.StartsWith("nvidia", StringComparison.OrdinalIgnoreCase) ||
                   deviceName.StartsWith("amdgpu", StringComparison.OrdinalIgnoreCase) ||
                   deviceName.StartsWith("radeon", StringComparison.OrdinalIgnoreCase) ||
                   deviceName.Contains("gpu", StringComparison.OrdinalIgnoreCase);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                lock (_lockObject)
                {
                    _disposed = true;

                    try
                    {
                        _cancellationTokenSource.Cancel();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error canceling thermal service operations", ex);
                    }

                    try
                    {
                        _monitoringTimer?.Dispose();
                        _monitoringTimer = null;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error disposing thermal service timer", ex);
                    }

                    try
                    {
                        _monitoringSemaphore.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error disposing thermal service monitoring semaphore", ex);
                    }

                    try
                    {
                        _discoveryLock.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error disposing thermal service discovery lock", ex);
                    }

                    try
                    {
                        _cancellationTokenSource.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error disposing thermal service cancellation token", ex);
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}