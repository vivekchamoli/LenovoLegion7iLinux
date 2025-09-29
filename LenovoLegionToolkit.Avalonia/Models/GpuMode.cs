namespace LenovoLegionToolkit.Avalonia.Models
{
    public enum GpuVendor
    {
        Unknown = 0,
        Intel = 1,
        NVIDIA = 2,
        AMD = 3
    }

    public enum GpuType
    {
        Integrated = 0,
        Discrete = 1
    }

    public enum HybridModeState
    {
        On = 0,           // Hybrid mode enabled (both GPUs available)
        OnIGPUOnly = 1,   // Force integrated GPU only
        OnAuto = 2,       // Automatic switching
        Off = 3           // Discrete GPU only (no hybrid)
    }

    public enum GpuPowerState
    {
        Active = 0,
        Suspended = 1,
        PoweredOff = 2,
        Unknown = 3
    }
}