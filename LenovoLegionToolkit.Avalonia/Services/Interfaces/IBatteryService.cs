using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;

namespace LenovoLegionToolkit.Avalonia.Services.Interfaces
{
    public interface IBatteryService
    {
        Task<BatteryInfo?> GetBatteryInfoAsync();
        Task<BatteryMode> GetBatteryModeAsync();
        Task<bool> SetConservationModeAsync(bool enabled);
        Task<bool> SetRapidChargeModeAsync(bool enabled);
        Task<bool> SetRapidChargeAsync(bool enabled); // Alias for SetRapidChargeModeAsync
        Task<bool> SetNightChargeModeAsync(bool enabled);
        Task<bool> SetChargeThresholdAsync(int startThreshold, int stopThreshold);
        Task<bool> SetChargingThresholdAsync(int threshold); // Set only stop threshold
        Task<int> GetChargingThresholdAsync(); // Get current charge threshold
        Task<bool> GetConservationModeAsync(); // Get conservation mode status
        Task<bool> GetRapidChargeAsync(); // Get rapid charge status
        Task<bool> SetChargeLimitAsync(int chargeLimit);
        Task<bool> IsConservationModeAvailableAsync();
        Task<bool> IsRapidChargeModeAvailableAsync();
        Task<bool> IsNightChargeModeAvailableAsync();
        event EventHandler<BatteryInfo>? BatteryInfoChanged;
        event EventHandler<BatteryMode>? BatteryModeChanged;
    }
}