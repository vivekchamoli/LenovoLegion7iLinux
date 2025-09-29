using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.Services.Linux
{
    public class LinuxBatteryService : IBatteryService
    {
        private const string BATTERY_PATH = "/sys/class/power_supply/BAT0";
        private const string AC_PATH = "/sys/class/power_supply/AC0";
        private const string CONSERVATION_MODE_PATH = "/sys/bus/platform/drivers/ideapad_acpi/VPC2004:00/conservation_mode";
        private const string RAPID_CHARGE_PATH = "/sys/kernel/legion_laptop/rapid_charge";
        private const string CHARGE_THRESHOLD_PATH = "/sys/class/power_supply/BAT0";
        private readonly System.Timers.Timer _updateTimer;
        private readonly IFileSystemService _fileSystem;
        private readonly IProcessRunner _processRunner;

        public event EventHandler<BatteryInfo>? BatteryInfoChanged;
        public event EventHandler<BatteryMode>? BatteryModeChanged;

        public LinuxBatteryService() : this(new FileSystemService(), new ProcessRunner())
        {
        }

        public LinuxBatteryService(IFileSystemService fileSystem, IProcessRunner processRunner)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
            _updateTimer = new System.Timers.Timer(5000); // Update every 5 seconds
            _updateTimer.Elapsed += async (s, e) => await UpdateBatteryInfoAsync();
            _updateTimer.Start();
        }

        public async Task<BatteryInfo?> GetBatteryInfoAsync()
        {
            try
            {
                if (!_fileSystem.DirectoryExists(BATTERY_PATH))
                {
                    Logger.Warning("No battery detected");
                    return null;
                }

                var info = new BatteryInfo();

                // Read charge level
                var capacityPath = _fileSystem.CombinePath(BATTERY_PATH, "capacity");
                if (_fileSystem.FileExists(capacityPath))
                {
                    var value = (await _fileSystem.ReadFileAsync(capacityPath)).Trim();
                    if (int.TryParse(value, out var capacity))
                        info.ChargeLevel = capacity;
                }

                // Read status
                var statusPath = _fileSystem.CombinePath(BATTERY_PATH, "status");
                if (_fileSystem.FileExists(statusPath))
                {
                    var status = (await _fileSystem.ReadFileAsync(statusPath)).Trim();
                    info.IsCharging = status?.ToLower() == "charging";
                }

                // Read AC adapter status
                if (_fileSystem.DirectoryExists(AC_PATH))
                {
                    var acOnlinePath = _fileSystem.CombinePath(AC_PATH, "online");
                    if (_fileSystem.FileExists(acOnlinePath))
                    {
                        var value = (await _fileSystem.ReadFileAsync(acOnlinePath)).Trim();
                        info.IsACConnected = value == "1";
                    }
                }

                // Read voltage
                var voltagePath = _fileSystem.CombinePath(BATTERY_PATH, "voltage_now");
                if (_fileSystem.FileExists(voltagePath))
                {
                    var value = (await _fileSystem.ReadFileAsync(voltagePath)).Trim();
                    if (long.TryParse(value, out var voltage))
                        info.Voltage = voltage / 1000000.0; // Convert μV to V
                }

                // Read current
                var currentPath = _fileSystem.CombinePath(BATTERY_PATH, "current_now");
                if (_fileSystem.FileExists(currentPath))
                {
                    var value = (await _fileSystem.ReadFileAsync(currentPath)).Trim();
                    if (long.TryParse(value, out var current))
                        info.Current = Math.Abs(current) / 1000000.0; // Convert μA to A
                }

                // Calculate power
                info.Power = info.Voltage * info.Current;

                // Read design capacity
                var designCapPath = _fileSystem.CombinePath(BATTERY_PATH, "charge_full_design");
                if (_fileSystem.FileExists(designCapPath))
                {
                    var value = (await _fileSystem.ReadFileAsync(designCapPath)).Trim();
                    if (int.TryParse(value, out var designCap))
                        info.DesignCapacity = designCap / 1000; // Convert μAh to mAh
                }

                // Read full charge capacity
                var fullCapPath = _fileSystem.CombinePath(BATTERY_PATH, "charge_full");
                if (_fileSystem.FileExists(fullCapPath))
                {
                    var value = (await _fileSystem.ReadFileAsync(fullCapPath)).Trim();
                    if (int.TryParse(value, out var fullCap))
                        info.FullChargeCapacity = fullCap / 1000; // Convert μAh to mAh
                }

                // Read current capacity
                var nowCapPath = _fileSystem.CombinePath(BATTERY_PATH, "charge_now");
                if (_fileSystem.FileExists(nowCapPath))
                {
                    var value = (await _fileSystem.ReadFileAsync(nowCapPath)).Trim();
                    if (int.TryParse(value, out var nowCap))
                        info.RemainingCapacity = nowCap / 1000; // Convert μAh to mAh
                }

                // Read cycle count
                var cyclePath = _fileSystem.CombinePath(BATTERY_PATH, "cycle_count");
                if (_fileSystem.FileExists(cyclePath))
                {
                    var value = (await _fileSystem.ReadFileAsync(cyclePath)).Trim();
                    if (int.TryParse(value, out var cycles))
                        info.CycleCount = cycles;
                }

                // Calculate time remaining
                if (info.Current > 0)
                {
                    if (info.IsCharging)
                    {
                        var hoursToFull = (info.FullChargeCapacity - info.RemainingCapacity) / (info.Current * 1000);
                        info.TimeToFull = TimeSpan.FromHours(hoursToFull);
                    }
                    else
                    {
                        var hoursToEmpty = info.RemainingCapacity / (info.Current * 1000);
                        info.TimeToEmpty = TimeSpan.FromHours(hoursToEmpty);
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get battery info", ex);
                return null;
            }
        }

        public async Task<BatteryMode> GetBatteryModeAsync()
        {
            var mode = new BatteryMode();

            try
            {
                // Read conservation mode
                if (_fileSystem.FileExists(CONSERVATION_MODE_PATH))
                {
                    var value = (await _fileSystem.ReadFileAsync(CONSERVATION_MODE_PATH)).Trim();
                    mode.ConservationMode = value == "1";
                }

                // Read rapid charge mode
                if (_fileSystem.FileExists(RAPID_CHARGE_PATH))
                {
                    var value = (await _fileSystem.ReadFileAsync(RAPID_CHARGE_PATH)).Trim();
                    mode.RapidChargeMode = value == "1";
                }

                // Read charge thresholds
                var startThresholdPath = "/sys/class/power_supply/BAT0/charge_control_start_threshold";
                if (_fileSystem.FileExists(startThresholdPath))
                {
                    var value = (await _fileSystem.ReadFileAsync(startThresholdPath)).Trim();
                    if (int.TryParse(value, out var threshold))
                        mode.ChargeThreshold = threshold;
                }

                var endThresholdPath = "/sys/class/power_supply/BAT0/charge_control_end_threshold";
                if (_fileSystem.FileExists(endThresholdPath))
                {
                    var value = (await _fileSystem.ReadFileAsync(endThresholdPath)).Trim();
                    if (int.TryParse(value, out var threshold))
                        mode.ChargeStopThreshold = threshold;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get battery mode", ex);
            }

            return mode;
        }

        public async Task<bool> SetConservationModeAsync(bool enabled)
        {
            try
            {
                if (!_fileSystem.FileExists(CONSERVATION_MODE_PATH))
                {
                    Logger.Warning("Conservation mode not available");
                    return false;
                }

                var value = enabled ? "1" : "0";
                await _fileSystem.WriteFileAsync(CONSERVATION_MODE_PATH, value);
                var success = true;

                if (success)
                {
                    Logger.Info($"Conservation mode set to: {enabled}");
                    var mode = await GetBatteryModeAsync();
                    BatteryModeChanged?.Invoke(this, mode);
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
            try
            {
                if (!_fileSystem.FileExists(RAPID_CHARGE_PATH))
                {
                    Logger.Warning("Rapid charge mode not available");
                    return false;
                }

                var value = enabled ? "1" : "0";
                await _fileSystem.WriteFileAsync(RAPID_CHARGE_PATH, value);
                var success = true;

                if (success)
                {
                    Logger.Info($"Rapid charge mode set to: {enabled}");
                    var mode = await GetBatteryModeAsync();
                    BatteryModeChanged?.Invoke(this, mode);
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
            // Alias for SetRapidChargeModeAsync
            return await SetRapidChargeModeAsync(enabled);
        }

        public async Task<bool> SetNightChargeModeAsync(bool enabled)
        {
            // Night charge mode might not be available on all models
            Logger.Warning("Night charge mode not implemented for Linux");
            return await Task.FromResult(false);
        }

        public async Task<bool> SetChargeThresholdAsync(int startThreshold, int stopThreshold)
        {
            try
            {
                var startPath = "/sys/class/power_supply/BAT0/charge_control_start_threshold";
                var endPath = "/sys/class/power_supply/BAT0/charge_control_end_threshold";

                if (!_fileSystem.FileExists(startPath) || !_fileSystem.FileExists(endPath))
                {
                    Logger.Warning("Charge threshold control not available");
                    return false;
                }

                await _fileSystem.WriteFileAsync(startPath, startThreshold.ToString());
                await _fileSystem.WriteFileAsync(endPath, stopThreshold.ToString());
                var success = true;

                if (success)
                {
                    Logger.Info($"Charge thresholds set to: {startThreshold}-{stopThreshold}");
                    var mode = await GetBatteryModeAsync();
                    BatteryModeChanged?.Invoke(this, mode);
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set charge thresholds", ex);
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
            try
            {
                var endPath = Path.Combine(CHARGE_THRESHOLD_PATH, "charge_stop_threshold");
                if (_fileSystem.FileExists(endPath))
                {
                    var value = (await _fileSystem.ReadFileAsync(endPath)).Trim();
                    if (int.TryParse(value, out var threshold))
                    {
                        return threshold;
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
            try
            {
                if (_fileSystem.FileExists(CONSERVATION_MODE_PATH))
                {
                    var value = (await _fileSystem.ReadFileAsync(CONSERVATION_MODE_PATH)).Trim();
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
            try
            {
                if (_fileSystem.FileExists(RAPID_CHARGE_PATH))
                {
                    var value = (await _fileSystem.ReadFileAsync(RAPID_CHARGE_PATH)).Trim();
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
            // For simple charge limit, set both start and stop to the same value
            // or set a reasonable range around the target limit
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
            // Not typically available on Linux
            return await Task.FromResult(false);
        }

        private async Task UpdateBatteryInfoAsync()
        {
            var info = await GetBatteryInfoAsync();
            if (info != null)
            {
                BatteryInfoChanged?.Invoke(this, info);
            }
        }
    }
}