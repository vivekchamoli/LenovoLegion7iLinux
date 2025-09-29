using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.Services.Linux
{
    public class LinuxPowerModeService : IPowerModeService
    {
        private const string POWER_MODE_PATH = "/sys/kernel/legion_laptop/power_mode";
        private const string CUSTOM_MODE_PATH = "/sys/kernel/legion_laptop/custom_mode";
        private readonly IHardwareService _hardwareService;
        private readonly IFileSystemService _fileSystem;
        private readonly IProcessRunner _processRunner;

        public event EventHandler<PowerMode>? PowerModeChanged;

        public LinuxPowerModeService(IHardwareService hardwareService) : this(hardwareService, new FileSystemService(), new ProcessRunner())
        {
        }

        public LinuxPowerModeService(IHardwareService hardwareService, IFileSystemService fileSystem, IProcessRunner processRunner)
        {
            _hardwareService = hardwareService ?? throw new ArgumentNullException(nameof(hardwareService));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        }

        public async Task<PowerMode> GetCurrentPowerModeAsync()
        {
            try
            {
                var value = (await _fileSystem.ReadFileAsync(POWER_MODE_PATH)).Trim();
                if (!string.IsNullOrEmpty(value))
                {
                    return PowerModeExtensions.FromSysfsValue(value);
                }

                // Fallback: Try reading platform profile
                var profilePath = "/sys/firmware/acpi/platform_profile";
                if (_fileSystem.FileExists(profilePath))
                {
                    value = (await _fileSystem.ReadFileAsync(profilePath)).Trim();
                    return value?.ToLower() switch
                    {
                        "quiet" or "low-power" => PowerMode.Quiet,
                        "balanced" => PowerMode.Balanced,
                        "performance" => PowerMode.Performance,
                        _ => PowerMode.Balanced
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get current power mode", ex);
            }

            return PowerMode.Balanced;
        }

        public async Task<PowerMode> GetCurrentModeAsync()
        {
            // Alias for GetCurrentPowerModeAsync
            return await GetCurrentPowerModeAsync();
        }

        public async Task<bool> SetPowerModeAsync(PowerMode mode)
        {
            try
            {
                Logger.Info($"Setting power mode to: {mode}");

                // Try Legion-specific interface first
                if (_fileSystem.FileExists(POWER_MODE_PATH))
                {
                    // Legion laptop path expects numeric values: 0=Quiet, 1=Balanced, 2=Performance
                    var value = mode switch
                    {
                        PowerMode.Quiet => "0",
                        PowerMode.Balanced => "1",
                        PowerMode.Performance => "2",
                        PowerMode.Custom => "255",
                        _ => "1"
                    };
                    await _fileSystem.WriteFileAsync(POWER_MODE_PATH, value);
                    var success = true;

                    if (success)
                    {
                        PowerModeChanged?.Invoke(this, mode);
                        Logger.Info($"Power mode changed to: {mode}");
                        return true;
                    }
                }

                // Fallback to platform profile
                var profilePath = "/sys/firmware/acpi/platform_profile";
                if (_fileSystem.FileExists(profilePath))
                {
                    var profileValue = mode switch
                    {
                        PowerMode.Quiet => "quiet",
                        PowerMode.Balanced => "balanced",
                        PowerMode.Performance => "performance",
                        _ => "balanced"
                    };

                    await _fileSystem.WriteFileAsync(profilePath, profileValue);
                    var success = true;

                    if (success)
                    {
                        PowerModeChanged?.Invoke(this, mode);
                        Logger.Info($"Power mode changed via platform profile to: {mode}");
                        return true;
                    }
                }

                Logger.Warning($"Failed to set power mode: {mode}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting power mode to {mode}", ex);
                return false;
            }
        }

        public async Task<List<PowerMode>> GetAvailablePowerModesAsync()
        {
            var modes = new List<PowerMode>();

            try
            {
                // Check Legion-specific modes
                if (_fileSystem.FileExists(POWER_MODE_PATH))
                {
                    modes.AddRange(new[] {
                        PowerMode.Quiet,
                        PowerMode.Balanced,
                        PowerMode.Performance
                    });

                    // Check for custom mode support
                    if (_fileSystem.FileExists(CUSTOM_MODE_PATH))
                    {
                        modes.Add(PowerMode.Custom);
                    }
                }
                else
                {
                    // Fallback: Check platform profile
                    var profilesPath = "/sys/firmware/acpi/platform_profile_choices";
                    if (_fileSystem.FileExists(profilesPath))
                    {
                        var profiles = (await _fileSystem.ReadFileAsync(profilesPath)).Trim();
                        if (!string.IsNullOrEmpty(profiles))
                        {
                            if (profiles.Contains("quiet") || profiles.Contains("low-power"))
                                modes.Add(PowerMode.Quiet);
                            if (profiles.Contains("balanced"))
                                modes.Add(PowerMode.Balanced);
                            if (profiles.Contains("performance"))
                                modes.Add(PowerMode.Performance);
                        }
                    }
                }

                // If no modes detected, provide defaults
                if (!modes.Any())
                {
                    modes.AddRange(new[] {
                        PowerMode.Quiet,
                        PowerMode.Balanced,
                        PowerMode.Performance
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get available power modes", ex);
                // Return default modes on error
                modes.AddRange(new[] {
                    PowerMode.Quiet,
                    PowerMode.Balanced,
                    PowerMode.Performance
                });
            }

            return modes.Distinct().OrderBy(m => (int)m).ToList();
        }

        public async Task<bool> IsPowerModeAvailableAsync(PowerMode mode)
        {
            var available = await GetAvailablePowerModesAsync();
            return available.Contains(mode);
        }

        public async Task<CustomPowerMode?> GetCustomPowerModeSettingsAsync()
        {
            if (!_fileSystem.FileExists(CUSTOM_MODE_PATH))
                return null;

            try
            {
                var settings = new CustomPowerMode
                {
                    CpuLongTermPowerLimit = 45,
                    CpuShortTermPowerLimit = 65,
                    GpuPowerLimit = 80,
                    GpuTemperatureLimit = 87
                };

                // Read custom mode parameters from sysfs
                var cpuPl1Path = "/sys/kernel/legion_laptop/cpu_pl1";
                if (_fileSystem.FileExists(cpuPl1Path))
                {
                    var value = (await _fileSystem.ReadFileAsync(cpuPl1Path)).Trim();
                    if (int.TryParse(value, out var pl1))
                        settings.CpuLongTermPowerLimit = pl1;
                }

                var cpuPl2Path = "/sys/kernel/legion_laptop/cpu_pl2";
                if (_fileSystem.FileExists(cpuPl2Path))
                {
                    var value = (await _fileSystem.ReadFileAsync(cpuPl2Path)).Trim();
                    if (int.TryParse(value, out var pl2))
                        settings.CpuShortTermPowerLimit = pl2;
                }

                var gpuTgpPath = "/sys/kernel/legion_laptop/gpu_tgp";
                if (_fileSystem.FileExists(gpuTgpPath))
                {
                    var value = (await _fileSystem.ReadFileAsync(gpuTgpPath)).Trim();
                    if (int.TryParse(value, out var tgp))
                        settings.GpuPowerLimit = tgp;
                }

                return settings;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get custom power mode settings", ex);
                return null;
            }
        }

        public async Task<bool> SetCustomPowerModeSettingsAsync(CustomPowerMode settings)
        {
            if (!_fileSystem.FileExists(CUSTOM_MODE_PATH))
                return false;

            try
            {
                var success = true;

                // Write CPU PL1
                var cpuPl1Path = "/sys/kernel/legion_laptop/cpu_pl1";
                if (_fileSystem.FileExists(cpuPl1Path))
                {
                    await _fileSystem.WriteFileAsync(cpuPl1Path,
                        settings.CpuLongTermPowerLimit.ToString());
                }

                // Write CPU PL2
                var cpuPl2Path = "/sys/kernel/legion_laptop/cpu_pl2";
                if (_fileSystem.FileExists(cpuPl2Path))
                {
                    await _fileSystem.WriteFileAsync(cpuPl2Path,
                        settings.CpuShortTermPowerLimit.ToString());
                }

                // Write GPU TGP
                var gpuTgpPath = "/sys/kernel/legion_laptop/gpu_tgp";
                if (_fileSystem.FileExists(gpuTgpPath))
                {
                    await _fileSystem.WriteFileAsync(gpuTgpPath,
                        settings.GpuPowerLimit.ToString());
                }

                if (success)
                {
                    Logger.Info("Custom power mode settings applied");
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to set custom power mode settings", ex);
                return false;
            }
        }
    }
}