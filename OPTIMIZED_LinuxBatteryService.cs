using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.Services.Linux
{
    /// <summary>
    /// OPTIMIZED Linux Battery Service with proper resource management and async patterns
    /// Fixes: Memory leaks, async void patterns, proper disposal, thread safety
    /// </summary>
    public class LinuxBatteryService : IBatteryService, IDisposable
    {
        private const string BATTERY_PATH = "/sys/class/power_supply/BAT0";
        private const string AC_PATH = "/sys/class/power_supply/AC0";
        private const string CONSERVATION_MODE_PATH = "/sys/bus/platform/drivers/ideapad_acpi/VPC2004:00/conservation_mode";
        private const string RAPID_CHARGE_PATH = "/sys/kernel/legion_laptop/rapid_charge";
        private const string CHARGE_THRESHOLD_PATH = "/sys/class/power_supply/BAT0";

        private readonly IFileSystemService _fileSystem;
        private readonly IProcessRunner _processRunner;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly SemaphoreSlim _updateSemaphore;

        private Timer? _updateTimer;
        private bool _disposed;
        private readonly object _lockObject = new object();

        public event EventHandler<BatteryInfo>? BatteryInfoChanged;
        public event EventHandler<BatteryMode>? BatteryModeChanged;

        public LinuxBatteryService() : this(new FileSystemService(), new ProcessRunner())
        {
        }

        public LinuxBatteryService(IFileSystemService fileSystem, IProcessRunner processRunner)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
            _cancellationTokenSource = new CancellationTokenSource();
            _updateSemaphore = new SemaphoreSlim(1, 1);

            // Start adaptive polling timer with proper async handling
            StartMonitoring();
        }

        private void StartMonitoring()
        {
            lock (_lockObject)
            {
                if (_disposed) return;

                // Use Timer instead of System.Timers.Timer for better resource management
                _updateTimer = new Timer(OnTimerElapsedAsync, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            }
        }

        private async void OnTimerElapsedAsync(object? state)
        {
            // This is the only acceptable async void pattern (event handler/timer callback)
            // with proper exception handling to prevent crashes
            try
            {
                if (_disposed || _cancellationTokenSource.Token.IsCancellationRequested)
                    return;

                await UpdateBatteryInfoSafeAsync();
            }
            catch (Exception ex)
            {
                // Critical: Catch all exceptions to prevent application crash
                Logger.Error("Battery monitoring timer error", ex);
            }
        }

        private async Task UpdateBatteryInfoSafeAsync()
        {
            // Use semaphore to prevent overlapping updates
            if (!await _updateSemaphore.WaitAsync(100, _cancellationTokenSource.Token))
                return;

            try
            {
                var info = await GetBatteryInfoAsync();
                if (info != null && !_disposed)
                {
                    BatteryInfoChanged?.Invoke(this, info);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to update battery info", ex);
            }
            finally
            {
                _updateSemaphore.Release();
            }
        }

        public async Task<BatteryInfo?> GetBatteryInfoAsync()
        {
            if (_disposed)
                return null;

            try
            {
                if (!_fileSystem.DirectoryExists(BATTERY_PATH))
                {
                    Logger.Warning("No battery detected");
                    return null;
                }

                var info = new BatteryInfo();
                var token = _cancellationTokenSource.Token;

                // Parallel reading for better performance
                var tasks = new[]
                {
                    ReadBatteryCapacityAsync(info, token),
                    ReadBatteryStatusAsync(info, token),
                    ReadACStatusAsync(info, token),
                    ReadBatteryVoltageAsync(info, token),
                    ReadBatteryCurrentAsync(info, token),
                    ReadBatteryCapacitiesAsync(info, token),
                    ReadBatteryCycleCountAsync(info, token)
                };

                await Task.WhenAll(tasks);

                // Calculate derived values
                info.Power = info.Voltage * info.Current;
                CalculateTimeRemaining(info);

                return info;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get battery info", ex);
                return null;
            }
        }

        private async Task ReadBatteryCapacityAsync(BatteryInfo info, CancellationToken cancellationToken)
        {
            var capacityPath = _fileSystem.CombinePath(BATTERY_PATH, "capacity");
            if (_fileSystem.FileExists(capacityPath))
            {
                var value = (await _fileSystem.ReadFileAsync(capacityPath, cancellationToken)).Trim();
                if (int.TryParse(value, out var capacity))
                    info.ChargeLevel = Math.Clamp(capacity, 0, 100);
            }
        }

        private async Task ReadBatteryStatusAsync(BatteryInfo info, CancellationToken cancellationToken)
        {
            var statusPath = _fileSystem.CombinePath(BATTERY_PATH, "status");
            if (_fileSystem.FileExists(statusPath))
            {
                var status = (await _fileSystem.ReadFileAsync(statusPath, cancellationToken)).Trim();
                info.IsCharging = string.Equals(status, "charging", StringComparison.OrdinalIgnoreCase);
            }
        }

        private async Task ReadACStatusAsync(BatteryInfo info, CancellationToken cancellationToken)
        {
            if (_fileSystem.DirectoryExists(AC_PATH))
            {
                var acOnlinePath = _fileSystem.CombinePath(AC_PATH, "online");
                if (_fileSystem.FileExists(acOnlinePath))
                {
                    var value = (await _fileSystem.ReadFileAsync(acOnlinePath, cancellationToken)).Trim();
                    info.IsACConnected = value == "1";
                }
            }
        }

        private async Task ReadBatteryVoltageAsync(BatteryInfo info, CancellationToken cancellationToken)
        {
            var voltagePath = _fileSystem.CombinePath(BATTERY_PATH, "voltage_now");
            if (_fileSystem.FileExists(voltagePath))
            {
                var value = (await _fileSystem.ReadFileAsync(voltagePath, cancellationToken)).Trim();
                if (long.TryParse(value, out var voltage))
                    info.Voltage = voltage / 1000000.0; // Convert μV to V
            }
        }

        private async Task ReadBatteryCurrentAsync(BatteryInfo info, CancellationToken cancellationToken)
        {
            var currentPath = _fileSystem.CombinePath(BATTERY_PATH, "current_now");
            if (_fileSystem.FileExists(currentPath))
            {
                var value = (await _fileSystem.ReadFileAsync(currentPath, cancellationToken)).Trim();
                if (long.TryParse(value, out var current))
                    info.Current = Math.Abs(current) / 1000000.0; // Convert μA to A
            }
        }

        private async Task ReadBatteryCapacitiesAsync(BatteryInfo info, CancellationToken cancellationToken)
        {
            // Read design capacity
            var designCapPath = _fileSystem.CombinePath(BATTERY_PATH, "charge_full_design");
            if (_fileSystem.FileExists(designCapPath))
            {
                var value = (await _fileSystem.ReadFileAsync(designCapPath, cancellationToken)).Trim();
                if (int.TryParse(value, out var designCap))
                    info.DesignCapacity = designCap / 1000; // Convert μAh to mAh
            }

            // Read full charge capacity
            var fullCapPath = _fileSystem.CombinePath(BATTERY_PATH, "charge_full");
            if (_fileSystem.FileExists(fullCapPath))
            {
                var value = (await _fileSystem.ReadFileAsync(fullCapPath, cancellationToken)).Trim();
                if (int.TryParse(value, out var fullCap))
                    info.FullChargeCapacity = fullCap / 1000; // Convert μAh to mAh
            }

            // Read current capacity
            var nowCapPath = _fileSystem.CombinePath(BATTERY_PATH, "charge_now");
            if (_fileSystem.FileExists(nowCapPath))
            {
                var value = (await _fileSystem.ReadFileAsync(nowCapPath, cancellationToken)).Trim();
                if (int.TryParse(value, out var nowCap))
                    info.RemainingCapacity = nowCap / 1000; // Convert μAh to mAh
            }
        }

        private async Task ReadBatteryCycleCountAsync(BatteryInfo info, CancellationToken cancellationToken)
        {
            var cyclePath = _fileSystem.CombinePath(BATTERY_PATH, "cycle_count");
            if (_fileSystem.FileExists(cyclePath))
            {
                var value = (await _fileSystem.ReadFileAsync(cyclePath, cancellationToken)).Trim();
                if (int.TryParse(value, out var cycles))
                    info.CycleCount = Math.Max(cycles, 0);
            }
        }

        private static void CalculateTimeRemaining(BatteryInfo info)
        {
            if (info.Current > 0.01) // Avoid division by very small numbers
            {
                if (info.IsCharging && info.FullChargeCapacity > info.RemainingCapacity)
                {
                    var hoursToFull = (info.FullChargeCapacity - info.RemainingCapacity) / (info.Current * 1000);
                    info.TimeToFull = TimeSpan.FromHours(Math.Clamp(hoursToFull, 0, 24));
                }
                else if (!info.IsCharging && info.RemainingCapacity > 0)
                {
                    var hoursToEmpty = info.RemainingCapacity / (info.Current * 1000);
                    info.TimeToEmpty = TimeSpan.FromHours(Math.Clamp(hoursToEmpty, 0, 24));
                }
            }
        }

        public async Task<BatteryMode> GetBatteryModeAsync()
        {
            if (_disposed)
                return new BatteryMode();

            var mode = new BatteryMode();
            var token = _cancellationTokenSource.Token;

            try
            {
                // Parallel reading for better performance
                var tasks = new[]
                {
                    ReadConservationModeAsync(mode, token),
                    ReadRapidChargeModeAsync(mode, token),
                    ReadChargeThresholdsAsync(mode, token)
                };

                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get battery mode", ex);
            }

            return mode;
        }

        private async Task ReadConservationModeAsync(BatteryMode mode, CancellationToken cancellationToken)
        {
            if (_fileSystem.FileExists(CONSERVATION_MODE_PATH))
            {
                var value = (await _fileSystem.ReadFileAsync(CONSERVATION_MODE_PATH, cancellationToken)).Trim();
                mode.ConservationMode = value == "1";
            }
        }

        private async Task ReadRapidChargeModeAsync(BatteryMode mode, CancellationToken cancellationToken)
        {
            if (_fileSystem.FileExists(RAPID_CHARGE_PATH))
            {
                var value = (await _fileSystem.ReadFileAsync(RAPID_CHARGE_PATH, cancellationToken)).Trim();
                mode.RapidChargeMode = value == "1";
            }
        }

        private async Task ReadChargeThresholdsAsync(BatteryMode mode, CancellationToken cancellationToken)
        {
            var startThresholdPath = "/sys/class/power_supply/BAT0/charge_control_start_threshold";
            if (_fileSystem.FileExists(startThresholdPath))
            {
                var value = (await _fileSystem.ReadFileAsync(startThresholdPath, cancellationToken)).Trim();
                if (int.TryParse(value, out var threshold))
                    mode.ChargeThreshold = Math.Clamp(threshold, 0, 100);
            }

            var endThresholdPath = "/sys/class/power_supply/BAT0/charge_control_end_threshold";
            if (_fileSystem.FileExists(endThresholdPath))
            {
                var value = (await _fileSystem.ReadFileAsync(endThresholdPath, cancellationToken)).Trim();
                if (int.TryParse(value, out var threshold))
                    mode.ChargeStopThreshold = Math.Clamp(threshold, 0, 100);
            }
        }

        public async Task<bool> SetConservationModeAsync(bool enabled)
        {
            if (_disposed) return false;

            try
            {
                if (!_fileSystem.FileExists(CONSERVATION_MODE_PATH))
                {
                    Logger.Warning("Conservation mode not available");
                    return false;
                }

                var value = enabled ? "1" : "0";
                var success = await _fileSystem.WriteFileAsync(CONSERVATION_MODE_PATH, value, _cancellationTokenSource.Token);

                if (success)
                {
                    Logger.Info($"Conservation mode set to: {enabled}");

                    // Trigger event asynchronously without blocking
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var mode = await GetBatteryModeAsync();
                            BatteryModeChanged?.Invoke(this, mode);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to notify battery mode change", ex);
                        }
                    }, _cancellationTokenSource.Token);
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set conservation mode to {enabled}", ex);
                return false;
            }
        }

        public async Task<bool> SetRapidChargeModeAsync(bool enabled)
        {
            if (_disposed) return false;

            try
            {
                if (!_fileSystem.FileExists(RAPID_CHARGE_PATH))
                {
                    Logger.Warning("Rapid charge mode not available");
                    return false;
                }

                var value = enabled ? "1" : "0";
                var success = await _fileSystem.WriteFileAsync(RAPID_CHARGE_PATH, value, _cancellationTokenSource.Token);

                if (success)
                {
                    Logger.Info($"Rapid charge mode set to: {enabled}");

                    // Trigger event asynchronously without blocking
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var mode = await GetBatteryModeAsync();
                            BatteryModeChanged?.Invoke(this, mode);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to notify battery mode change", ex);
                        }
                    }, _cancellationTokenSource.Token);
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set rapid charge mode to {enabled}", ex);
                return false;
            }
        }

        public async Task<bool> SetRapidChargeAsync(bool enabled)
        {
            return await SetRapidChargeModeAsync(enabled);
        }

        public async Task<bool> SetNightChargeModeAsync(bool enabled)
        {
            Logger.Warning("Night charge mode not implemented for Linux");
            return await Task.FromResult(false);
        }

        public async Task<bool> SetChargeThresholdAsync(int startThreshold, int stopThreshold)
        {
            if (_disposed) return false;

            // Validate input parameters
            startThreshold = Math.Clamp(startThreshold, 0, 100);
            stopThreshold = Math.Clamp(stopThreshold, 0, 100);

            if (startThreshold >= stopThreshold)
            {
                Logger.Warning($"Invalid charge thresholds: start ({startThreshold}) must be less than stop ({stopThreshold})");
                return false;
            }

            try
            {
                var startPath = "/sys/class/power_supply/BAT0/charge_control_start_threshold";
                var endPath = "/sys/class/power_supply/BAT0/charge_control_end_threshold";

                if (!_fileSystem.FileExists(startPath) || !_fileSystem.FileExists(endPath))
                {
                    Logger.Warning("Charge threshold control not available");
                    return false;
                }

                var token = _cancellationTokenSource.Token;
                var startTask = _fileSystem.WriteFileAsync(startPath, startThreshold.ToString(), token);
                var endTask = _fileSystem.WriteFileAsync(endPath, stopThreshold.ToString(), token);

                var results = await Task.WhenAll(startTask, endTask);
                var success = results[0] && results[1];

                if (success)
                {
                    Logger.Info($"Charge thresholds set to: {startThreshold}-{stopThreshold}");

                    // Trigger event asynchronously without blocking
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var mode = await GetBatteryModeAsync();
                            BatteryModeChanged?.Invoke(this, mode);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to notify battery mode change", ex);
                        }
                    }, _cancellationTokenSource.Token);
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to set charge thresholds", ex);
                return false;
            }
        }

        public async Task<bool> SetChargingThresholdAsync(int threshold)
        {
            // Set the stop threshold to the specified value
            // Keep start threshold 5% lower
            int startThreshold = Math.Max(threshold - 5, 0);
            return await SetChargeThresholdAsync(startThreshold, threshold);
        }

        public async Task<int> GetChargingThresholdAsync()
        {
            if (_disposed) return 80;

            try
            {
                var endPath = Path.Combine(CHARGE_THRESHOLD_PATH, "charge_stop_threshold");
                if (_fileSystem.FileExists(endPath))
                {
                    var value = (await _fileSystem.ReadFileAsync(endPath, _cancellationTokenSource.Token)).Trim();
                    if (int.TryParse(value, out var threshold))
                    {
                        return Math.Clamp(threshold, 0, 100);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get charging threshold", ex);
            }
            return 80; // Default value
        }

        public async Task<bool> GetConservationModeAsync()
        {
            if (_disposed) return false;

            try
            {
                if (_fileSystem.FileExists(CONSERVATION_MODE_PATH))
                {
                    var value = (await _fileSystem.ReadFileAsync(CONSERVATION_MODE_PATH, _cancellationTokenSource.Token)).Trim();
                    return value == "1";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get conservation mode", ex);
            }
            return false;
        }

        public async Task<bool> GetRapidChargeAsync()
        {
            if (_disposed) return false;

            try
            {
                if (_fileSystem.FileExists(RAPID_CHARGE_PATH))
                {
                    var value = (await _fileSystem.ReadFileAsync(RAPID_CHARGE_PATH, _cancellationTokenSource.Token)).Trim();
                    return value == "1";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get rapid charge mode", ex);
            }
            return false;
        }

        public async Task<bool> SetChargeLimitAsync(int chargeLimit)
        {
            int startThreshold = Math.Max(chargeLimit - 5, 0);
            int stopThreshold = Math.Min(chargeLimit, 100);
            return await SetChargeThresholdAsync(startThreshold, stopThreshold);
        }

        public async Task<bool> IsConservationModeAvailableAsync()
        {
            return await Task.FromResult(_fileSystem.FileExists(CONSERVATION_MODE_PATH));
        }

        public async Task<bool> IsRapidChargeModeAvailableAsync()
        {
            return await Task.FromResult(_fileSystem.FileExists(RAPID_CHARGE_PATH));
        }

        public async Task<bool> IsNightChargeModeAvailableAsync()
        {
            return await Task.FromResult(false);
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
                        Logger.Error("Error canceling battery service operations", ex);
                    }

                    try
                    {
                        _updateTimer?.Dispose();
                        _updateTimer = null;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error disposing battery service timer", ex);
                    }

                    try
                    {
                        _updateSemaphore.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error disposing battery service semaphore", ex);
                    }

                    try
                    {
                        _cancellationTokenSource.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error disposing battery service cancellation token", ex);
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