using System;
using System.Collections.Generic;

namespace LenovoLegionToolkit.Avalonia.Models
{
    public enum RgbKeyboardZone
    {
        All = 0,
        Left = 1,
        Center = 2,
        Right = 3,
        WASD = 4,
        NumPad = 5,
        FunctionKeys = 6,
        ArrowKeys = 7
    }

    public enum RgbKeyboardEffect
    {
        Static = 0,
        Breathing = 1,
        Wave = 2,
        Rainbow = 3,
        Ripple = 4,
        Shift = 5,
        Pulse = 6,
        Random = 7
    }

    public class RgbKeyboardInfo
    {
        public bool IsSupported { get; set; }
        public bool Is4Zone { get; set; }
        public bool IsPerKey { get; set; }
        public int MaxBrightness { get; set; } = 100;
        public List<RgbKeyboardZone> SupportedZones { get; set; } = new();
        public List<RgbKeyboardEffect> SupportedEffects { get; set; } = new();
    }

    public class RgbKeyboardState
    {
        public bool IsOn { get; set; }
        public int Brightness { get; set; }
        public RgbKeyboardEffect CurrentEffect { get; set; }
        public Dictionary<RgbKeyboardZone, RgbColor> ZoneColors { get; set; } = new();
        public byte EffectSpeed { get; set; }
    }

    public class RgbKeyboardProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ModifiedAt { get; set; } = DateTime.Now;
        public RgbKeyboardEffect Effect { get; set; }
        public Dictionary<RgbKeyboardZone, RgbColor> ZoneColors { get; set; } = new();
        public int Brightness { get; set; } = 100;
        public byte EffectSpeed { get; set; } = 5;
        public bool AutoApplyOnAC { get; set; }
        public bool AutoApplyOnBattery { get; set; }
    }

    public struct RgbColor
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public RgbColor(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

        public static RgbColor FromHex(string hex)
        {
            hex = hex.TrimStart('#');
            if (hex.Length != 6)
                throw new ArgumentException("Invalid hex color format");

            return new RgbColor(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16)
            );
        }

        public override string ToString() => ToHex();

        // Predefined colors
        public static readonly RgbColor Red = new(255, 0, 0);
        public static readonly RgbColor Green = new(0, 255, 0);
        public static readonly RgbColor Blue = new(0, 0, 255);
        public static readonly RgbColor White = new(255, 255, 255);
        public static readonly RgbColor Black = new(0, 0, 0);
        public static readonly RgbColor Yellow = new(255, 255, 0);
        public static readonly RgbColor Cyan = new(0, 255, 255);
        public static readonly RgbColor Magenta = new(255, 0, 255);
        public static readonly RgbColor Orange = new(255, 165, 0);
        public static readonly RgbColor Purple = new(128, 0, 128);
        public static readonly RgbColor Pink = new(255, 192, 203);
        public static readonly RgbColor LegionRed = new(220, 38, 38);
    }
}