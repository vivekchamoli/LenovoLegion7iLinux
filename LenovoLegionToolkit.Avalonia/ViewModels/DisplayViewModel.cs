using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;

namespace LenovoLegionToolkit.Avalonia.ViewModels
{
    public class DisplayViewModel : ViewModelBase, IActivatableViewModel
    {
        private readonly IDisplayService _displayService;

        private DisplayInfo? _selectedDisplay;
        private DisplayConfiguration? _currentConfiguration;
        private Resolution? _selectedResolution;
        private int _selectedRefreshRate;
        private int _brightness = 100;
        private bool _hdrEnabled;
        private bool _nightLightEnabled;
        private int _colorTemperature = 6500;
        private bool _useNightLightSchedule;
        private TimeSpan _nightLightStartTime = new(20, 0, 0);
        private TimeSpan _nightLightEndTime = new(7, 0, 0);
        private string _selectedPreset = string.Empty;
        private string _newPresetName = string.Empty;

        public ViewModelActivator Activator { get; } = new ViewModelActivator();

        public DisplayInfo? SelectedDisplay
        {
            get => _selectedDisplay;
            set => this.RaiseAndSetIfChanged(ref _selectedDisplay, value);
        }

        public DisplayConfiguration? CurrentConfiguration
        {
            get => _currentConfiguration;
            set => this.RaiseAndSetIfChanged(ref _currentConfiguration, value);
        }

        public Resolution? SelectedResolution
        {
            get => _selectedResolution;
            set => this.RaiseAndSetIfChanged(ref _selectedResolution, value);
        }

        public int SelectedRefreshRate
        {
            get => _selectedRefreshRate;
            set => this.RaiseAndSetIfChanged(ref _selectedRefreshRate, value);
        }

        public int Brightness
        {
            get => _brightness;
            set => this.RaiseAndSetIfChanged(ref _brightness, value);
        }

        public bool HdrEnabled
        {
            get => _hdrEnabled;
            set => this.RaiseAndSetIfChanged(ref _hdrEnabled, value);
        }

        public bool NightLightEnabled
        {
            get => _nightLightEnabled;
            set => this.RaiseAndSetIfChanged(ref _nightLightEnabled, value);
        }

        public int ColorTemperature
        {
            get => _colorTemperature;
            set => this.RaiseAndSetIfChanged(ref _colorTemperature, value);
        }

        public bool UseNightLightSchedule
        {
            get => _useNightLightSchedule;
            set => this.RaiseAndSetIfChanged(ref _useNightLightSchedule, value);
        }

        public TimeSpan NightLightStartTime
        {
            get => _nightLightStartTime;
            set => this.RaiseAndSetIfChanged(ref _nightLightStartTime, value);
        }

        public TimeSpan NightLightEndTime
        {
            get => _nightLightEndTime;
            set => this.RaiseAndSetIfChanged(ref _nightLightEndTime, value);
        }

        public string SelectedPreset
        {
            get => _selectedPreset;
            set => this.RaiseAndSetIfChanged(ref _selectedPreset, value);
        }

        public string NewPresetName
        {
            get => _newPresetName;
            set => this.RaiseAndSetIfChanged(ref _newPresetName, value);
        }

        public ObservableCollection<DisplayInfo> Displays { get; }
        public ObservableCollection<Resolution> AvailableResolutions { get; }
        public ObservableCollection<int> AvailableRefreshRates { get; }
        public ObservableCollection<string> DisplayPresets { get; }

        // Commands
        public ReactiveCommand<Unit, Unit> RefreshDisplaysCommand { get; }
        public ReactiveCommand<Unit, Unit> ApplyResolutionCommand { get; }
        public ReactiveCommand<Unit, Unit> ApplyRefreshRateCommand { get; }
        public ReactiveCommand<Unit, Unit> ApplyBrightnessCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleHdrCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleNightLightCommand { get; }
        public ReactiveCommand<Unit, Unit> ApplyNightLightSettingsCommand { get; }
        public ReactiveCommand<string, Unit> LoadPresetCommand { get; }
        public ReactiveCommand<Unit, Unit> SavePresetCommand { get; }
        public ReactiveCommand<string, Unit> DeletePresetCommand { get; }
        public ReactiveCommand<Unit, Unit> ResetToDefaultCommand { get; }
        public ReactiveCommand<Unit, Unit> IdentifyDisplaysCommand { get; }

        public DisplayViewModel(IDisplayService displayService)
        {
            _displayService = displayService ?? throw new ArgumentNullException(nameof(displayService));

            Displays = new ObservableCollection<DisplayInfo>();
            AvailableResolutions = new ObservableCollection<Resolution>();
            AvailableRefreshRates = new ObservableCollection<int>();
            DisplayPresets = new ObservableCollection<string>();

            // Initialize commands
            RefreshDisplaysCommand = ReactiveCommand.CreateFromTask(RefreshDisplaysAsync);
            ApplyResolutionCommand = ReactiveCommand.CreateFromTask(ApplyResolutionAsync);
            ApplyRefreshRateCommand = ReactiveCommand.CreateFromTask(ApplyRefreshRateAsync);
            ApplyBrightnessCommand = ReactiveCommand.CreateFromTask(ApplyBrightnessAsync);
            ToggleHdrCommand = ReactiveCommand.CreateFromTask(ToggleHdrAsync);
            ToggleNightLightCommand = ReactiveCommand.CreateFromTask(ToggleNightLightAsync);
            ApplyNightLightSettingsCommand = ReactiveCommand.CreateFromTask(ApplyNightLightSettingsAsync);
            LoadPresetCommand = ReactiveCommand.CreateFromTask<string>(LoadPresetAsync);
            SavePresetCommand = ReactiveCommand.CreateFromTask(SavePresetAsync);
            DeletePresetCommand = ReactiveCommand.CreateFromTask<string>(DeletePresetAsync);
            ResetToDefaultCommand = ReactiveCommand.CreateFromTask(ResetToDefaultAsync);
            IdentifyDisplaysCommand = ReactiveCommand.CreateFromTask(IdentifyDisplaysAsync);

            // React to property changes
            this.WhenAnyValue(x => x.SelectedDisplay)
                .Where(d => d != null)
                .SelectMany(d => Observable.FromAsync(() => LoadDisplayConfigurationAsync(d!)))
                .Subscribe();

            this.WhenAnyValue(x => x.SelectedResolution)
                .Where(r => r != null)
                .Subscribe(r => UpdateAvailableRefreshRates(r!));

            this.WhenAnyValue(x => x.Brightness)
                .Throttle(TimeSpan.FromMilliseconds(500))
                .Where(_ => SelectedDisplay != null)
                .SelectMany(_ => Observable.FromAsync(ApplyBrightnessAsync))
                .Subscribe();

            this.WhenAnyValue(x => x.ColorTemperature)
                .Throttle(TimeSpan.FromMilliseconds(500))
                .Where(_ => NightLightEnabled)
                .SelectMany(temp => Observable.FromAsync(() =>
                    _displayService.SetNightLightAsync(true, temp)))
                .Subscribe();

            this.WhenActivated(disposables =>
            {
                Observable.FromAsync(InitializeAsync)
                    .Subscribe()
                    .DisposeWith(disposables);

                // Subscribe to display changes
                if (_displayService is INotifyDisplayChanged notifier)
                {
                    Observable.FromEventPattern<DisplayChangedEventArgs>(
                        h => notifier.DisplayChanged += h,
                        h => notifier.DisplayChanged -= h)
                        .Select(e => e.EventArgs)
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(OnDisplayChanged)
                        .DisposeWith(disposables);
                }

                // Auto-refresh displays every 30 seconds
                Observable.Timer(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30))
                    .SelectMany(_ => Observable.FromAsync(RefreshDisplaysAsync))
                    .Subscribe()
                    .DisposeWith(disposables);
            });
        }

        private async Task InitializeAsync()
        {
            await RefreshDisplaysAsync();
            await LoadPresetsAsync();
            await LoadNightLightSettingsAsync();
        }

        private async Task RefreshDisplaysAsync()
        {
            try
            {
                var displays = await _displayService.GetDisplaysAsync();

                Displays.Clear();
                foreach (var display in displays.Where(d => d.IsConnected))
                {
                    Displays.Add(display);
                }

                // Select primary or first display
                SelectedDisplay = Displays.FirstOrDefault(d => d.IsPrimary) ?? Displays.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to refresh displays: {ex.Message}");
            }
        }

        private async Task LoadDisplayConfigurationAsync(DisplayInfo display)
        {
            try
            {
                CurrentConfiguration = await _displayService.GetDisplayConfigurationAsync(display.Id);

                if (CurrentConfiguration != null)
                {
                    // Update available resolutions
                    AvailableResolutions.Clear();
                    foreach (var resolution in display.Capabilities.SupportedResolutions)
                    {
                        AvailableResolutions.Add(resolution);
                    }

                    // Select current resolution
                    SelectedResolution = AvailableResolutions.FirstOrDefault(r =>
                        r.Width == CurrentConfiguration.Width &&
                        r.Height == CurrentConfiguration.Height);

                    // Update other settings
                    SelectedRefreshRate = CurrentConfiguration.RefreshRate;
                    HdrEnabled = CurrentConfiguration.HdrEnabled;
                    Brightness = await _displayService.GetBrightnessAsync(display.Id);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load display configuration: {ex.Message}");
            }
        }

        private void UpdateAvailableRefreshRates(Resolution resolution)
        {
            AvailableRefreshRates.Clear();
            foreach (var rate in resolution.RefreshRates.OrderByDescending(r => r))
            {
                AvailableRefreshRates.Add(rate);
            }

            // Select current or highest refresh rate
            if (!AvailableRefreshRates.Contains(SelectedRefreshRate))
            {
                SelectedRefreshRate = AvailableRefreshRates.FirstOrDefault();
            }
        }

        private async Task ApplyResolutionAsync()
        {
            if (SelectedDisplay == null || SelectedResolution == null)
                return;

            await _displayService.SetResolutionAsync(
                SelectedDisplay.Id,
                SelectedResolution.Width,
                SelectedResolution.Height);
        }

        private async Task ApplyRefreshRateAsync()
        {
            if (SelectedDisplay == null || SelectedRefreshRate <= 0)
                return;

            await _displayService.SetRefreshRateAsync(SelectedDisplay.Id, SelectedRefreshRate);
        }

        private async Task ApplyBrightnessAsync()
        {
            if (SelectedDisplay == null)
                return;

            await _displayService.SetBrightnessAsync(SelectedDisplay.Id, Brightness);
        }

        private async Task ToggleHdrAsync()
        {
            if (SelectedDisplay == null)
                return;

            HdrEnabled = !HdrEnabled;
            await _displayService.SetHdrAsync(SelectedDisplay.Id, HdrEnabled);
        }

        private async Task ToggleNightLightAsync()
        {
            NightLightEnabled = !NightLightEnabled;
            await _displayService.SetNightLightAsync(NightLightEnabled, ColorTemperature);
        }

        private async Task ApplyNightLightSettingsAsync()
        {
            if (UseNightLightSchedule)
            {
                await _displayService.SetNightLightScheduleAsync(NightLightStartTime, NightLightEndTime);
            }
        }

        private async Task LoadNightLightSettingsAsync()
        {
            var settings = await _displayService.GetNightLightSettingsAsync();
            if (settings != null)
            {
                NightLightEnabled = settings.Enabled;
                ColorTemperature = settings.Temperature;
                UseNightLightSchedule = settings.UseSchedule;
                NightLightStartTime = settings.ScheduleStartTime;
                NightLightEndTime = settings.ScheduleEndTime;
            }
        }

        private async Task LoadPresetsAsync()
        {
            var presets = await _displayService.GetAvailablePresetsAsync();
            DisplayPresets.Clear();
            foreach (var preset in presets)
            {
                DisplayPresets.Add(preset);
            }
        }

        private async Task LoadPresetAsync(string presetName)
        {
            await _displayService.ApplyDisplayPresetAsync(presetName);
            await RefreshDisplaysAsync();
        }

        private async Task SavePresetAsync()
        {
            if (string.IsNullOrWhiteSpace(NewPresetName) || CurrentConfiguration == null)
                return;

            await _displayService.SaveDisplayPresetAsync(NewPresetName, CurrentConfiguration);
            await LoadPresetsAsync();
            NewPresetName = string.Empty;
        }

        private async Task DeletePresetAsync(string presetName)
        {
            // TODO: Implement preset deletion
            await LoadPresetsAsync();
        }

        private async Task ResetToDefaultAsync()
        {
            if (SelectedDisplay == null)
                return;

            // Reset to native resolution and refresh rate
            var native = AvailableResolutions.FirstOrDefault(r =>
                r.Width == SelectedDisplay.Capabilities.NativeWidth &&
                r.Height == SelectedDisplay.Capabilities.NativeHeight);

            if (native != null)
            {
                SelectedResolution = native;
                await ApplyResolutionAsync();
            }

            SelectedRefreshRate = SelectedDisplay.Capabilities.NativeRefreshRate;
            await ApplyRefreshRateAsync();

            Brightness = 100;
            await ApplyBrightnessAsync();
        }

        private async Task IdentifyDisplaysAsync()
        {
            // Flash display identification on each monitor
            // This would require creating temporary overlay windows
            await Task.Delay(100);
            Console.WriteLine("Display identification not yet implemented");
        }

        private void OnDisplayChanged(DisplayChangedEventArgs e)
        {
            // Handle display change events
            if (e.ChangeType == DisplayChangeType.Connected || e.ChangeType == DisplayChangeType.Disconnected)
            {
                RefreshDisplaysAsync().Wait();
            }
        }
    }
}