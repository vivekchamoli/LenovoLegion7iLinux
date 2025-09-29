using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;

namespace LenovoLegionToolkit.Avalonia.Services.Interfaces
{
    public interface IGpuService
    {
        Task<List<GpuInfo>> GetGpuInfoAsync();
        Task<HybridModeState> GetHybridModeAsync();
        Task<bool> SetHybridModeAsync(HybridModeState mode);
        Task<bool> IsHybridModeSupportedAsync();
        Task<bool> IsDiscreteGpuActiveAsync();
        Task<bool> PowerOffDiscreteGpuAsync();
        Task<bool> PowerOnDiscreteGpuAsync();
        Task<GpuInfo?> GetPrimaryGpuAsync();
        Task<GpuInfo?> GetDiscreteGpuAsync();
        IObservable<GpuInfo> GpuStateChanged { get; }
    }
}