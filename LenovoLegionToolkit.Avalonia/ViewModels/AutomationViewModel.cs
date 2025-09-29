using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.ViewModels
{
    public class AutomationViewModel : ViewModelBase
    {
        private readonly IAutomationService _automationService;
        private readonly INotificationService _notificationService;

        private bool _isAutomationEnabled;
        private bool _isLoading;
        private Profile? _selectedProfile;
        private AutomationRule? _selectedRule;
        private ObservableCollection<Profile> _profiles = new();
        private ObservableCollection<AutomationRule> _rules = new();
        private ObservableCollection<AutomationEvent> _history = new();
        private string _statusText = "Automation Ready";

        // Properties
        public bool IsAutomationEnabled
        {
            get => _isAutomationEnabled;
            set => this.RaiseAndSetIfChanged(ref _isAutomationEnabled, value);
        }

        public new bool IsLoading
        {
            get => _isLoading;
            set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        public Profile? SelectedProfile
        {
            get => _selectedProfile;
            set => this.RaiseAndSetIfChanged(ref _selectedProfile, value);
        }

        public AutomationRule? SelectedRule
        {
            get => _selectedRule;
            set => this.RaiseAndSetIfChanged(ref _selectedRule, value);
        }

        public ObservableCollection<Profile> Profiles
        {
            get => _profiles;
            set => this.RaiseAndSetIfChanged(ref _profiles, value);
        }

        public ObservableCollection<AutomationRule> Rules
        {
            get => _rules;
            set => this.RaiseAndSetIfChanged(ref _rules, value);
        }

        public ObservableCollection<AutomationEvent> History
        {
            get => _history;
            set => this.RaiseAndSetIfChanged(ref _history, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => this.RaiseAndSetIfChanged(ref _statusText, value);
        }

        // Commands
        public ReactiveCommand<Unit, Unit> ToggleAutomationCommand { get; }
        public ReactiveCommand<Unit, Unit> CreateProfileCommand { get; }
        public ReactiveCommand<Unit, Unit> CreateProfileFromCurrentCommand { get; }
        public ReactiveCommand<Profile, Unit> ApplyProfileCommand { get; }
        public ReactiveCommand<Profile, Unit> EditProfileCommand { get; }
        public ReactiveCommand<Profile, Unit> DeleteProfileCommand { get; }
        public ReactiveCommand<Profile, Unit> SetDefaultProfileCommand { get; }
        public ReactiveCommand<Unit, Unit> CreateRuleCommand { get; }
        public ReactiveCommand<AutomationRule, Unit> EditRuleCommand { get; }
        public ReactiveCommand<AutomationRule, Unit> DeleteRuleCommand { get; }
        public ReactiveCommand<AutomationRule, Unit> ToggleRuleCommand { get; }
        public ReactiveCommand<AutomationRule, Unit> TestRuleCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        public ReactiveCommand<Unit, Unit> ClearHistoryCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportConfigCommand { get; }
        public ReactiveCommand<Unit, Unit> ImportConfigCommand { get; }

        public AutomationViewModel(
            IAutomationService automationService,
            INotificationService notificationService)
        {
            _automationService = automationService;
            _notificationService = notificationService;

            // Initialize commands
            ToggleAutomationCommand = ReactiveCommand.CreateFromTask(ToggleAutomationAsync);
            CreateProfileCommand = ReactiveCommand.CreateFromTask(CreateProfileAsync);
            CreateProfileFromCurrentCommand = ReactiveCommand.CreateFromTask(CreateProfileFromCurrentAsync);
            ApplyProfileCommand = ReactiveCommand.CreateFromTask<Profile>(ApplyProfileAsync);
            EditProfileCommand = ReactiveCommand.CreateFromTask<Profile>(EditProfileAsync);
            DeleteProfileCommand = ReactiveCommand.CreateFromTask<Profile>(DeleteProfileAsync);
            SetDefaultProfileCommand = ReactiveCommand.CreateFromTask<Profile>(SetDefaultProfileAsync);
            CreateRuleCommand = ReactiveCommand.CreateFromTask(CreateRuleAsync);
            EditRuleCommand = ReactiveCommand.CreateFromTask<AutomationRule>(EditRuleAsync);
            DeleteRuleCommand = ReactiveCommand.CreateFromTask<AutomationRule>(DeleteRuleAsync);
            ToggleRuleCommand = ReactiveCommand.CreateFromTask<AutomationRule>(ToggleRuleAsync);
            TestRuleCommand = ReactiveCommand.CreateFromTask<AutomationRule>(TestRuleAsync);
            RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
            ClearHistoryCommand = ReactiveCommand.CreateFromTask(ClearHistoryAsync);
            ExportConfigCommand = ReactiveCommand.CreateFromTask(ExportConfigAsync);
            ImportConfigCommand = ReactiveCommand.CreateFromTask(ImportConfigAsync);

            // Subscribe to events
            _automationService.AutomationStateChanged += OnAutomationStateChanged;
            _automationService.RuleTriggered += OnRuleTriggered;
            _automationService.ProfileApplied += OnProfileApplied;
        }

        public override async void Initialize()
        {
            base.Initialize();
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Loading automation data...";

                // Load configuration
                await _automationService.LoadConfigurationAsync();

                // Load profiles
                var profiles = await _automationService.GetProfilesAsync();
                Profiles = new ObservableCollection<Profile>(profiles);

                // Load rules
                var rules = await _automationService.GetRulesAsync();
                Rules = new ObservableCollection<AutomationRule>(rules);

                // Load history
                var history = await _automationService.GetHistoryAsync(50);
                History = new ObservableCollection<AutomationEvent>(history);

                // Check automation state
                IsAutomationEnabled = _automationService.IsRunning;

                StatusText = IsAutomationEnabled ? "Automation Active" : "Automation Stopped";
                Logger.Debug("Automation data loaded");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load automation data", ex);
                StatusText = "Failed to load data";
                SetError($"Failed to load automation data: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ToggleAutomationAsync()
        {
            try
            {
                IsLoading = true;

                if (_automationService.IsRunning)
                {
                    await _automationService.StopAutomationAsync();
                    StatusText = "Automation Stopped";
                    await _notificationService.ShowInfoAsync("Automation has been stopped");
                }
                else
                {
                    await _automationService.StartAutomationAsync();
                    StatusText = "Automation Active";
                    await _notificationService.ShowSuccessAsync("Automation has been started");
                }

                IsAutomationEnabled = _automationService.IsRunning;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to toggle automation", ex);
                SetError($"Failed to toggle automation: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CreateProfileAsync()
        {
            try
            {
                // In a real application, this would open a dialog
                var profile = new Profile
                {
                    Name = $"Profile {Profiles.Count + 1}",
                    Description = "New profile",
                    PowerMode = PowerMode.Balanced,
                    BatteryChargeLimit = 80,
                    FanMode = FanMode.Auto
                };

                var created = await _automationService.CreateProfileAsync(profile);
                Profiles.Add(created);

                await _notificationService.ShowSuccessAsync($"Profile '{created.Name}' created");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create profile", ex);
                SetError($"Failed to create profile: {ex.Message}");
            }
        }

        private async Task CreateProfileFromCurrentAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Creating profile from current state...";

                var name = $"Current State {DateTime.Now:yyyy-MM-dd HH:mm}";
                var description = "Profile created from current hardware state";

                var profile = await _automationService.CreateProfileFromCurrentStateAsync(name, description);
                Profiles.Add(profile);

                StatusText = "Profile created";
                await _notificationService.ShowSuccessAsync($"Profile '{profile.Name}' created from current state");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create profile from current state", ex);
                SetError($"Failed to create profile: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ApplyProfileAsync(Profile profile)
        {
            try
            {
                IsLoading = true;
                StatusText = $"Applying profile '{profile.Name}'...";

                var success = await _automationService.ApplyProfileAsync(profile.Id);

                if (success)
                {
                    StatusText = $"Profile '{profile.Name}' applied";
                    await _notificationService.ShowSuccessAsync($"Profile '{profile.Name}' applied successfully");
                }
                else
                {
                    StatusText = "Failed to apply profile";
                    SetError("Failed to apply profile");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to apply profile {profile.Name}", ex);
                SetError($"Failed to apply profile: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task EditProfileAsync(Profile profile)
        {
            try
            {
                // In a real application, this would open an edit dialog
                profile.LastModified = DateTime.Now;
                var updated = await _automationService.UpdateProfileAsync(profile);

                var index = Profiles.IndexOf(profile);
                if (index >= 0)
                {
                    Profiles[index] = updated;
                }

                await _notificationService.ShowSuccessAsync($"Profile '{profile.Name}' updated");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to edit profile {profile.Name}", ex);
                SetError($"Failed to edit profile: {ex.Message}");
            }
        }

        private async Task DeleteProfileAsync(Profile profile)
        {
            try
            {
                // In a real application, this would show a confirmation dialog
                var success = await _automationService.DeleteProfileAsync(profile.Id);

                if (success)
                {
                    Profiles.Remove(profile);
                    await _notificationService.ShowSuccessAsync($"Profile '{profile.Name}' deleted");
                }
                else
                {
                    SetError("Cannot delete profile - it may be in use by rules");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to delete profile {profile.Name}", ex);
                SetError($"Failed to delete profile: {ex.Message}");
            }
        }

        private async Task SetDefaultProfileAsync(Profile profile)
        {
            try
            {
                var success = await _automationService.SetDefaultProfileAsync(profile.Id);

                if (success)
                {
                    // Update UI to reflect default
                    foreach (var p in Profiles)
                    {
                        p.IsDefault = p.Id == profile.Id;
                    }

                    await _notificationService.ShowSuccessAsync($"Profile '{profile.Name}' set as default");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set default profile {profile.Name}", ex);
                SetError($"Failed to set default profile: {ex.Message}");
            }
        }

        private async Task CreateRuleAsync()
        {
            try
            {
                // In a real application, this would open a rule creation dialog
                var rule = new AutomationRule
                {
                    Name = $"Rule {Rules.Count + 1}",
                    Description = "New automation rule",
                    IsEnabled = false,
                    Action = AutomationAction.ShowNotification,
                    Triggers = new List<AutomationTrigger>
                    {
                        new AutomationTrigger
                        {
                            Name = "Battery Level",
                            Type = AutomationTriggerType.BatteryLevel,
                            BatteryLevel = 20,
                            Operator = ComparisonOperator.LessThanOrEqual
                        }
                    }
                };

                var created = await _automationService.CreateRuleAsync(rule);
                Rules.Add(created);

                await _notificationService.ShowSuccessAsync($"Rule '{created.Name}' created");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create rule", ex);
                SetError($"Failed to create rule: {ex.Message}");
            }
        }

        private async Task EditRuleAsync(AutomationRule rule)
        {
            try
            {
                // In a real application, this would open an edit dialog
                var updated = await _automationService.UpdateRuleAsync(rule);

                var index = Rules.IndexOf(rule);
                if (index >= 0)
                {
                    Rules[index] = updated;
                }

                await _notificationService.ShowSuccessAsync($"Rule '{rule.Name}' updated");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to edit rule {rule.Name}", ex);
                SetError($"Failed to edit rule: {ex.Message}");
            }
        }

        private async Task DeleteRuleAsync(AutomationRule rule)
        {
            try
            {
                // In a real application, this would show a confirmation dialog
                var success = await _automationService.DeleteRuleAsync(rule.Id);

                if (success)
                {
                    Rules.Remove(rule);
                    await _notificationService.ShowSuccessAsync($"Rule '{rule.Name}' deleted");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to delete rule {rule.Name}", ex);
                SetError($"Failed to delete rule: {ex.Message}");
            }
        }

        private async Task ToggleRuleAsync(AutomationRule rule)
        {
            try
            {
                rule.IsEnabled = !rule.IsEnabled;
                var updated = await _automationService.UpdateRuleAsync(rule);

                var index = Rules.IndexOf(rule);
                if (index >= 0)
                {
                    Rules[index] = updated;
                }

                var status = rule.IsEnabled ? "enabled" : "disabled";
                await _notificationService.ShowInfoAsync($"Rule '{rule.Name}' {status}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to toggle rule {rule.Name}", ex);
                SetError($"Failed to toggle rule: {ex.Message}");
            }
        }

        private async Task TestRuleAsync(AutomationRule rule)
        {
            try
            {
                IsLoading = true;
                StatusText = $"Testing rule '{rule.Name}'...";

                var wouldTrigger = await _automationService.TestRuleAsync(rule.Id);

                if (wouldTrigger)
                {
                    StatusText = $"Rule '{rule.Name}' would trigger in current context";
                    await _notificationService.ShowInfoAsync($"Rule '{rule.Name}' would trigger in the current context");
                }
                else
                {
                    StatusText = $"Rule '{rule.Name}' would not trigger";
                    await _notificationService.ShowInfoAsync($"Rule '{rule.Name}' would not trigger in the current context");
                }

                // Optionally trigger it manually
                if (wouldTrigger)
                {
                    // In a real app, might ask for confirmation
                    await _automationService.TriggerRuleManuallyAsync(rule.Id);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to test rule {rule.Name}", ex);
                SetError($"Failed to test rule: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshAsync()
        {
            await LoadDataAsync();
        }

        private async Task ClearHistoryAsync()
        {
            try
            {
                // In a real application, this would show a confirmation dialog
                await _automationService.ClearHistoryAsync();
                History.Clear();
                await _notificationService.ShowSuccessAsync("Automation history cleared");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to clear history", ex);
                SetError($"Failed to clear history: {ex.Message}");
            }
        }

        private async Task ExportConfigAsync()
        {
            try
            {
                // In a real application, this would open a file save dialog
                var filePath = $"/home/{Environment.UserName}/Downloads/legion-automation-{DateTime.Now:yyyyMMdd-HHmmss}.json";
                var success = await _automationService.ExportConfigurationAsync(filePath);

                if (success)
                {
                    await _notificationService.ShowSuccessAsync($"Configuration exported to {filePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to export configuration", ex);
                SetError($"Failed to export configuration: {ex.Message}");
            }
        }

        private async Task ImportConfigAsync()
        {
            try
            {
                // In a real application, this would open a file open dialog
                var filePath = $"/home/{Environment.UserName}/Downloads/legion-automation.json";
                var success = await _automationService.ImportConfigurationAsync(filePath);

                if (success)
                {
                    await LoadDataAsync();
                    await _notificationService.ShowSuccessAsync("Configuration imported successfully");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to import configuration", ex);
                SetError($"Failed to import configuration: {ex.Message}");
            }
        }

        private void OnAutomationStateChanged(object? sender, bool isRunning)
        {
            IsAutomationEnabled = isRunning;
            StatusText = isRunning ? "Automation Active" : "Automation Stopped";
        }

        private void OnRuleTriggered(object? sender, AutomationEvent e)
        {
            // Add to history (keep only last 50)
            History.Insert(0, e);
            while (History.Count > 50)
            {
                History.RemoveAt(History.Count - 1);
            }

            StatusText = $"Rule '{e.Rule.Name}' triggered";
        }

        private void OnProfileApplied(object? sender, Profile profile)
        {
            SelectedProfile = profile;
            StatusText = $"Profile '{profile.Name}' applied";
        }

        public override void Cleanup()
        {
            _automationService.AutomationStateChanged -= OnAutomationStateChanged;
            _automationService.RuleTriggered -= OnRuleTriggered;
            _automationService.ProfileApplied -= OnProfileApplied;
            base.Cleanup();
        }
    }
}