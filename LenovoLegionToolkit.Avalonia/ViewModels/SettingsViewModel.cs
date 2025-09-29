using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Settings;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;

        private AppSettings _settings = new();
        private bool _isDirty;
        private string _selectedCategory = "General";

        public AppSettings Settings
        {
            get => _settings;
            set => this.RaiseAndSetIfChanged(ref _settings, value);
        }

        public bool IsDirty
        {
            get => _isDirty;
            set => this.RaiseAndSetIfChanged(ref _isDirty, value);
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set => this.RaiseAndSetIfChanged(ref _selectedCategory, value);
        }

        public ObservableCollection<SettingCategory> Categories { get; }

        // Commands
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> ResetCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportCommand { get; }
        public ReactiveCommand<Unit, Unit> ImportCommand { get; }
        public ReactiveCommand<string, Unit> NavigateToCategoryCommand { get; }
        public ReactiveCommand<Unit, Unit> TestSettingsCommand { get; }

        public SettingsViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;

            Title = "Settings";

            Categories = new ObservableCollection<SettingCategory>
            {
                new() { Name = "General", Icon = "⚙️", Description = "Application settings" },
                new() { Name = "Power", Icon = "⚡", Description = "Power management settings" },
                new() { Name = "Battery", Icon = "🔋", Description = "Battery settings" },
                new() { Name = "Thermal", Icon = "🌡️", Description = "Temperature and fan settings" },
                new() { Name = "Display", Icon = "🖥️", Description = "Display settings" },
                new() { Name = "Keyboard", Icon = "⌨️", Description = "Keyboard and lighting" },
                new() { Name = "Monitoring", Icon = "📊", Description = "Monitoring and logging" },
                new() { Name = "Automation", Icon = "🤖", Description = "Automation rules" },
                new() { Name = "Advanced", Icon = "🔧", Description = "Advanced settings" }
            };

            // Initialize commands
            var canSave = this.WhenAnyValue(x => x.IsDirty);
            SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, canSave);
            ResetCommand = ReactiveCommand.CreateFromTask(ResetAsync);
            ExportCommand = ReactiveCommand.CreateFromTask(ExportSettingsAsync);
            ImportCommand = ReactiveCommand.CreateFromTask(ImportSettingsAsync);
            NavigateToCategoryCommand = ReactiveCommand.Create<string>(NavigateToCategory);
            TestSettingsCommand = ReactiveCommand.CreateFromTask(TestSettingsAsync);

            // Watch for changes
            this.WhenAnyValue(x => x.Settings)
                .Subscribe(_ => IsDirty = true);
        }

        public override void Initialize()
        {
            base.Initialize();
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                Settings = _settingsService.Settings;
                IsDirty = false;

                Logger.Debug("Settings loaded in view model");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load settings", ex);
                SetError("Failed to load settings");
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                IsLoading = true;
                SetStatus("Saving settings...");

                // Validate settings
                if (!ValidateSettings())
                {
                    SetError("Invalid settings. Please check your input.");
                    return;
                }

                // Apply settings
                _settingsService.UpdateSetting<AppSettings>(s =>
                {
                    // Copy all settings
                    s.General = Settings.General;
                    s.UI = Settings.UI;
                    s.Features = Settings.Features;
                    s.Monitoring = Settings.Monitoring;
                    s.Automation = Settings.Automation;
                    s.Advanced = Settings.Advanced;
                });

                await _settingsService.SaveSettingsAsync(Settings);

                // Apply certain settings immediately
                await ApplySettingsAsync();

                IsDirty = false;
                SetStatus("Settings saved successfully");

                Logger.Info("Settings saved");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save settings", ex);
                SetError($"Failed to save settings: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ResetAsync()
        {
            try
            {
                SetStatus("Resetting settings...");

                _settingsService.ResetSettings();
                Settings = _settingsService.LoadSettings();
                LoadSettings();
                SetStatus("Settings reset to defaults");
                Logger.Info("Settings reset to defaults");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to reset settings", ex);
                SetError($"Failed to reset settings: {ex.Message}");
            }
        }

        private async Task ExportSettingsAsync()
        {
            try
            {
                // In a real implementation, this would open a file dialog
                var exportPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"legion-toolkit-settings-{DateTime.Now:yyyyMMdd-HHmmss}.json"
                );

                var json = System.Text.Json.JsonSerializer.Serialize(Settings, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await System.IO.File.WriteAllTextAsync(exportPath, json);

                SetStatus($"Settings exported to {exportPath}");
                Logger.Info($"Settings exported to {exportPath}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to export settings", ex);
                SetError($"Failed to export settings: {ex.Message}");
            }
        }

        private async Task ImportSettingsAsync()
        {
            try
            {
                // In a real implementation, this would open a file dialog
                SetError("Import functionality not yet implemented");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to import settings", ex);
                SetError($"Failed to import settings: {ex.Message}");
            }
        }

        private void NavigateToCategory(string category)
        {
            SelectedCategory = category;
            SetStatus($"Viewing {category} settings");
        }

        private async Task TestSettingsAsync()
        {
            try
            {
                SetStatus("Testing settings...");

                // Test various settings
                var tests = new[]
                {
                    TestAutoStart(),
                    TestIpcServer(),
                    TestMonitoring()
                };

                await Task.WhenAll(tests);

                SetStatus("Settings test completed");
            }
            catch (Exception ex)
            {
                Logger.Error("Settings test failed", ex);
                SetError($"Settings test failed: {ex.Message}");
            }
        }

        private bool ValidateSettings()
        {
            try
            {
                // Validate general settings
                if (string.IsNullOrWhiteSpace(Settings.General.Language))
                {
                    Logger.Warning("Invalid language setting");
                    return false;
                }

                // Validate monitoring settings
                if (Settings.Monitoring.UpdateIntervalSeconds < 1 || Settings.Monitoring.UpdateIntervalSeconds > 3600)
                {
                    Logger.Warning("Invalid monitoring interval");
                    return false;
                }

                // Validate battery thresholds
                if (Settings.Features.ConservationModeThreshold < 20 || Settings.Features.ConservationModeThreshold > 80)
                {
                    Logger.Warning("Invalid conservation mode threshold");
                    return false;
                }

                if (Settings.Features.LowBatteryThreshold < 5 || Settings.Features.LowBatteryThreshold > 50)
                {
                    Logger.Warning("Invalid low battery threshold");
                    return false;
                }

                // Validate thermal settings
                if (Settings.Features.HighTemperatureWarning < 50 || Settings.Features.HighTemperatureWarning > 100)
                {
                    Logger.Warning("Invalid temperature warning threshold");
                    return false;
                }

                // Validate advanced settings
                if (Settings.Advanced.CommandTimeoutSeconds < 1 || Settings.Advanced.CommandTimeoutSeconds > 300)
                {
                    Logger.Warning("Invalid command timeout");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Settings validation failed", ex);
                return false;
            }
        }

        private async Task ApplySettingsAsync()
        {
            try
            {
                // Apply autostart setting
                if (Settings.General.AutoStart)
                {
                    _settingsService.SetAutoStart(true);
                }

                // Restart monitoring if settings changed
                // This would be handled by the service

                // Apply theme
                // This would be handled by the UI layer

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply settings", ex);
            }
        }

        private async Task<bool> TestAutoStart()
        {
            try
            {
                var autostartFile = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config/autostart/legion-toolkit.desktop"
                );

                return await Task.FromResult(System.IO.File.Exists(autostartFile) == Settings.General.AutoStart);
            }
            catch (Exception ex)
            {
                Logger.Error("Autostart test failed", ex);
                return false;
            }
        }

        private async Task<bool> TestIpcServer()
        {
            try
            {
                if (!Settings.Advanced.EnableIpcServer)
                    return true;

                // Test IPC server connectivity
                return await Task.FromResult(System.IO.File.Exists(Settings.Advanced.IpcSocketPath));
            }
            catch (Exception ex)
            {
                Logger.Error("IPC server test failed", ex);
                return false;
            }
        }

        private async Task<bool> TestMonitoring()
        {
            try
            {
                if (!Settings.Monitoring.EnableMonitoring)
                    return true;

                // Test monitoring is active
                // This would check if the monitoring timer is running
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Logger.Error("Monitoring test failed", ex);
                return false;
            }
        }
    }

    public class SettingCategory : ReactiveObject
    {
        private string _name = string.Empty;
        private string _icon = string.Empty;
        private string _description = string.Empty;
        private bool _isSelected;

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public string Icon
        {
            get => _icon;
            set => this.RaiseAndSetIfChanged(ref _icon, value);
        }

        public string Description
        {
            get => _description;
            set => this.RaiseAndSetIfChanged(ref _description, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => this.RaiseAndSetIfChanged(ref _isSelected, value);
        }
    }
}