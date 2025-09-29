using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using LenovoLegionToolkit.Avalonia.Models;

namespace LenovoLegionToolkit.Avalonia.Settings
{
    public class AppSettings
    {
        public int Version { get; set; } = 1;
        public DateTime LastModified { get; set; } = DateTime.Now;

        // General Settings
        public GeneralSettings General { get; set; } = new();

        // UI Settings
        public UiSettings UI { get; set; } = new();

        // Feature Settings
        public FeatureSettings Features { get; set; } = new();

        // Monitoring Settings
        public MonitoringSettings Monitoring { get; set; } = new();

        // Automation Settings
        public AutomationSettings Automation { get; set; } = new();

        // Advanced Settings
        public AdvancedSettings Advanced { get; set; } = new();
    }

    public class GeneralSettings
    {
        public bool AutoStart { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool StartMinimized { get; set; } = false;
        public bool CheckForUpdates { get; set; } = true;
        public bool EnableNotifications { get; set; } = true;
        public string Language { get; set; } = "en";
    }

    public class UiSettings
    {
        public string Theme { get; set; } = "Dark";
        public string AccentColor { get; set; } = "#0078D4";
        public bool ShowStatusBar { get; set; } = true;
        public bool ShowTrayIcon { get; set; } = true;
        public bool AnimationsEnabled { get; set; } = true;
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 800;
        public int WindowX { get; set; } = -1;
        public int WindowY { get; set; } = -1;
        public bool IsMaximized { get; set; } = false;
        public List<string> PinnedFeatures { get; set; } = new();
        public Dictionary<string, bool> FeatureVisibility { get; set; } = new();
    }

    public class FeatureSettings
    {
        // Power Settings
        public PowerMode DefaultPowerMode { get; set; } = PowerMode.Balanced;
        public bool RestorePowerModeOnStartup { get; set; } = true;
        public bool SyncPowerModeWithAC { get; set; } = false;
        public PowerMode ACPowerMode { get; set; } = PowerMode.Performance;
        public PowerMode BatteryPowerMode { get; set; } = PowerMode.Quiet;

        // Battery Settings
        public bool EnableConservationMode { get; set; } = false;
        public int ConservationModeThreshold { get; set; } = 60;
        public bool EnableRapidCharge { get; set; } = false;
        public bool ShowBatteryPercentage { get; set; } = true;
        public bool LowBatteryNotification { get; set; } = true;
        public int LowBatteryThreshold { get; set; } = 20;

        // Thermal Settings
        public bool CustomFanCurvesEnabled { get; set; } = false;
        public Dictionary<string, FanProfile> FanProfiles { get; set; } = new();
        public string ActiveFanProfile { get; set; } = "Default";
        public int HighTemperatureWarning { get; set; } = 85;

        // Display Settings
        public bool AutoAdjustRefreshRate { get; set; } = false;
        public int PreferredRefreshRate { get; set; } = 60;
        public bool EnableHDR { get; set; } = false;
        public bool EnableOverdrive { get; set; } = false;

        // Keyboard Settings
        public bool RememberBacklightState { get; set; } = true;
        public int DefaultBacklightBrightness { get; set; } = 50;
        public bool EnableRgbEffects { get; set; } = true;
        public string RgbProfile { get; set; } = "Default";

        // Other Hardware
        public bool TouchpadLockEnabled { get; set; } = false;
        public bool FnLockEnabled { get; set; } = false;
        public bool WinKeyDisabled { get; set; } = false;
        public bool CameraPrivacyMode { get; set; } = false;
    }

    public class MonitoringSettings
    {
        public bool EnableMonitoring { get; set; } = true;
        public int UpdateIntervalSeconds { get; set; } = 5;
        public bool MonitorCpuTemperature { get; set; } = true;
        public bool MonitorGpuTemperature { get; set; } = true;
        public bool MonitorFanSpeed { get; set; } = true;
        public bool MonitorBattery { get; set; } = true;
        public bool LogToFile { get; set; } = false;
        public int DataRetentionDays { get; set; } = 7;
        public List<string> EnabledSensors { get; set; } = new();
    }

    public class AutomationSettings
    {
        public bool Enabled { get; set; } = false;
        public List<AutomationRule> Rules { get; set; } = new();
        public bool RunOnStartup { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;
    }

    public class AutomationRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public AutomationTrigger Trigger { get; set; } = new();
        public List<AutomationAction> Actions { get; set; } = new();
        public DateTime? LastExecuted { get; set; }
        public int ExecutionCount { get; set; }
    }

    public class AutomationTrigger
    {
        public string Type { get; set; } = string.Empty; // "PowerChange", "Temperature", "Time", "Process"
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class AutomationAction
    {
        public string Type { get; set; } = string.Empty; // "SetPowerMode", "SetFanSpeed", "RunScript", etc.
        public Dictionary<string, object> Parameters { get; set; } = new();
        public int DelaySeconds { get; set; } = 0;
    }

    public class AdvancedSettings
    {
        public bool EnableDebugMode { get; set; } = false;
        public bool VerboseLogging { get; set; } = false;
        public bool EnableExperimentalFeatures { get; set; } = false;
        public bool BypassCompatibilityChecks { get; set; } = false;
        public bool UseAlternativePowerControl { get; set; } = false;
        public int CommandTimeoutSeconds { get; set; } = 10;
        public bool EnableIpcServer { get; set; } = true;
        public string IpcSocketPath { get; set; } = "/tmp/legion-toolkit.sock";
        public List<string> TrustedIpcClients { get; set; } = new();
        public Dictionary<string, string> CustomSysfsPaths { get; set; } = new();
        public bool ForceRootPermissions { get; set; } = false;
    }

    // Settings versioning for migration
    public class SettingsVersion
    {
        public int Version { get; set; }
        public DateTime MigrationDate { get; set; }
        public string FromVersion { get; set; } = string.Empty;
        public string ToVersion { get; set; } = string.Empty;
    }
}