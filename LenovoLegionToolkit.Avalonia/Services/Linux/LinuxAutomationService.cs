using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;
using Timer = System.Timers.Timer;

namespace LenovoLegionToolkit.Avalonia.Services.Linux
{
    public class LinuxAutomationService : IAutomationService
    {
        private readonly IPowerModeService _powerModeService;
        private readonly IBatteryService _batteryService;
        private readonly IThermalService _thermalService;
        private readonly IKeyboardService _keyboardService;
        private readonly IDisplayService _displayService;
        private readonly ISettingsService _settingsService;
        private readonly INotificationService _notificationService;

        private readonly string _configPath;
        private Timer? _evaluationTimer;
        private AutomationConfiguration _configuration = new();
        private Profile? _currentProfile;
        private readonly SemaphoreSlim _evaluationLock = new(1, 1);
        private DateTime _lastEvaluationTime = DateTime.MinValue;

        public event EventHandler<AutomationEvent>? RuleTriggered;
        public event EventHandler<Profile>? ProfileApplied;
        public event EventHandler<bool>? AutomationStateChanged;

        public bool IsRunning { get; private set; }
        public AutomationConfiguration Configuration => _configuration;
        public Profile? CurrentProfile => _currentProfile;

        public LinuxAutomationService(
            IPowerModeService powerModeService,
            IBatteryService batteryService,
            IThermalService thermalService,
            IKeyboardService keyboardService,
            IDisplayService displayService,
            ISettingsService settingsService,
            INotificationService notificationService)
        {
            _powerModeService = powerModeService;
            _batteryService = batteryService;
            _thermalService = thermalService;
            _keyboardService = keyboardService;
            _displayService = displayService;
            _settingsService = settingsService;
            _notificationService = notificationService;

            var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LegionToolkit");
            Directory.CreateDirectory(configDir);
            _configPath = Path.Combine(configDir, "automation.json");
        }

        // Profile management
        public Task<List<Profile>> GetProfilesAsync()
        {
            return Task.FromResult(_configuration.Profiles.ToList());
        }

        public Task<Profile?> GetProfileAsync(Guid profileId)
        {
            var profile = _configuration.Profiles.FirstOrDefault(p => p.Id == profileId);
            return Task.FromResult(profile);
        }

        public async Task<Profile> CreateProfileAsync(Profile profile)
        {
            profile.Id = Guid.NewGuid();
            profile.CreatedAt = DateTime.Now;
            profile.LastModified = DateTime.Now;

            _configuration.Profiles.Add(profile);
            await SaveConfigurationAsync();

            Logger.Info($"Created profile: {profile.Name}");
            return profile;
        }

        public async Task<Profile> UpdateProfileAsync(Profile profile)
        {
            var existing = _configuration.Profiles.FirstOrDefault(p => p.Id == profile.Id);
            if (existing == null)
                throw new InvalidOperationException($"Profile {profile.Id} not found");

            profile.LastModified = DateTime.Now;
            _configuration.Profiles[_configuration.Profiles.IndexOf(existing)] = profile;
            await SaveConfigurationAsync();

            Logger.Info($"Updated profile: {profile.Name}");
            return profile;
        }

        public async Task<bool> DeleteProfileAsync(Guid profileId)
        {
            var profile = _configuration.Profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null)
                return false;

            // Check if profile is used in any rules
            var rulesUsingProfile = _configuration.Rules
                .Where(r => r.ProfileId == profileId)
                .ToList();

            if (rulesUsingProfile.Any())
            {
                Logger.Warning($"Cannot delete profile {profile.Name} - used by {rulesUsingProfile.Count} rules");
                return false;
            }

            _configuration.Profiles.Remove(profile);
            await SaveConfigurationAsync();

            Logger.Info($"Deleted profile: {profile.Name}");
            return true;
        }

        public async Task<bool> ApplyProfileAsync(Guid profileId)
        {
            var profile = await GetProfileAsync(profileId);
            if (profile == null)
                return false;

            try
            {
                var tasks = new List<Task>();

                // Apply power mode
                if (profile.PowerMode.HasValue)
                {
                    tasks.Add(_powerModeService.SetPowerModeAsync(profile.PowerMode.Value));
                }

                // Apply battery settings
                if (profile.BatteryChargeLimit.HasValue)
                {
                    tasks.Add(_batteryService.SetChargeLimitAsync(profile.BatteryChargeLimit.Value));
                }
                if (profile.ConservationMode.HasValue)
                {
                    tasks.Add(_batteryService.SetConservationModeAsync(profile.ConservationMode.Value));
                }
                if (profile.RapidChargeMode.HasValue)
                {
                    tasks.Add(_batteryService.SetRapidChargeModeAsync(profile.RapidChargeMode.Value));
                }

                // Apply fan mode
                if (profile.FanMode.HasValue)
                {
                    tasks.Add(_thermalService.SetFanModeAsync(profile.FanMode.Value));
                }

                // Apply display settings
                if (profile.RefreshRate.HasValue)
                {
                    var displays = await _displayService.GetDisplaysAsync();
                    var internalDisplay = displays.FirstOrDefault(d => d.IsInternal);
                    if (internalDisplay != null)
                    {
                        tasks.Add(_displayService.SetRefreshRateAsync(internalDisplay.Id, profile.RefreshRate.Value));
                    }
                }
                if (profile.HdrEnabled.HasValue)
                {
                    var displays = await _displayService.GetDisplaysAsync();
                    var internalDisplay = displays.FirstOrDefault(d => d.IsInternal);
                    if (internalDisplay != null)
                    {
                        tasks.Add(_displayService.SetHdrEnabledAsync(profile.HdrEnabled.Value));
                    }
                }
                if (profile.Brightness.HasValue)
                {
                    var displays = await _displayService.GetDisplaysAsync();
                    var internalDisplay = displays.FirstOrDefault(d => d.IsInternal);
                    if (internalDisplay != null)
                    {
                        tasks.Add(_displayService.SetBrightnessAsync(internalDisplay.Id, profile.Brightness.Value));
                    }
                }
                if (profile.NightLightEnabled.HasValue)
                {
                    tasks.Add(_displayService.SetNightLightEnabledAsync(profile.NightLightEnabled.Value));
                }

                // Apply keyboard settings
                if (profile.KeyboardBacklightEnabled.HasValue)
                {
                    tasks.Add(_keyboardService.SetBacklightEnabledAsync(profile.KeyboardBacklightEnabled.Value));
                }
                if (profile.KeyboardBrightness.HasValue)
                {
                    tasks.Add(_keyboardService.SetBrightnessAsync(profile.KeyboardBrightness.Value));
                }
                if (profile.RgbEffect.HasValue && !string.IsNullOrEmpty(profile.RgbColor))
                {
                    // Parse hex color
                    if (profile.RgbColor.StartsWith("#"))
                        profile.RgbColor = profile.RgbColor.Substring(1);

                    if (profile.RgbColor.Length == 6)
                    {
                        byte r = Convert.ToByte(profile.RgbColor.Substring(0, 2), 16);
                        byte g = Convert.ToByte(profile.RgbColor.Substring(2, 2), 16);
                        byte b = Convert.ToByte(profile.RgbColor.Substring(4, 2), 16);

                        if (profile.RgbEffect == RgbKeyboardEffect.Static)
                        {
                            tasks.Add(_keyboardService.SetStaticColorAsync(r, g, b));
                        }
                        else
                        {
                            tasks.Add(_keyboardService.SetEffectAsync(profile.RgbEffect.Value));
                        }
                    }
                }

                await Task.WhenAll(tasks);

                _currentProfile = profile;
                ProfileApplied?.Invoke(this, profile);

                Logger.Info($"Applied profile: {profile.Name}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to apply profile {profile.Name}", ex);
                return false;
            }
        }

        public async Task<Profile> CreateProfileFromCurrentStateAsync(string name, string description)
        {
            var profile = new Profile
            {
                Name = name,
                Description = description,
                CreatedAt = DateTime.Now,
                LastModified = DateTime.Now
            };

            // Capture current power mode
            profile.PowerMode = await _powerModeService.GetCurrentModeAsync();

            // Capture battery settings
            var batteryInfo = await _batteryService.GetBatteryInfoAsync();
            profile.BatteryChargeLimit = batteryInfo.ChargeLimit;
            var batteryMode = await _batteryService.GetBatteryModeAsync();
            profile.ConservationMode = batteryMode.ConservationMode;
            profile.RapidChargeMode = batteryMode.RapidChargeMode;

            // Capture thermal settings
            profile.FanMode = FanMode.Auto; // TODO: Add GetCurrentFanModeAsync to IThermalService

            // Capture display settings
            var displays = await _displayService.GetDisplaysAsync();
            var internalDisplay = displays.FirstOrDefault(d => d.IsInternal);
            if (internalDisplay != null)
            {
                profile.RefreshRate = internalDisplay.CurrentRefreshRate;
                profile.HdrEnabled = internalDisplay.HdrEnabled;
                profile.Brightness = internalDisplay.Brightness;
            }
            var nightLightSettings = await _displayService.GetNightLightSettingsAsync();
            profile.NightLightEnabled = nightLightSettings?.Enabled ?? false;

            // Capture keyboard settings
            // TODO: Add keyboard capabilities methods to IKeyboardService
            profile.KeyboardBacklightEnabled = false;
            profile.KeyboardBrightness = await _keyboardService.GetBrightnessAsync();
            profile.RgbEffect = RgbKeyboardEffect.Static;
            profile.RgbColor = "#FFFFFF";

            return await CreateProfileAsync(profile);
        }

        public async Task<bool> SetDefaultProfileAsync(Guid profileId)
        {
            var profile = await GetProfileAsync(profileId);
            if (profile == null)
                return false;

            // Clear previous default
            foreach (var p in _configuration.Profiles)
            {
                p.IsDefault = false;
            }

            profile.IsDefault = true;
            await SaveConfigurationAsync();

            Logger.Info($"Set default profile: {profile.Name}");
            return true;
        }

        // Rule management
        public Task<List<AutomationRule>> GetRulesAsync()
        {
            return Task.FromResult(_configuration.Rules.OrderBy(r => r.Priority).ToList());
        }

        public Task<AutomationRule?> GetRuleAsync(Guid ruleId)
        {
            var rule = _configuration.Rules.FirstOrDefault(r => r.Id == ruleId);
            return Task.FromResult(rule);
        }

        public async Task<AutomationRule> CreateRuleAsync(AutomationRule rule)
        {
            rule.Id = Guid.NewGuid();
            _configuration.Rules.Add(rule);
            await SaveConfigurationAsync();

            Logger.Info($"Created automation rule: {rule.Name}");
            return rule;
        }

        public async Task<AutomationRule> UpdateRuleAsync(AutomationRule rule)
        {
            var existing = _configuration.Rules.FirstOrDefault(r => r.Id == rule.Id);
            if (existing == null)
                throw new InvalidOperationException($"Rule {rule.Id} not found");

            _configuration.Rules[_configuration.Rules.IndexOf(existing)] = rule;
            await SaveConfigurationAsync();

            Logger.Info($"Updated automation rule: {rule.Name}");
            return rule;
        }

        public async Task<bool> DeleteRuleAsync(Guid ruleId)
        {
            var rule = _configuration.Rules.FirstOrDefault(r => r.Id == ruleId);
            if (rule == null)
                return false;

            _configuration.Rules.Remove(rule);
            await SaveConfigurationAsync();

            Logger.Info($"Deleted automation rule: {rule.Name}");
            return true;
        }

        public async Task<bool> EnableRuleAsync(Guid ruleId, bool enabled)
        {
            var rule = await GetRuleAsync(ruleId);
            if (rule == null)
                return false;

            rule.IsEnabled = enabled;
            await UpdateRuleAsync(rule);
            return true;
        }

        public async Task<bool> TestRuleAsync(Guid ruleId)
        {
            var rule = await GetRuleAsync(ruleId);
            if (rule == null)
                return false;

            var context = await GetCurrentContextAsync();
            return rule.ShouldTrigger(context);
        }

        // Automation engine
        public async Task<bool> StartAutomationAsync()
        {
            if (IsRunning)
                return false;

            await LoadConfigurationAsync();

            if (!_configuration.Enabled)
            {
                Logger.Warning("Automation is disabled in configuration");
                return false;
            }

            _evaluationTimer = new Timer(_configuration.EvaluationInterval.TotalMilliseconds);
            _evaluationTimer.Elapsed += async (sender, e) => await EvaluateRulesAsync();
            _evaluationTimer.Start();

            IsRunning = true;
            AutomationStateChanged?.Invoke(this, true);

            Logger.Info("Automation service started");
            return true;
        }

        public Task<bool> StopAutomationAsync()
        {
            if (!IsRunning)
                return Task.FromResult(false);

            _evaluationTimer?.Stop();
            _evaluationTimer?.Dispose();
            _evaluationTimer = null;

            IsRunning = false;
            AutomationStateChanged?.Invoke(this, false);

            Logger.Info("Automation service stopped");
            return Task.FromResult(true);
        }

        public async Task EvaluateRulesAsync()
        {
            if (!IsRunning || !_configuration.Enabled)
                return;

            // Prevent concurrent evaluations
            if (!await _evaluationLock.WaitAsync(0))
                return;

            try
            {
                var context = await GetCurrentContextAsync();
                var triggeredRules = new List<AutomationRule>();

                // Check each rule
                foreach (var rule in _configuration.Rules.Where(r => r.IsEnabled).OrderBy(r => r.Priority))
                {
                    if (rule.ShouldTrigger(context))
                    {
                        triggeredRules.Add(rule);
                    }
                }

                // Execute triggered rules
                foreach (var rule in triggeredRules)
                {
                    await ExecuteRuleAsync(rule, context);
                }

                _lastEvaluationTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                Logger.Error("Error evaluating automation rules", ex);
            }
            finally
            {
                _evaluationLock.Release();
            }
        }

        public async Task<bool> TriggerRuleManuallyAsync(Guid ruleId)
        {
            var rule = await GetRuleAsync(ruleId);
            if (rule == null)
                return false;

            var context = await GetCurrentContextAsync();
            return await ExecuteRuleAsync(rule, context);
        }

        private async Task<bool> ExecuteRuleAsync(AutomationRule rule, AutomationContext context)
        {
            var automationEvent = new AutomationEvent
            {
                Timestamp = DateTime.Now,
                Rule = rule,
                Action = rule.Action
            };

            try
            {
                bool success = false;

                switch (rule.Action)
                {
                    case AutomationAction.ApplyProfile:
                        if (rule.ProfileId.HasValue)
                        {
                            success = await ApplyProfileAsync(rule.ProfileId.Value);
                        }
                        break;

                    case AutomationAction.SetPowerMode:
                        if (rule.ActionParameters.TryGetValue("PowerMode", out var powerModeObj) &&
                            Enum.TryParse<PowerMode>(powerModeObj.ToString(), out var powerMode))
                        {
                            success = await _powerModeService.SetPowerModeAsync(powerMode);
                        }
                        break;

                    case AutomationAction.SetBatteryChargeLimit:
                        if (rule.ActionParameters.TryGetValue("ChargeLimit", out var chargeLimitObj) &&
                            int.TryParse(chargeLimitObj.ToString(), out var chargeLimit))
                        {
                            success = await _batteryService.SetChargeLimitAsync(chargeLimit);
                        }
                        break;

                    case AutomationAction.SetFanMode:
                        if (rule.ActionParameters.TryGetValue("FanMode", out var fanModeObj) &&
                            Enum.TryParse<FanMode>(fanModeObj.ToString(), out var fanMode))
                        {
                            success = await _thermalService.SetFanModeAsync(fanMode);
                        }
                        break;

                    case AutomationAction.SetKeyboardBacklight:
                        if (rule.ActionParameters.TryGetValue("Enabled", out var enabledObj) &&
                            bool.TryParse(enabledObj.ToString(), out var enabled))
                        {
                            success = await _keyboardService.SetBacklightEnabledAsync(enabled);
                        }
                        break;

                    case AutomationAction.SetDisplayRefreshRate:
                        if (rule.ActionParameters.TryGetValue("RefreshRate", out var refreshRateObj) &&
                            int.TryParse(refreshRateObj.ToString(), out var refreshRate))
                        {
                            var displays = await _displayService.GetDisplaysAsync();
                            var internalDisplay = displays.FirstOrDefault(d => d.IsInternal);
                            if (internalDisplay != null)
                            {
                                success = await _displayService.SetRefreshRateAsync(internalDisplay.Id, refreshRate);
                            }
                        }
                        break;

                    case AutomationAction.RunScript:
                        if (rule.ActionParameters.TryGetValue("Script", out var scriptObj))
                        {
                            success = await RunScriptAsync(scriptObj.ToString() ?? string.Empty);
                        }
                        break;

                    case AutomationAction.ShowNotification:
                        if (rule.ActionParameters.TryGetValue("Message", out var messageObj))
                        {
                            await _notificationService.ShowNotificationAsync(new Notification
                            {
                                Title = $"Automation: {rule.Name}",
                                Message = messageObj.ToString() ?? "Rule triggered",
                                Type = NotificationType.Information
                            });
                            success = true;
                        }
                        break;
                }

                automationEvent.Success = success;

                if (success)
                {
                    rule.LastTriggered = DateTime.Now;
                    rule.TriggerCount++;

                    if (_configuration.NotifyOnTrigger)
                    {
                        await _notificationService.ShowNotificationAsync(new Notification
                        {
                            Title = "Automation Triggered",
                            Message = $"Rule '{rule.Name}' executed successfully",
                            Type = NotificationType.Success
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                automationEvent.Success = false;
                automationEvent.ErrorMessage = ex.Message;
                Logger.Error($"Failed to execute automation rule {rule.Name}", ex);
            }

            // Add to history
            _configuration.History.Add(automationEvent);

            // Trim history
            while (_configuration.History.Count > _configuration.MaxHistoryItems)
            {
                _configuration.History.RemoveAt(0);
            }

            RuleTriggered?.Invoke(this, automationEvent);
            await SaveConfigurationAsync();

            return automationEvent.Success;
        }

        private async Task<bool> RunScriptAsync(string script)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/sh",
                        Arguments = $"-c \"{script}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to run script: {script}", ex);
                return false;
            }
        }

        // Context and history
        public async Task<AutomationContext> GetCurrentContextAsync()
        {
            var context = new AutomationContext
            {
                Timestamp = DateTime.Now
            };

            try
            {
                // Get battery info
                var batteryInfo = await _batteryService.GetBatteryInfoAsync();
                context.BatteryLevel = batteryInfo.ChargeLevel;
                context.OnBattery = batteryInfo.IsCharging == false;

                // Get power mode
                context.CurrentPowerMode = await _powerModeService.GetCurrentModeAsync();

                // Get running processes
                context.RunningProcesses = GetRunningProcesses();

                // Get network info
                context.ConnectedNetworkSSID = await GetConnectedNetworkSSIDAsync();

                // Get display info
                var displays = await _displayService.GetDisplaysAsync();
                context.ExternalDisplayConnected = displays.Any(d => !d.IsInternal);
                var internalDisplay = displays.FirstOrDefault(d => d.IsInternal);
                context.CurrentRefreshRate = internalDisplay?.CurrentRefreshRate;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to build automation context", ex);
            }

            return context;
        }

        private List<string> GetRunningProcesses()
        {
            try
            {
                var processes = Process.GetProcesses()
                    .Select(p => p.ProcessName)
                    .Distinct()
                    .ToList();
                return processes;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get running processes", ex);
                return new List<string>();
            }
        }

        private async Task<string?> GetConnectedNetworkSSIDAsync()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "iwgetid",
                        Arguments = "-r",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                return output.Trim();
            }
            catch
            {
                return null;
            }
        }

        public Task<List<AutomationEvent>> GetHistoryAsync(int count = 50)
        {
            var history = _configuration.History
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToList();
            return Task.FromResult(history);
        }

        public async Task ClearHistoryAsync()
        {
            _configuration.History.Clear();
            await SaveConfigurationAsync();
            Logger.Info("Automation history cleared");
        }

        // Configuration
        public async Task<bool> SaveConfigurationAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_configuration, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                });
                await File.WriteAllTextAsync(_configPath, json);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save automation configuration", ex);
                return false;
            }
        }

        public async Task<bool> LoadConfigurationAsync()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = await File.ReadAllTextAsync(_configPath);
                    _configuration = JsonSerializer.Deserialize<AutomationConfiguration>(json, new JsonSerializerOptions
                    {
                        Converters = { new JsonStringEnumConverter() }
                    }) ?? new AutomationConfiguration();
                }
                else
                {
                    _configuration = new AutomationConfiguration();
                    await SaveConfigurationAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load automation configuration", ex);
                _configuration = new AutomationConfiguration();
                return false;
            }
        }

        public async Task<bool> ExportConfigurationAsync(string filePath)
        {
            try
            {
                var json = JsonSerializer.Serialize(_configuration, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                });
                await File.WriteAllTextAsync(filePath, json);
                Logger.Info($"Exported automation configuration to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to export configuration to {filePath}", ex);
                return false;
            }
        }

        public async Task<bool> ImportConfigurationAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Logger.Error($"Import file not found: {filePath}");
                    return false;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var imported = JsonSerializer.Deserialize<AutomationConfiguration>(json, new JsonSerializerOptions
                {
                    Converters = { new JsonStringEnumConverter() }
                });

                if (imported == null)
                {
                    Logger.Error("Failed to parse imported configuration");
                    return false;
                }

                _configuration = imported;
                await SaveConfigurationAsync();

                Logger.Info($"Imported automation configuration from {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to import configuration from {filePath}", ex);
                return false;
            }
        }
    }

    // Missing enum converter
    public class JsonStringEnumConverter : System.Text.Json.Serialization.JsonConverter<Enum>
    {
        public override Enum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return (Enum)Enum.Parse(typeToConvert, value ?? string.Empty);
        }

        public override void Write(Utf8JsonWriter writer, Enum value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }

        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsEnum;
        }
    }
}