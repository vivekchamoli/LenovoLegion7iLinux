using System;
using System.Collections.Generic;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.ViewModels;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.SystemTray
{
    public class TrayMenu : IDisposable
    {
        private readonly MainViewModel _mainViewModel;
        private NativeMenu? _menu;
        private NativeMenuItem? _showHideItem;
        private Dictionary<PowerMode, NativeMenuItem> _powerModeItems = new();
        private Dictionary<string, NativeMenuItem> _rgbPresetItems = new();

        public TrayMenu(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
        }

        public NativeMenu CreateMenu()
        {
            _menu = new NativeMenu();

            // Show/Hide main window
            _showHideItem = new NativeMenuItem("Show Legion Toolkit");
            _showHideItem.Click += OnShowHideClicked;
            _menu.Items.Add(_showHideItem);

            _menu.Items.Add(new NativeMenuItemSeparator());

            // Power Mode submenu
            var powerMenu = CreatePowerModeMenu();
            if (powerMenu != null)
            {
                _menu.Items.Add(powerMenu);
            }

            // RGB Keyboard submenu
            var rgbMenu = CreateRgbMenu();
            if (rgbMenu != null)
            {
                _menu.Items.Add(rgbMenu);
            }

            // Quick Actions
            var quickActionsMenu = CreateQuickActionsMenu();
            if (quickActionsMenu != null)
            {
                _menu.Items.Add(quickActionsMenu);
            }

            _menu.Items.Add(new NativeMenuItemSeparator());

            // Tools submenu
            var toolsMenu = CreateToolsMenu();
            if (toolsMenu != null)
            {
                _menu.Items.Add(toolsMenu);
            }

            _menu.Items.Add(new NativeMenuItemSeparator());

            // Settings
            var settingsItem = new NativeMenuItem("Settings...");
            settingsItem.Click += OnSettingsClicked;
            _menu.Items.Add(settingsItem);

            // About
            var aboutItem = new NativeMenuItem("About...");
            aboutItem.Click += OnAboutClicked;
            _menu.Items.Add(aboutItem);

            _menu.Items.Add(new NativeMenuItemSeparator());

            // Exit
            var exitItem = new NativeMenuItem("Exit");
            exitItem.Click += OnExitClicked;
            _menu.Items.Add(exitItem);

            return _menu;
        }

        private NativeMenuItem? CreatePowerModeMenu()
        {
            try
            {
                var powerMenu = new NativeMenuItem("Power Mode");
                var submenu = new NativeMenu();

                // Add power mode options
                var modes = new[]
                {
                    PowerMode.Quiet,
                    PowerMode.Balanced,
                    PowerMode.Performance,
                    PowerMode.Custom
                };

                foreach (var mode in modes)
                {
                    var item = new NativeMenuItem(GetPowerModeName(mode));
                    item.Click += (sender, e) => OnPowerModeSelected(mode);
                    _powerModeItems[mode] = item;
                    submenu.Items.Add(item);
                }

                powerMenu.Menu = submenu;
                return powerMenu;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to create power mode menu: {ex.Message}");
                return null;
            }
        }

        private NativeMenuItem? CreateRgbMenu()
        {
            try
            {
                var rgbMenu = new NativeMenuItem("RGB Keyboard");
                var submenu = new NativeMenu();

                // RGB On/Off
                var toggleItem = new NativeMenuItem("Toggle RGB");
                toggleItem.Click += OnRgbToggleClicked;
                submenu.Items.Add(toggleItem);

                submenu.Items.Add(new NativeMenuItemSeparator());

                // Preset colors
                var presets = new Dictionary<string, string>
                {
                    ["Legion Red"] = "#DC2626",
                    ["Blue"] = "#0000FF",
                    ["Green"] = "#00FF00",
                    ["Purple"] = "#800080",
                    ["Rainbow"] = "effect:rainbow"
                };

                foreach (var preset in presets)
                {
                    var item = new NativeMenuItem(preset.Key);
                    item.Click += (sender, e) => OnRgbPresetSelected(preset.Key, preset.Value);
                    _rgbPresetItems[preset.Key] = item;
                    submenu.Items.Add(item);
                }

                submenu.Items.Add(new NativeMenuItemSeparator());

                // Effects
                var effectsItem = new NativeMenuItem("Effects");
                var effectsSubmenu = new NativeMenu();

                var effects = new[] { "Static", "Breathing", "Wave", "Rainbow" };
                foreach (var effect in effects)
                {
                    var item = new NativeMenuItem(effect);
                    item.Click += (sender, e) => OnRgbEffectSelected(effect);
                    effectsSubmenu.Items.Add(item);
                }

                effectsItem.Menu = effectsSubmenu;
                submenu.Items.Add(effectsItem);

                rgbMenu.Menu = submenu;
                return rgbMenu;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to create RGB menu: {ex.Message}");
                return null;
            }
        }

        private NativeMenuItem? CreateQuickActionsMenu()
        {
            try
            {
                var quickMenu = new NativeMenuItem("Quick Actions");
                var submenu = new NativeMenu();

                // Battery Conservation Mode
                var conservationItem = new NativeMenuItem("Conservation Mode");
                conservationItem.Click += OnConservationModeClicked;
                submenu.Items.Add(conservationItem);

                // Rapid Charge
                var rapidChargeItem = new NativeMenuItem("Rapid Charge");
                rapidChargeItem.Click += OnRapidChargeClicked;
                submenu.Items.Add(rapidChargeItem);

                submenu.Items.Add(new NativeMenuItemSeparator());

                // Fan Control
                var fanAutoItem = new NativeMenuItem("Auto Fan Control");
                fanAutoItem.Click += OnFanAutoClicked;
                submenu.Items.Add(fanAutoItem);

                var fanMaxItem = new NativeMenuItem("Max Fan Speed");
                fanMaxItem.Click += OnFanMaxClicked;
                submenu.Items.Add(fanMaxItem);

                quickMenu.Menu = submenu;
                return quickMenu;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to create quick actions menu: {ex.Message}");
                return null;
            }
        }

        private NativeMenuItem? CreateToolsMenu()
        {
            try
            {
                var toolsMenu = new NativeMenuItem("Tools");
                var submenu = new NativeMenu();

                // System Info
                var sysInfoItem = new NativeMenuItem("System Information");
                sysInfoItem.Click += OnSystemInfoClicked;
                submenu.Items.Add(sysInfoItem);

                // Temperature Monitor
                var tempMonItem = new NativeMenuItem("Temperature Monitor");
                tempMonItem.Click += OnTempMonitorClicked;
                submenu.Items.Add(tempMonItem);

                submenu.Items.Add(new NativeMenuItemSeparator());

                // Export Logs
                var exportLogsItem = new NativeMenuItem("Export Logs...");
                exportLogsItem.Click += OnExportLogsClicked;
                submenu.Items.Add(exportLogsItem);

                // Check for Updates
                var updateItem = new NativeMenuItem("Check for Updates...");
                updateItem.Click += OnCheckUpdateClicked;
                submenu.Items.Add(updateItem);

                toolsMenu.Menu = submenu;
                return toolsMenu;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to create tools menu: {ex.Message}");
                return null;
            }
        }

        public void UpdateShowHideText(bool isVisible)
        {
            if (_showHideItem != null)
            {
                _showHideItem.Header = isVisible ? "Hide Legion Toolkit" : "Show Legion Toolkit";
            }
        }

        public void UpdatePowerModeSelection(PowerMode mode)
        {
            foreach (var item in _powerModeItems)
            {
                // Note: NativeMenuItem doesn't support checkmarks in all platforms
                // We'll update the text to indicate selection
                var prefix = item.Key == mode ? "● " : "  ";
                item.Value.Header = prefix + GetPowerModeName(item.Key);
            }
        }

        private string GetPowerModeName(PowerMode mode)
        {
            return mode switch
            {
                PowerMode.Quiet => "Quiet",
                PowerMode.Balanced => "Balanced",
                PowerMode.Performance => "Performance",
                PowerMode.Custom => "Custom",
                _ => mode.ToString()
            };
        }

        // Event Handlers
        private void OnShowHideClicked(object? sender, EventArgs e)
        {
            try
            {
                var app = Application.Current;
                if (app?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var mainWindow = desktop.MainWindow;
                    if (mainWindow != null)
                    {
                        if (mainWindow.IsVisible)
                        {
                            mainWindow.Hide();
                        }
                        else
                        {
                            mainWindow.Show();
                            mainWindow.WindowState = WindowState.Normal;
                            mainWindow.Activate();
                        }
                        UpdateShowHideText(mainWindow.IsVisible);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to toggle main window", ex);
            }
        }

        private async void OnPowerModeSelected(PowerMode mode)
        {
            try
            {
                _mainViewModel.NavigateCommand.Execute("Power").Subscribe();
                // TODO: Set power mode through service
                Logger.Info($"Power mode selected from tray: {mode}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set power mode: {ex.Message}", ex);
            }
        }

        private void OnRgbToggleClicked(object? sender, EventArgs e)
        {
            try
            {
                // TODO: Toggle RGB through service
                Logger.Info("RGB toggle clicked from tray");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to toggle RGB: {ex.Message}", ex);
            }
        }

        private void OnRgbPresetSelected(string name, string value)
        {
            try
            {
                // TODO: Apply RGB preset through service
                Logger.Info($"RGB preset selected from tray: {name} = {value}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to apply RGB preset: {ex.Message}", ex);
            }
        }

        private void OnRgbEffectSelected(string effect)
        {
            try
            {
                // TODO: Apply RGB effect through service
                Logger.Info($"RGB effect selected from tray: {effect}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to apply RGB effect: {ex.Message}", ex);
            }
        }

        private void OnConservationModeClicked(object? sender, EventArgs e)
        {
            // TODO: Toggle conservation mode
            Logger.Info("Conservation mode clicked from tray");
        }

        private void OnRapidChargeClicked(object? sender, EventArgs e)
        {
            // TODO: Toggle rapid charge
            Logger.Info("Rapid charge clicked from tray");
        }

        private void OnFanAutoClicked(object? sender, EventArgs e)
        {
            // TODO: Set fan to auto
            Logger.Info("Auto fan clicked from tray");
        }

        private void OnFanMaxClicked(object? sender, EventArgs e)
        {
            // TODO: Set fan to max
            Logger.Info("Max fan clicked from tray");
        }

        private void OnSystemInfoClicked(object? sender, EventArgs e)
        {
            // TODO: Show system info
            Logger.Info("System info clicked from tray");
        }

        private void OnTempMonitorClicked(object? sender, EventArgs e)
        {
            _mainViewModel.NavigateCommand.Execute("Thermal");
        }

        private void OnExportLogsClicked(object? sender, EventArgs e)
        {
            // TODO: Export logs
            Logger.Info("Export logs clicked from tray");
        }

        private void OnCheckUpdateClicked(object? sender, EventArgs e)
        {
            // TODO: Check for updates
            Logger.Info("Check update clicked from tray");
        }

        private void OnSettingsClicked(object? sender, EventArgs e)
        {
            _mainViewModel.NavigateCommand.Execute("Settings");
            OnShowHideClicked(sender, e); // Show window
        }

        private void OnAboutClicked(object? sender, EventArgs e)
        {
            // TODO: Show about dialog
            Logger.Info("About clicked from tray");
        }

        private void OnExitClicked(object? sender, EventArgs e)
        {
            try
            {
                Logger.Info("Exit requested from tray menu");
                var app = Application.Current;
                if (app?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to exit application", ex);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_showHideItem != null)
                {
                    _showHideItem.Click -= OnShowHideClicked;
                }

                _powerModeItems.Clear();
                _rgbPresetItems.Clear();
                _menu = null;
            }
            catch (Exception ex)
            {
                Logger.Error("Error disposing tray menu", ex);
            }
        }
    }
}