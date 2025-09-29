using System.Collections.Generic;

namespace LenovoLegionToolkit.Avalonia.Models
{
    public class GpuInfo
    {
        public string Name { get; set; } = string.Empty;
        public GpuVendor Vendor { get; set; }
        public GpuType Type { get; set; }
        public bool IsActive { get; set; }
        public string Driver { get; set; } = string.Empty;
        public string BusId { get; set; } = string.Empty;
        public string PciId { get; set; } = string.Empty;
        public GpuPowerState PowerState { get; set; }
        public double Temperature { get; set; }
        public int PowerDraw { get; set; } // in watts
        public int MemoryUsed { get; set; } // in MB
        public int MemoryTotal { get; set; } // in MB
        public List<string> ActiveProcesses { get; set; } = new();
    }
}