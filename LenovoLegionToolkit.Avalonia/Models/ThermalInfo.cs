using System;
using System.Collections.Generic;

namespace LenovoLegionToolkit.Avalonia.Models
{
    public class ThermalInfo
    {
        public double CpuTemperature { get; set; }
        public double GpuTemperature { get; set; }
        public double SystemTemperature { get; set; }
        public int CpuFanRpm { get; set; }
        public int GpuFanRpm { get; set; }
        public string ThermalMode { get; set; } = "Balanced";
        public List<FanInfo> Fans { get; set; } = new();
        public List<ThermalZone> ThermalZones { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class FanInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CurrentSpeed { get; set; }
        public int MaxSpeed { get; set; }
        public int MinSpeed { get; set; }
        public double SpeedPercentage => MaxSpeed > 0
            ? Math.Round((double)CurrentSpeed / MaxSpeed * 100, 1)
            : 0;
        public bool IsAutomatic { get; set; }
        public FanProfile? CustomProfile { get; set; }
    }

    public class FanProfile
    {
        public string Name { get; set; } = string.Empty;
        public List<FanCurvePoint> Points { get; set; } = new();
    }

    public class FanCurvePoint
    {
        public int Temperature { get; set; }
        public int FanSpeed { get; set; }
    }

    public class ThermalZone
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double Temperature { get; set; }
        public double? CriticalTemp { get; set; }
        public double? PassiveTemp { get; set; }
        public string Path { get; set; } = string.Empty;
    }
}