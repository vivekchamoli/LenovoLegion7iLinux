using System.Collections.Generic;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;

namespace LenovoLegionToolkit.Avalonia.Services.Interfaces
{
    public interface IHardwareService
    {
        Task<HardwareInfo> GetHardwareInfoAsync();
        Task<DeviceCapabilities> DetectCapabilitiesAsync();
        Task<bool> IsFeatureSupportedAsync(HardwareFeature feature);
        Task<string?> GetBiosVersionAsync();
        Task<string?> GetSerialNumberAsync();
        Task<bool> CheckKernelModuleAsync();
        Task<bool> LoadKernelModuleAsync();
        Task<Dictionary<string, string>> GetSystemInfoAsync();
    }
}