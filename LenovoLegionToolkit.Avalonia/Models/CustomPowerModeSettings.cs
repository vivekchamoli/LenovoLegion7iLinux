namespace LenovoLegionToolkit.Avalonia.Models
{
    public class CustomPowerModeSettings
    {
        public int CpuLongTermPowerLimit { get; set; }
        public int CpuShortTermPowerLimit { get; set; }
        public int GpuPowerLimit { get; set; }
        public int FanSpeed { get; set; }
        public bool ApuFixedMode { get; set; }
        public int CpuCrossLoadingPowerLimit { get; set; }
        public int CpuTemperatureLimit { get; set; }
        public int GpuTemperatureLimit { get; set; }
    }
}