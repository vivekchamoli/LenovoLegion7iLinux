using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using ReactiveUI;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;

namespace LenovoLegionToolkit.Avalonia.ViewModels
{
    public class KeyboardViewModel : ViewModelBase, IActivatableViewModel
    {
        private readonly IKeyboardService _keyboardService;

        private bool _isRgbSupported;
        private bool _isEnabled;
        private int _brightness = 100;
        private RgbKeyboardEffect _selectedEffect;
        private byte _effectSpeed = 5;
        private Color _selectedColor = Colors.Red;
        private RgbKeyboardZone _selectedZone = RgbKeyboardZone.All;
        private RgbKeyboardInfo? _keyboardInfo;
        private RgbKeyboardProfile? _currentProfile;
        private string _selectedProfileName = string.Empty;

        public ViewModelActivator Activator { get; } = new ViewModelActivator();

        public bool IsRgbSupported
        {
            get => _isRgbSupported;
            set => this.RaiseAndSetIfChanged(ref _isRgbSupported, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
        }

        public int Brightness
        {
            get => _brightness;
            set => this.RaiseAndSetIfChanged(ref _brightness, value);
        }

        public RgbKeyboardEffect SelectedEffect
        {
            get => _selectedEffect;
            set => this.RaiseAndSetIfChanged(ref _selectedEffect, value);
        }

        public byte EffectSpeed
        {
            get => _effectSpeed;
            set => this.RaiseAndSetIfChanged(ref _effectSpeed, value);
        }

        public Color SelectedColor
        {
            get => _selectedColor;
            set => this.RaiseAndSetIfChanged(ref _selectedColor, value);
        }

        public RgbKeyboardZone SelectedZone
        {
            get => _selectedZone;
            set => this.RaiseAndSetIfChanged(ref _selectedZone, value);
        }

        public RgbKeyboardInfo? KeyboardInfo
        {
            get => _keyboardInfo;
            set => this.RaiseAndSetIfChanged(ref _keyboardInfo, value);
        }

        public RgbKeyboardProfile? CurrentProfile
        {
            get => _currentProfile;
            set => this.RaiseAndSetIfChanged(ref _currentProfile, value);
        }

        public string SelectedProfileName
        {
            get => _selectedProfileName;
            set => this.RaiseAndSetIfChanged(ref _selectedProfileName, value);
        }

        public ObservableCollection<RgbKeyboardEffect> AvailableEffects { get; }
        public ObservableCollection<RgbKeyboardZone> AvailableZones { get; }
        public ObservableCollection<string> SavedProfiles { get; }
        public ObservableCollection<PresetColor> PresetColors { get; }

        // Commands
        public ReactiveCommand<Unit, Unit> ToggleCommand { get; }
        public ReactiveCommand<Unit, Unit> ApplyColorCommand { get; }
        public ReactiveCommand<RgbKeyboardEffect, Unit> ApplyEffectCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveProfileCommand { get; }
        public ReactiveCommand<string, Unit> LoadProfileCommand { get; }
        public ReactiveCommand<string, Unit> DeleteProfileCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        public ReactiveCommand<PresetColor, Unit> ApplyPresetCommand { get; }

        public KeyboardViewModel(IKeyboardService keyboardService)
        {
            _keyboardService = keyboardService ?? throw new ArgumentNullException(nameof(keyboardService));

            AvailableEffects = new ObservableCollection<RgbKeyboardEffect>();
            AvailableZones = new ObservableCollection<RgbKeyboardZone>();
            SavedProfiles = new ObservableCollection<string>();
            PresetColors = new ObservableCollection<PresetColor>
            {
                new PresetColor("Legion Red", Color.FromRgb(220, 38, 38)),
                new PresetColor("Blue", Color.FromRgb(0, 0, 255)),
                new PresetColor("Green", Color.FromRgb(0, 255, 0)),
                new PresetColor("Purple", Color.FromRgb(128, 0, 128)),
                new PresetColor("Cyan", Color.FromRgb(0, 255, 255)),
                new PresetColor("Yellow", Color.FromRgb(255, 255, 0)),
                new PresetColor("Orange", Color.FromRgb(255, 165, 0)),
                new PresetColor("Pink", Color.FromRgb(255, 192, 203)),
                new PresetColor("White", Color.FromRgb(255, 255, 255))
            };

            // Initialize commands
            ToggleCommand = ReactiveCommand.CreateFromTask(ToggleAsync);
            ApplyColorCommand = ReactiveCommand.CreateFromTask(ApplyColorAsync);
            ApplyEffectCommand = ReactiveCommand.CreateFromTask<RgbKeyboardEffect>(ApplyEffectAsync);
            SaveProfileCommand = ReactiveCommand.CreateFromTask(SaveProfileAsync);
            LoadProfileCommand = ReactiveCommand.CreateFromTask<string>(LoadProfileAsync);
            DeleteProfileCommand = ReactiveCommand.CreateFromTask<string>(DeleteProfileAsync);
            RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
            ApplyPresetCommand = ReactiveCommand.CreateFromTask<PresetColor>(ApplyPresetColorAsync);

            // React to property changes
            this.WhenAnyValue(x => x.Brightness)
                .Throttle(TimeSpan.FromMilliseconds(500))
                .Where(_ => IsEnabled)
                .SelectMany(brightness => Observable.FromAsync(() => _keyboardService.SetBrightnessAsync(brightness)))
                .Subscribe();

            this.WhenAnyValue(x => x.EffectSpeed)
                .Throttle(TimeSpan.FromMilliseconds(500))
                .Where(_ => IsEnabled && SelectedEffect != RgbKeyboardEffect.Static)
                .SelectMany(speed => Observable.FromAsync(() => _keyboardService.SetEffectAsync(SelectedEffect, speed)))
                .Subscribe();

            this.WhenActivated(disposables =>
            {
                Observable.FromAsync(InitializeAsync)
                    .Subscribe()
                    .DisposeWith(disposables);

                // Subscribe to keyboard state changes
                if (_keyboardService is INotifyKeyboardStateChanged notifier)
                {
                    Observable.FromEventPattern<RgbKeyboardState>(
                        h => notifier.KeyboardStateChanged += h,
                        h => notifier.KeyboardStateChanged -= h)
                        .Select(e => e.EventArgs)
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(UpdateFromState)
                        .DisposeWith(disposables);
                }
            });
        }

        private async Task InitializeAsync()
        {
            try
            {
                IsRgbSupported = await _keyboardService.IsRgbSupportedAsync();

                if (!IsRgbSupported)
                    return;

                KeyboardInfo = await _keyboardService.GetKeyboardInfoAsync();

                if (KeyboardInfo != null)
                {
                    // Populate available effects
                    AvailableEffects.Clear();
                    foreach (var effect in KeyboardInfo.SupportedEffects)
                    {
                        AvailableEffects.Add(effect);
                    }

                    // Populate available zones
                    AvailableZones.Clear();
                    foreach (var zone in KeyboardInfo.SupportedZones)
                    {
                        AvailableZones.Add(zone);
                    }
                }

                // Load current state
                var state = await _keyboardService.GetCurrentStateAsync();
                if (state != null)
                {
                    UpdateFromState(state);
                }

                // Load saved profiles
                await LoadSavedProfilesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize keyboard: {ex.Message}");
            }
        }

        private void UpdateFromState(RgbKeyboardState state)
        {
            IsEnabled = state.IsOn;
            Brightness = state.Brightness;
            SelectedEffect = state.CurrentEffect;
            EffectSpeed = state.EffectSpeed;

            // Update color from first zone
            if (state.ZoneColors.TryGetValue(SelectedZone, out var color))
            {
                SelectedColor = Color.FromRgb(color.R, color.G, color.B);
            }
        }

        private async Task ToggleAsync()
        {
            if (IsEnabled)
            {
                await _keyboardService.TurnOffAsync();
            }
            else
            {
                await _keyboardService.TurnOnAsync();
            }
            IsEnabled = !IsEnabled;
        }

        private async Task ApplyColorAsync()
        {
            var r = SelectedColor.R;
            var g = SelectedColor.G;
            var b = SelectedColor.B;

            await _keyboardService.SetStaticColorAsync(r, g, b, SelectedZone);
            SelectedEffect = RgbKeyboardEffect.Static;
        }

        private async Task ApplyEffectAsync(RgbKeyboardEffect effect)
        {
            await _keyboardService.SetEffectAsync(effect, EffectSpeed);
            SelectedEffect = effect;
        }

        private async Task ApplyPresetColorAsync(PresetColor preset)
        {
            SelectedColor = preset.Color;
            await ApplyColorAsync();
        }

        private async Task SaveProfileAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedProfileName))
            {
                // TODO: Show dialog to get profile name
                return;
            }

            var profile = new RgbKeyboardProfile
            {
                Name = SelectedProfileName,
                Effect = SelectedEffect,
                Brightness = Brightness,
                EffectSpeed = EffectSpeed
            };

            // Add zone colors
            foreach (var zone in AvailableZones)
            {
                profile.ZoneColors[zone] = new RgbColor(SelectedColor.R, SelectedColor.G, SelectedColor.B);
            }

            await _keyboardService.SaveProfileAsync(SelectedProfileName, profile);
            await LoadSavedProfilesAsync();
        }

        private async Task LoadProfileAsync(string profileName)
        {
            var profile = await _keyboardService.LoadProfileAsync(profileName);
            if (profile == null)
                return;

            CurrentProfile = profile;
            SelectedProfileName = profileName;

            // Apply profile settings
            await _keyboardService.SetBrightnessAsync(profile.Brightness);
            await _keyboardService.SetEffectAsync(profile.Effect, profile.EffectSpeed);

            // Apply zone colors
            foreach (var kvp in profile.ZoneColors)
            {
                await _keyboardService.SetStaticColorAsync(kvp.Value.R, kvp.Value.G, kvp.Value.B, kvp.Key);
            }

            // Update UI
            Brightness = profile.Brightness;
            SelectedEffect = profile.Effect;
            EffectSpeed = profile.EffectSpeed;

            if (profile.ZoneColors.TryGetValue(SelectedZone, out var color))
            {
                SelectedColor = Color.FromRgb(color.R, color.G, color.B);
            }
        }

        private async Task DeleteProfileAsync(string profileName)
        {
            // TODO: Implement profile deletion
            await LoadSavedProfilesAsync();
        }

        private async Task LoadSavedProfilesAsync()
        {
            var profiles = await _keyboardService.GetAvailableProfilesAsync();
            SavedProfiles.Clear();
            foreach (var profile in profiles)
            {
                SavedProfiles.Add(profile);
            }
        }

        private async Task RefreshAsync()
        {
            await InitializeAsync();
        }

        public class PresetColor
        {
            public string Name { get; }
            public Color Color { get; }

            public PresetColor(string name, Color color)
            {
                Name = name;
                Color = color;
            }
        }
    }
}