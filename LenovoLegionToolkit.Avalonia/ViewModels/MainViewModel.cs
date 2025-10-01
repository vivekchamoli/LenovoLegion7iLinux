using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Settings;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ISettingsService _settingsService;
        private readonly IHardwareService _hardwareService;

        private ViewModelBase? _currentPage;
        private NavigationItem? _selectedNavigationItem;
        private bool _isInitialized;
        private string _windowTitle = "Legion Toolkit for Linux";
        private string _statusText = "Ready";
        private bool _isBusy;

        public ViewModelBase? CurrentPage
        {
            get => _currentPage;
            set => this.RaiseAndSetIfChanged(ref _currentPage, value);
        }

        public NavigationItem? SelectedNavigationItem
        {
            get => _selectedNavigationItem;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedNavigationItem, value);
                if (value != null)
                {
                    NavigateToPage(value.PageType);
                }
            }
        }

        public string WindowTitle
        {
            get => _windowTitle;
            set => this.RaiseAndSetIfChanged(ref _windowTitle, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => this.RaiseAndSetIfChanged(ref _statusText, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => this.RaiseAndSetIfChanged(ref _isBusy, value);
        }

        public ObservableCollection<NavigationItem> NavigationItems { get; }

        // Commands
        public ReactiveCommand<Unit, Unit> InitializeCommand { get; }
        public ReactiveCommand<string, Unit> NavigateCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowSettingsCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowAboutCommand { get; }
        public ReactiveCommand<Unit, Unit> ExitCommand { get; }

        public MainViewModel(
            IServiceProvider serviceProvider,
            ISettingsService settingsService,
            IHardwareService hardwareService)
        {
            _serviceProvider = serviceProvider;
            _settingsService = settingsService;
            _hardwareService = hardwareService;

            // Initialize navigation items
            NavigationItems = new ObservableCollection<NavigationItem>
            {
                new() { Name = "Dashboard", Icon = "🏠", PageType = "Dashboard", IsEnabled = true },
                new() { Name = "Power", Icon = "⚡", PageType = "Power", IsEnabled = true },
                new() { Name = "Battery", Icon = "🔋", PageType = "Battery", IsEnabled = true },
                new() { Name = "GPU", Icon = "🎮", PageType = "Gpu", IsEnabled = true },
                new() { Name = "Thermal", Icon = "🌡️", PageType = "Thermal", IsEnabled = true },
                new() { Name = "Keyboard", Icon = "⌨️", PageType = "Keyboard", IsEnabled = true },
                new() { Name = "Display", Icon = "🖥️", PageType = "Display", IsEnabled = true },
                new() { Name = "Automation", Icon = "🤖", PageType = "Automation", IsEnabled = true },
                new() { Name = "Settings", Icon = "⚙️", PageType = "Settings", IsEnabled = true }
            };

            // Initialize commands
            InitializeCommand = ReactiveCommand.CreateFromTask(InitializeAsync);
            NavigateCommand = ReactiveCommand.Create<string>(NavigateToPage);
            RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
            ShowSettingsCommand = ReactiveCommand.Create(() => NavigateToPage("Settings"));
            ShowAboutCommand = ReactiveCommand.Create(() => NavigateToPage("About"));
            ExitCommand = ReactiveCommand.Create(Exit);

            // Load initial page
            NavigateToPage("Dashboard");
        }

        public override async void Initialize()
        {
            base.Initialize();

            if (!_isInitialized)
            {
                await InitializeAsync();
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                Logger.Info("Starting MainViewModel initialization");
                IsBusy = true;
                StatusText = "Initializing...";

                try
                {
                    // Load settings
                    Logger.Info("Loading settings...");
                    await _settingsService.LoadSettingsAsync();
                    Logger.Info("Settings loaded");
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to load settings", ex);
                    // Continue anyway
                }

                try
                {
                    // Get hardware info
                    Logger.Info("Getting hardware info...");
                    var hardwareInfo = await _hardwareService.GetHardwareInfoAsync();
                    WindowTitle = $"Legion Toolkit - {hardwareInfo.Model}";
                    Logger.Info($"Hardware info loaded: {hardwareInfo.Model}");
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to get hardware info", ex);
                    WindowTitle = "Legion Toolkit for Linux";
                }

                try
                {
                    // Check capabilities and update navigation
                    Logger.Info("Detecting hardware capabilities...");
                    var capabilities = await _hardwareService.DetectCapabilitiesAsync();
                    UpdateNavigationBasedOnCapabilities(capabilities);
                    Logger.Info("Capabilities detected");
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to detect capabilities", ex);
                    // Continue with default navigation
                }

                try
                {
                    // Apply settings
                    Logger.Info("Applying settings...");
                    ApplySettings();
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to apply settings", ex);
                }

                _isInitialized = true;
                StatusText = "Ready";

                Logger.Info("Main window initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize main window", ex);
                StatusText = "Initialization failed - some features may be unavailable";
                // Don't call SetError - it might not exist yet
                _isInitialized = true; // Mark as initialized anyway so window shows
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void NavigateToPage(string pageType)
        {
            try
            {
                Logger.Debug($"Navigating to page: {pageType}");

                ViewModelBase? page = pageType switch
                {
                    "Dashboard" => _serviceProvider.GetService(typeof(DashboardViewModel)) as DashboardViewModel,
                    "Settings" => _serviceProvider.GetService(typeof(SettingsViewModel)) as SettingsViewModel,
                    "Power" => _serviceProvider.GetService(typeof(PowerViewModel)) as PowerViewModel,
                    "Battery" => _serviceProvider.GetService(typeof(BatteryViewModel)) as BatteryViewModel,
                    "Gpu" => _serviceProvider.GetService(typeof(GpuViewModel)) as GpuViewModel,
                    "Thermal" => _serviceProvider.GetService(typeof(ThermalViewModel)) as ThermalViewModel,
                    "Keyboard" => _serviceProvider.GetService(typeof(KeyboardViewModel)) as KeyboardViewModel,
                    "Display" => _serviceProvider.GetService(typeof(DisplayViewModel)) as DisplayViewModel,
                    "Automation" => _serviceProvider.GetService(typeof(AutomationViewModel)) as AutomationViewModel,
                    // Add other pages as they are implemented
                    _ => null
                };

                if (page != null)
                {
                    // Clean up previous page
                    CurrentPage?.Cleanup();

                    CurrentPage = page;
                    page.Initialize();

                    StatusText = $"Viewing {pageType}";
                }
                else
                {
                    Logger.Warning($"Page not implemented: {pageType}");
                    StatusText = $"Page {pageType} is not yet available";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to navigate to {pageType}", ex);
                SetError($"Navigation failed: {ex.Message}");
            }
        }

        private async Task RefreshAsync()
        {
            try
            {
                IsBusy = true;
                StatusText = "Refreshing...";

                // Refresh current page
                if (CurrentPage != null)
                {
                    CurrentPage.Cleanup();
                    CurrentPage.Initialize();
                }

                StatusText = "Refreshed";
            }
            catch (Exception ex)
            {
                Logger.Error("Refresh failed", ex);
                SetError($"Refresh failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void UpdateNavigationBasedOnCapabilities(Models.DeviceCapabilities capabilities)
        {
            foreach (var navItem in NavigationItems)
            {
                navItem.IsEnabled = navItem.PageType switch
                {
                    "Dashboard" or "Settings" => true,
                    "Power" => capabilities.Features.ContainsKey(Models.HardwareFeature.PowerModeControl) && capabilities.Features[Models.HardwareFeature.PowerModeControl],
                    "Battery" => capabilities.HasBattery,
                    "Gpu" => (capabilities.Features.ContainsKey(Models.HardwareFeature.DiscreteGpuControl) && capabilities.Features[Models.HardwareFeature.DiscreteGpuControl]) ||
                             (capabilities.Features.ContainsKey(Models.HardwareFeature.HybridMode) && capabilities.Features[Models.HardwareFeature.HybridMode]),
                    "Thermal" => capabilities.Features.ContainsKey(Models.HardwareFeature.ThermalMonitoring) && capabilities.Features[Models.HardwareFeature.ThermalMonitoring],
                    "Display" => capabilities.Features.ContainsKey(Models.HardwareFeature.DisplayRefreshRate) && capabilities.Features[Models.HardwareFeature.DisplayRefreshRate],
                    "Keyboard" => capabilities.HasKeyboardBacklight,
                    "Automation" => _settingsService.Settings.Automation.Enabled,
                    _ => false
                };
            }
        }

        private void ApplySettings()
        {
            var settings = _settingsService.Settings;

            // Apply UI settings
            // Theme and other UI settings would be applied here

            // Update status bar visibility
            // This would be bound to the view

            Logger.Debug("Settings applied to main window");
        }

        private void Exit()
        {
            Logger.Info("Application exit requested");
            // The actual exit would be handled by the view/application
        }

        public override void Cleanup()
        {
            CurrentPage?.Cleanup();
            base.Cleanup();
        }
    }

    public class NavigationItem : ReactiveObject
    {
        private string _name = string.Empty;
        private string _icon = string.Empty;
        private string _pageType = string.Empty;
        private bool _isEnabled = true;
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

        public string PageType
        {
            get => _pageType;
            set => this.RaiseAndSetIfChanged(ref _pageType, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => this.RaiseAndSetIfChanged(ref _isSelected, value);
        }
    }
}