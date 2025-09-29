using System.ComponentModel;
using System.Linq;

namespace LenovoLegionToolkit.Avalonia.Models
{
    public enum PowerMode
    {
        [Description("Quiet Mode")]
        Quiet = 0,

        [Description("Balanced Mode")]
        Balanced = 1,

        [Description("Performance Mode")]
        Performance = 2,

        [Description("Custom Mode")]
        Custom = 3
    }

    public static class PowerModeExtensions
    {
        public static string GetDescription(this PowerMode mode)
        {
            var field = mode.GetType().GetField(mode.ToString());
            var attribute = (DescriptionAttribute?)field?.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault();
            return attribute?.Description ?? mode.ToString();
        }

        public static string ToSysfsValue(this PowerMode mode)
        {
            return mode switch
            {
                PowerMode.Quiet => "quiet",
                PowerMode.Balanced => "balanced",
                PowerMode.Performance => "performance",
                PowerMode.Custom => "custom",
                _ => "balanced"
            };
        }

        public static PowerMode FromSysfsValue(string value)
        {
            return value?.ToLower() switch
            {
                "quiet" => PowerMode.Quiet,
                "balanced" => PowerMode.Balanced,
                "performance" => PowerMode.Performance,
                "custom" => PowerMode.Custom,
                _ => PowerMode.Balanced
            };
        }
    }
}