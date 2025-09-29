using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;

namespace LenovoLegionToolkit.Avalonia.Services.Interfaces
{
    public interface IThermalService
    {
        Task<ThermalInfo?> GetThermalInfoAsync();
        Task<double> GetCpuTemperatureAsync();
        Task<double> GetGpuTemperatureAsync();
        Task<List<FanInfo>> GetFansInfoAsync();
        Task<bool> SetFanSpeedAsync(int fanId, int speed);
        Task<bool> SetFanModeAsync(int fanId, FanMode mode);
        Task<bool> SetFanModeAsync(FanMode mode); // Overload for all fans
        Task<bool> SetFanProfileAsync(int fanId, FanProfile profile);
        Task<List<ThermalZone>> GetThermalZonesAsync();
        Task<bool> StartMonitoringAsync(TimeSpan interval);
        Task StopMonitoringAsync();
        Task<bool> SetFanControlAsync(bool automatic);
        Task<bool> GetFanControlStateAsync();
        event EventHandler<ThermalInfo>? ThermalInfoUpdated;
    }

    public enum FanMode
    {
        Auto,
        Manual,
        Custom
    }
}