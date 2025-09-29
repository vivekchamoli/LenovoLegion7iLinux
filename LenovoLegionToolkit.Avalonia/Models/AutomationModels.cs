using System;
using System.Collections.Generic;
using System.Linq;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;

namespace LenovoLegionToolkit.Avalonia.Models
{
    public enum AutomationTriggerType
    {
        BatteryLevel,
        PowerState,
        TimeSchedule,
        ProcessRunning,
        NetworkConnection,
        DisplayConnected,
        Manual
    }

    public enum ComparisonOperator
    {
        Equals,
        NotEquals,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
        Contains,
        StartsWith,
        EndsWith
    }

    public enum AutomationAction
    {
        ApplyProfile,
        SetPowerMode,
        SetBatteryChargeLimit,
        SetFanMode,
        SetKeyboardBacklight,
        SetDisplayRefreshRate,
        RunScript,
        ShowNotification
    }

    public class Profile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
        public bool IsDefault { get; set; }

        // Hardware settings
        public PowerMode? PowerMode { get; set; }
        public int? BatteryChargeLimit { get; set; }
        public FanMode? FanMode { get; set; }
        public bool? ConservationMode { get; set; }
        public bool? RapidChargeMode { get; set; }

        // Display settings
        public int? RefreshRate { get; set; }
        public bool? HdrEnabled { get; set; }
        public int? Brightness { get; set; }
        public bool? NightLightEnabled { get; set; }

        // Keyboard settings
        public bool? KeyboardBacklightEnabled { get; set; }
        public int? KeyboardBrightness { get; set; }
        public RgbKeyboardEffect? RgbEffect { get; set; }
        public string? RgbColor { get; set; } // Hex color

        // Advanced settings
        public Dictionary<string, object> CustomSettings { get; set; } = new();

        public Profile Clone()
        {
            return new Profile
            {
                Id = Guid.NewGuid(),
                Name = $"{Name} (Copy)",
                Description = Description,
                CreatedAt = DateTime.Now,
                LastModified = DateTime.Now,
                IsDefault = false,
                PowerMode = PowerMode,
                BatteryChargeLimit = BatteryChargeLimit,
                FanMode = FanMode,
                ConservationMode = ConservationMode,
                RapidChargeMode = RapidChargeMode,
                RefreshRate = RefreshRate,
                HdrEnabled = HdrEnabled,
                Brightness = Brightness,
                NightLightEnabled = NightLightEnabled,
                KeyboardBacklightEnabled = KeyboardBacklightEnabled,
                KeyboardBrightness = KeyboardBrightness,
                RgbEffect = RgbEffect,
                RgbColor = RgbColor,
                CustomSettings = new Dictionary<string, object>(CustomSettings)
            };
        }
    }

    public class AutomationTrigger
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public AutomationTriggerType Type { get; set; }
        public bool IsEnabled { get; set; } = true;
        public Dictionary<string, object> Parameters { get; set; } = new();

        // Type-specific properties
        public int? BatteryLevel { get; set; }
        public bool? OnBattery { get; set; }
        public TimeSpan? TimeOfDay { get; set; }
        public DayOfWeek[]? DaysOfWeek { get; set; }
        public string? ProcessName { get; set; }
        public string? NetworkSSID { get; set; }
        public bool? ExternalDisplayConnected { get; set; }

        public ComparisonOperator Operator { get; set; } = ComparisonOperator.Equals;

        public bool Evaluate(AutomationContext context)
        {
            return Type switch
            {
                AutomationTriggerType.BatteryLevel => EvaluateBatteryLevel(context),
                AutomationTriggerType.PowerState => EvaluatePowerState(context),
                AutomationTriggerType.TimeSchedule => EvaluateTimeSchedule(context),
                AutomationTriggerType.ProcessRunning => EvaluateProcess(context),
                AutomationTriggerType.NetworkConnection => EvaluateNetwork(context),
                AutomationTriggerType.DisplayConnected => EvaluateDisplay(context),
                AutomationTriggerType.Manual => false, // Manual triggers are never automatically evaluated
                _ => false
            };
        }

        private bool EvaluateBatteryLevel(AutomationContext context)
        {
            if (!BatteryLevel.HasValue || !context.BatteryLevel.HasValue)
                return false;

            return Operator switch
            {
                ComparisonOperator.Equals => context.BatteryLevel == BatteryLevel,
                ComparisonOperator.NotEquals => context.BatteryLevel != BatteryLevel,
                ComparisonOperator.LessThan => context.BatteryLevel < BatteryLevel,
                ComparisonOperator.LessThanOrEqual => context.BatteryLevel <= BatteryLevel,
                ComparisonOperator.GreaterThan => context.BatteryLevel > BatteryLevel,
                ComparisonOperator.GreaterThanOrEqual => context.BatteryLevel >= BatteryLevel,
                _ => false
            };
        }

        private bool EvaluatePowerState(AutomationContext context)
        {
            if (!OnBattery.HasValue)
                return false;

            return context.OnBattery == OnBattery.Value;
        }

        private bool EvaluateTimeSchedule(AutomationContext context)
        {
            if (!TimeOfDay.HasValue)
                return false;

            var now = DateTime.Now;
            var currentTime = now.TimeOfDay;

            // Check day of week if specified
            if (DaysOfWeek != null && DaysOfWeek.Length > 0)
            {
                if (!Array.Exists(DaysOfWeek, d => d == now.DayOfWeek))
                    return false;
            }

            // Check time (within 1 minute window)
            var timeDiff = Math.Abs((currentTime - TimeOfDay.Value).TotalMinutes);
            return timeDiff < 1;
        }

        private bool EvaluateProcess(AutomationContext context)
        {
            if (string.IsNullOrEmpty(ProcessName))
                return false;

            return context.RunningProcesses?.Any(p => string.Equals(p, ProcessName, StringComparison.OrdinalIgnoreCase)) ?? false;
        }

        private bool EvaluateNetwork(AutomationContext context)
        {
            if (string.IsNullOrEmpty(NetworkSSID))
                return false;

            return string.Equals(context.ConnectedNetworkSSID, NetworkSSID, StringComparison.OrdinalIgnoreCase);
        }

        private bool EvaluateDisplay(AutomationContext context)
        {
            if (!ExternalDisplayConnected.HasValue)
                return false;

            return context.ExternalDisplayConnected == ExternalDisplayConnected.Value;
        }
    }

    public class AutomationRule
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public int Priority { get; set; } = 0;

        public List<AutomationTrigger> Triggers { get; set; } = new();
        public bool RequireAllTriggers { get; set; } // AND vs OR logic

        public AutomationAction Action { get; set; }
        public Guid? ProfileId { get; set; }
        public Dictionary<string, object> ActionParameters { get; set; } = new();

        public DateTime? LastTriggered { get; set; }
        public int TriggerCount { get; set; }
        public bool HasBeenTriggered => TriggerCount > 0;

        // Cooldown to prevent rapid re-triggering
        public TimeSpan? CooldownPeriod { get; set; }

        public bool ShouldTrigger(AutomationContext context)
        {
            if (!IsEnabled || Triggers.Count == 0)
                return false;

            // Check cooldown
            if (CooldownPeriod.HasValue && LastTriggered.HasValue)
            {
                if (DateTime.Now - LastTriggered.Value < CooldownPeriod.Value)
                    return false;
            }

            // Evaluate triggers
            if (RequireAllTriggers)
            {
                // All triggers must be true (AND logic)
                foreach (var trigger in Triggers)
                {
                    if (!trigger.IsEnabled || !trigger.Evaluate(context))
                        return false;
                }
                return true;
            }
            else
            {
                // At least one trigger must be true (OR logic)
                foreach (var trigger in Triggers)
                {
                    if (trigger.IsEnabled && trigger.Evaluate(context))
                        return true;
                }
                return false;
            }
        }
    }

    public class AutomationContext
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;

        // Current system state
        public int? BatteryLevel { get; set; }
        public bool OnBattery { get; set; }
        public PowerMode CurrentPowerMode { get; set; }
        public List<string>? RunningProcesses { get; set; }
        public string? ConnectedNetworkSSID { get; set; }
        public bool ExternalDisplayConnected { get; set; }
        public int? CurrentRefreshRate { get; set; }

        // Additional context
        public Dictionary<string, object> CustomContext { get; set; } = new();
    }

    public class AutomationEvent
    {
        public DateTime Timestamp { get; set; }
        public AutomationRule Rule { get; set; } = null!;
        public AutomationAction Action { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object>? Details { get; set; }
    }

    public class AutomationConfiguration
    {
        public bool Enabled { get; set; } = true;
        public List<Profile> Profiles { get; set; } = new();
        public List<AutomationRule> Rules { get; set; } = new();
        public List<AutomationEvent> History { get; set; } = new();
        public int MaxHistoryItems { get; set; } = 100;
        public TimeSpan EvaluationInterval { get; set; } = TimeSpan.FromSeconds(30);
        public bool NotifyOnTrigger { get; set; } = true;
        public bool LogEvents { get; set; } = true;
    }
}