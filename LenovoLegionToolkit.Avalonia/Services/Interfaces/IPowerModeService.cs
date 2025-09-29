using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;

namespace LenovoLegionToolkit.Avalonia.Services.Interfaces
{
    public interface IPowerModeService : INotifyPowerModeChanged
    {
        Task<PowerMode> GetCurrentPowerModeAsync();
        Task<PowerMode> GetCurrentModeAsync(); // Alias for GetCurrentPowerModeAsync
        Task<bool> SetPowerModeAsync(PowerMode mode);
        Task<List<PowerMode>> GetAvailablePowerModesAsync();
        Task<bool> IsPowerModeAvailableAsync(PowerMode mode);
        Task<CustomPowerMode?> GetCustomPowerModeSettingsAsync();
        Task<bool> SetCustomPowerModeSettingsAsync(CustomPowerMode settings);
        // PowerModeChanged event is inherited from INotifyPowerModeChanged
    }

    public class CustomPowerMode
    {
        public int CpuLongTermPowerLimit { get; set; }
        public int CpuShortTermPowerLimit { get; set; }
        public int GpuPowerLimit { get; set; }
        public int GpuTemperatureLimit { get; set; }
        public FanProfile? CpuFanProfile { get; set; }
        public FanProfile? GpuFanProfile { get; set; }
    }
}