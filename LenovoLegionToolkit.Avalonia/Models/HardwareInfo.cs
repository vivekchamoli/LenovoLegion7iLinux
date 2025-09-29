using System;
using System.Collections.Generic;

namespace LenovoLegionToolkit.Avalonia.Models
{
    public class HardwareInfo
    {
        public string Model { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = "Lenovo";
        public string BiosVersion { get; set; } = string.Empty;
        public string BiosDate { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Generation { get; set; } = string.Empty;
        public string CpuModel { get; set; } = string.Empty;
        public string GpuModel { get; set; } = string.Empty;
        public int RamSizeGB { get; set; }
        public string KernelVersion { get; set; } = string.Empty;
        public string Distribution { get; set; } = string.Empty;
        public bool HasLegionKernelModule { get; set; }
        public List<HardwareCapability> Capabilities { get; set; } = new();
    }

    public class HardwareCapability
    {
        public string Name { get; set; } = string.Empty;
        public bool IsSupported { get; set; }
        public string? RequiredModule { get; set; }
        public string? SysfsPath { get; set; }
        public string? Description { get; set; }
    }

    public enum HardwareFeature
    {
        PowerModeControl,
        BatteryConservation,
        RapidCharge,
        ThermalMonitoring,
        FanControl,
        KeyboardBacklight,
        RgbKeyboard,
        HybridMode,
        OverDrive,
        DiscreteGpuControl,
        PanelOverdrive,
        DisplayRefreshRate,
        TouchpadLock,
        FnLock,
        WinKeyDisable,
        CameraPrivacy,
        MicrophoneMute
    }

    public class DeviceCapabilities
    {
        public Dictionary<HardwareFeature, bool> Features { get; set; } = new();
        public List<PowerMode> SupportedPowerModes { get; set; } = new();
        public int MaxFanSpeed { get; set; }
        public bool HasBattery { get; set; }
        public bool HasDiscreteGpu { get; set; }
        public bool HasKeyboardBacklight { get; set; }
        public KeyboardBacklightType KeyboardType { get; set; }
    }

    public enum KeyboardBacklightType
    {
        None,
        White,
        WhiteMultiLevel,
        RGB4Zone,
        RGBPerKey
    }
}