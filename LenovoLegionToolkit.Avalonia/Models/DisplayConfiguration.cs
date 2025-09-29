using System;
using System.Collections.Generic;

namespace LenovoLegionToolkit.Avalonia.Models
{
    public class DisplayInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public bool IsInternal { get; set; }
        public bool IsConnected { get; set; }
        public DisplayTechnology Technology { get; set; }
        public DisplayCapabilities Capabilities { get; set; } = new();
        public DisplayConfiguration CurrentConfiguration { get; set; } = new();

        // Convenience properties
        public int CurrentRefreshRate => CurrentConfiguration?.RefreshRate ?? 0;
        public bool HdrEnabled => CurrentConfiguration?.HdrEnabled ?? false;
        public int Brightness => CurrentConfiguration?.Brightness ?? 0;
    }

    public class DisplayConfiguration
    {
        public string DisplayId { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public int RefreshRate { get; set; }
        public int BitDepth { get; set; }
        public bool HdrEnabled { get; set; }
        public int Brightness { get; set; }
        public ColorProfile ColorProfile { get; set; }
        public DisplayOrientation Orientation { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public double Scale { get; set; } = 1.0;
        public bool VrrEnabled { get; set; } // Variable Refresh Rate
        public int Overscan { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class DisplayCapabilities
    {
        public List<Resolution> SupportedResolutions { get; set; } = new();
        public List<int> SupportedRefreshRates { get; set; } = new();
        public int MaxRefreshRate { get; set; }
        public int MinRefreshRate { get; set; }
        public bool SupportsHdr { get; set; }
        public bool SupportsHdr10 { get; set; }
        public bool SupportsDolbyVision { get; set; }
        public bool SupportsFreesync { get; set; }
        public bool SupportsGsync { get; set; }
        public bool SupportsVrr { get; set; }
        public bool SupportsDdc { get; set; }
        public int MaxBrightness { get; set; } = 100;
        public List<ColorProfile> SupportedColorProfiles { get; set; } = new();
        public int NativeWidth { get; set; }
        public int NativeHeight { get; set; }
        public int NativeRefreshRate { get; set; }
        public double PhysicalWidth { get; set; } // in mm
        public double PhysicalHeight { get; set; } // in mm
    }

    public class Resolution
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<int> RefreshRates { get; set; } = new();

        public string DisplayName => $"{Width}x{Height}";
        public double AspectRatio => Math.Round((double)Width / Height, 2);

        public override string ToString() => DisplayName;
    }

    public class ColorProfile
    {
        public string Name { get; set; } = string.Empty;
        public ColorGamut Gamut { get; set; }
        public double Gamma { get; set; } = 2.2;
        public int WhitePoint { get; set; } = 6500; // Kelvin
        public double RedX { get; set; }
        public double RedY { get; set; }
        public double GreenX { get; set; }
        public double GreenY { get; set; }
        public double BlueX { get; set; }
        public double BlueY { get; set; }
        public double WhiteX { get; set; }
        public double WhiteY { get; set; }
        public bool IsCustom { get; set; }
    }

    public class NightLightSettings
    {
        public bool Enabled { get; set; }
        public int Temperature { get; set; } = 4500; // Kelvin
        public bool UseSchedule { get; set; }
        public TimeSpan ScheduleStartTime { get; set; }
        public TimeSpan ScheduleEndTime { get; set; }
        public bool UseSunsetToSunrise { get; set; }
        public int TransitionDuration { get; set; } = 60; // seconds
    }

    public class DisplayPreset
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, DisplayConfiguration> DisplayConfigurations { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool IsDefault { get; set; }
        public string? TriggerApp { get; set; }
        public PowerMode? TriggerPowerMode { get; set; }
    }

    public enum DisplayTechnology
    {
        Unknown,
        LCD,
        LED,
        OLED,
        MiniLED,
        QLED,
        IPS,
        VA,
        TN
    }

    public enum DisplayOrientation
    {
        Landscape = 0,
        Portrait = 90,
        LandscapeFlipped = 180,
        PortraitFlipped = 270
    }

    public enum ColorGamut
    {
        sRGB,
        AdobeRGB,
        DisplayP3,
        Rec709,
        Rec2020,
        DCIP3,
        Custom
    }
}