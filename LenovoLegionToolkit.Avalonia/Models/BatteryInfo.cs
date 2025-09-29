using System;

namespace LenovoLegionToolkit.Avalonia.Models
{
    public class BatteryInfo
    {
        public int ChargeLevel { get; set; }
        public bool IsCharging { get; set; }
        public bool IsACConnected { get; set; }
        public bool IsDischarging => !IsCharging && !IsACConnected;
        public double Voltage { get; set; }
        public double Current { get; set; }
        public double Power { get; set; }
        public double Temperature { get; set; }
        public int CycleCount { get; set; }
        public int ChargeLimit { get; set; } = 100;
        public int DesignCapacity { get; set; }
        public int FullChargeCapacity { get; set; }
        public int RemainingCapacity { get; set; }
        public TimeSpan? TimeToFull { get; set; }
        public TimeSpan? TimeToEmpty { get; set; }
        public TimeSpan? EstimatedTimeRemaining => IsCharging ? TimeToFull : TimeToEmpty;
        public double Health => DesignCapacity > 0
            ? Math.Round((double)FullChargeCapacity / DesignCapacity * 100, 1)
            : 100;

        public string Status
        {
            get
            {
                if (IsCharging)
                    return "Charging";
                if (IsACConnected)
                    return "AC Connected";
                return "On Battery";
            }
        }
    }

    public class BatteryMode
    {
        public bool ConservationMode { get; set; }
        public bool RapidChargeMode { get; set; }
        public bool NightChargeMode { get; set; }
        public int ChargeThreshold { get; set; } = 60;
        public int ChargeStopThreshold { get; set; } = 80;
    }
}