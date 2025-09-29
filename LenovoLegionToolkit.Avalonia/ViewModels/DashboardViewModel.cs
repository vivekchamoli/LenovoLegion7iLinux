using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Settings;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly IPowerModeService _powerModeService;
        private readonly IBatteryService _batteryService;
        private readonly IThermalService _thermalService;
        private readonly IHardwareService _hardwareService;
        private readonly ISettingsService _settingsService;

        private BatteryInfo? _batteryInfo;
        private ThermalInfo? _thermalInfo;
        private HardwareInfo? _hardwareInfo;
        private PowerMode _currentPowerMode;
        private bool _isRefreshing;

        // Dashboard-specific properties for binding
        private double _cpuTemperature;
        private double _gpuTemperature;
        private int _batteryLevel;
        private string _batteryStatus = "Unknown";
        private string _batteryHealth = "--";
        private int _batteryCycles;
        private string _batteryTimeRemaining = "--";
        private bool _isConservationModeEnabled;
        private bool _isRapidChargeModeEnabled;
        private int _cpuFanSpeed;
        private int _gpuFanSpeed;
        private string _currentPowerModeDisplay = "Balanced";
        private string _systemModel = "Unknown";
        private string _biosVersion = "--";
        private string _kernelVersion = "--";
        private string _legionModuleStatus = "Not Loaded";

        // Quick Actions
        private bool _isKeyboardBacklightEnabled;
        private bool _isTouchpadLocked;
        private bool _isFnLockEnabled;
        private bool _isHybridModeEnabled;

        public BatteryInfo? BatteryInfo
        {
            get => _batteryInfo;
            set => this.RaiseAndSetIfChanged(ref _batteryInfo, value);
        }

        public ThermalInfo? ThermalInfo
        {
            get => _thermalInfo;
            set => this.RaiseAndSetIfChanged(ref _thermalInfo, value);
        }

        public HardwareInfo? HardwareInfo
        {
            get => _hardwareInfo;
            set => this.RaiseAndSetIfChanged(ref _hardwareInfo, value);
        }

        public PowerMode CurrentPowerMode
        {
            get => _currentPowerMode;
            set => this.RaiseAndSetIfChanged(ref _currentPowerMode, value);
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => this.RaiseAndSetIfChanged(ref _isRefreshing, value);
        }

        // Dashboard Properties
        public double CpuTemperature
        {
            get => _cpuTemperature;
            set => this.RaiseAndSetIfChanged(ref _cpuTemperature, value);
        }

        public double GpuTemperature
        {
            get => _gpuTemperature;
            set => this.RaiseAndSetIfChanged(ref _gpuTemperature, value);
        }

        public int BatteryLevel
        {
            get => _batteryLevel;
            set => this.RaiseAndSetIfChanged(ref _batteryLevel, value);
        }

        public string BatteryStatus
        {
            get => _batteryStatus;
            set => this.RaiseAndSetIfChanged(ref _batteryStatus, value);
        }

        public string BatteryHealth
        {
            get => _batteryHealth;
            set => this.RaiseAndSetIfChanged(ref _batteryHealth, value);
        }

        public int BatteryCycles
        {
            get => _batteryCycles;
            set => this.RaiseAndSetIfChanged(ref _batteryCycles, value);
        }

        public string BatteryTimeRemaining
        {
            get => _batteryTimeRemaining;
            set => this.RaiseAndSetIfChanged(ref _batteryTimeRemaining, value);
        }

        public bool IsConservationModeEnabled
        {
            get => _isConservationModeEnabled;
            set => this.RaiseAndSetIfChanged(ref _isConservationModeEnabled, value);
        }

        public bool IsRapidChargeModeEnabled
        {
            get => _isRapidChargeModeEnabled;
            set => this.RaiseAndSetIfChanged(ref _isRapidChargeModeEnabled, value);
        }

        public int CpuFanSpeed
        {
            get => _cpuFanSpeed;
            set => this.RaiseAndSetIfChanged(ref _cpuFanSpeed, value);
        }

        public int GpuFanSpeed
        {
            get => _gpuFanSpeed;
            set => this.RaiseAndSetIfChanged(ref _gpuFanSpeed, value);
        }

        public string CurrentPowerModeDisplay
        {
            get => _currentPowerModeDisplay;
            set => this.RaiseAndSetIfChanged(ref _currentPowerModeDisplay, value);
        }

        public string SystemModel
        {
            get => _systemModel;
            set => this.RaiseAndSetIfChanged(ref _systemModel, value);
        }

        public string BiosVersion
        {
            get => _biosVersion;
            set => this.RaiseAndSetIfChanged(ref _biosVersion, value);
        }

        public string KernelVersion
        {
            get => _kernelVersion;
            set => this.RaiseAndSetIfChanged(ref _kernelVersion, value);
        }

        public string LegionModuleStatus
        {
            get => _legionModuleStatus;
            set => this.RaiseAndSetIfChanged(ref _legionModuleStatus, value);
        }

        // Quick Actions
        public bool IsKeyboardBacklightEnabled
        {
            get => _isKeyboardBacklightEnabled;
            set => this.RaiseAndSetIfChanged(ref _isKeyboardBacklightEnabled, value);
        }

        public bool IsTouchpadLocked
        {
            get => _isTouchpadLocked;
            set => this.RaiseAndSetIfChanged(ref _isTouchpadLocked, value);
        }

        public bool IsFnLockEnabled
        {
            get => _isFnLockEnabled;
            set => this.RaiseAndSetIfChanged(ref _isFnLockEnabled, value);
        }

        public bool IsHybridModeEnabled
        {
            get => _isHybridModeEnabled;
            set => this.RaiseAndSetIfChanged(ref _isHybridModeEnabled, value);
        }

        public ObservableCollection<FeatureGroup> FeatureGroups { get; }
        public ObservableCollection<FeatureCard> QuickActions { get; }
        public ObservableCollection<StatusItem> StatusItems { get; }

        // Commands
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        public ReactiveCommand<FeatureCard, Unit> ToggleFeatureCommand { get; }
        public ReactiveCommand<FeatureCard, Unit> ExecuteActionCommand { get; }

        // Power Mode Commands
        public ReactiveCommand<PowerMode, Unit> SetPowerModeCommand { get; }
        public ReactiveCommand<Unit, Unit> ApplyPowerModeCommand { get; }

        // Battery Commands
        public ReactiveCommand<Unit, Unit> ToggleConservationModeCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleRapidChargeCommand { get; }

        // Quick Action Commands
        public ReactiveCommand<Unit, Unit> ToggleKeyboardBacklightCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleTouchpadLockCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleFnLockCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleHybridModeCommand { get; }

        public DashboardViewModel(
            IPowerModeService powerModeService,
            IBatteryService batteryService,
            IThermalService thermalService,
            IHardwareService hardwareService,
            ISettingsService settingsService)
        {
            _powerModeService = powerModeService;
            _batteryService = batteryService;
            _thermalService = thermalService;
            _hardwareService = hardwareService;
            _settingsService = settingsService;

            Title = "Dashboard";

            FeatureGroups = new ObservableCollection<FeatureGroup>();
            QuickActions = new ObservableCollection<FeatureCard>();
            StatusItems = new ObservableCollection<StatusItem>();

            // Initialize commands
            RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
            ToggleFeatureCommand = ReactiveCommand.CreateFromTask<FeatureCard>(ToggleFeatureAsync);
            ExecuteActionCommand = ReactiveCommand.CreateFromTask<FeatureCard>(ExecuteActionAsync);

            // Power Mode Commands
            SetPowerModeCommand = ReactiveCommand.CreateFromTask<PowerMode>(async mode =>
            {
                var success = await _powerModeService.SetPowerModeAsync(mode);
                if (success)
                {
                    CurrentPowerMode = mode;
                    CurrentPowerModeDisplay = mode.GetDescription();
                    SetStatus($"Power mode changed to {mode}");
                }
            });
            ApplyPowerModeCommand = ReactiveCommand.CreateFromTask(async () => await RefreshAsync());

            // Battery Commands
            ToggleConservationModeCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var newState = !IsConservationModeEnabled;
                var success = await _batteryService.SetConservationModeAsync(newState);
                if (success)
                {
                    IsConservationModeEnabled = newState;
                    SetStatus($"Conservation mode {(newState ? "enabled" : "disabled")}");
                }
            });

            ToggleRapidChargeCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var newState = !IsRapidChargeModeEnabled;
                var success = await _batteryService.SetRapidChargeModeAsync(newState);
                if (success)
                {
                    IsRapidChargeModeEnabled = newState;
                    SetStatus($"Rapid charge {(newState ? "enabled" : "disabled")}");
                }
            });

            // Quick Action Commands
            ToggleKeyboardBacklightCommand = ReactiveCommand.Create(() =>
            {
                IsKeyboardBacklightEnabled = !IsKeyboardBacklightEnabled;
                SetStatus($"Keyboard backlight {(IsKeyboardBacklightEnabled ? "enabled" : "disabled")}");
            });

            ToggleTouchpadLockCommand = ReactiveCommand.Create(() =>
            {
                IsTouchpadLocked = !IsTouchpadLocked;
                SetStatus($"Touchpad {(IsTouchpadLocked ? "locked" : "unlocked")}");
            });

            ToggleFnLockCommand = ReactiveCommand.Create(() =>
            {
                IsFnLockEnabled = !IsFnLockEnabled;
                SetStatus($"Fn lock {(IsFnLockEnabled ? "enabled" : "disabled")}");
            });

            ToggleHybridModeCommand = ReactiveCommand.Create(() =>
            {
                IsHybridModeEnabled = !IsHybridModeEnabled;
                SetStatus($"Hybrid mode {(IsHybridModeEnabled ? "enabled" : "disabled")} (restart required)");
            });

            // Subscribe to service events
            _powerModeService.PowerModeChanged += OnPowerModeChanged;
            _batteryService.BatteryInfoChanged += OnBatteryInfoChanged;
            _thermalService.ThermalInfoUpdated += OnThermalInfoUpdated;
        }

        public override async void Initialize()
        {
            base.Initialize();
            await InitializeFeaturesAsync();
            await RefreshAsync();
        }

        private async Task InitializeFeaturesAsync()
        {
            try
            {
                IsLoading = true;

                var capabilities = await _hardwareService.DetectCapabilitiesAsync();

                // Clear existing features
                FeatureGroups.Clear();
                QuickActions.Clear();

                // Power & Performance group
                var powerGroup = new FeatureGroup { Name = "Power & Performance" };

                if (capabilities.Features.ContainsKey(HardwareFeature.PowerModeControl) && capabilities.Features[HardwareFeature.PowerModeControl])
                {
                    var powerModeCard = new FeatureCard
                    {
                        Title = "Power Mode",
                        Description = "Switch between power profiles",
                        Icon = "⚡",
                        Feature = HardwareFeature.PowerModeControl,
                        Type = FeatureCardType.Dropdown,
                        Options = new ObservableCollection<string> { "Quiet", "Balanced", "Performance", "Custom" },
                        OnOptionSelected = async (card, option) => await SetPowerModeAsync(option)
                    };
                    powerGroup.Features.Add(powerModeCard);
                }

                if (powerGroup.Features.Any())
                    FeatureGroups.Add(powerGroup);

                // Battery group
                if (capabilities.HasBattery)
                {
                    var batteryGroup = new FeatureGroup { Name = "Battery" };

                    if (capabilities.Features.ContainsKey(HardwareFeature.BatteryConservation) && capabilities.Features[HardwareFeature.BatteryConservation])
                    {
                        var conservationCard = new FeatureCard
                        {
                            Title = "Conservation Mode",
                            Description = "Limit charge to preserve battery",
                            Icon = "🔋",
                            Feature = HardwareFeature.BatteryConservation,
                            Type = FeatureCardType.Toggle,
                            OnToggled = async (card) => await ToggleConservationModeAsync(card)
                        };
                        batteryGroup.Features.Add(conservationCard);
                    }

                    if (capabilities.Features.ContainsKey(HardwareFeature.RapidCharge) && capabilities.Features[HardwareFeature.RapidCharge])
                    {
                        var rapidChargeCard = new FeatureCard
                        {
                            Title = "Rapid Charge",
                            Description = "Enable faster charging",
                            Icon = "⚡",
                            Feature = HardwareFeature.RapidCharge,
                            Type = FeatureCardType.Toggle,
                            OnToggled = async (card) => await ToggleRapidChargeAsync(card)
                        };
                        batteryGroup.Features.Add(rapidChargeCard);
                    }

                    if (batteryGroup.Features.Any())
                        FeatureGroups.Add(batteryGroup);
                }

                // Thermal group
                if (capabilities.Features.ContainsKey(HardwareFeature.ThermalMonitoring) && capabilities.Features[HardwareFeature.ThermalMonitoring])
                {
                    var thermalGroup = new FeatureGroup { Name = "Thermal" };

                    var cpuTempCard = new FeatureCard
                    {
                        Title = "CPU Temperature",
                        Description = "Current CPU temperature",
                        Icon = "🌡️",
                        Feature = HardwareFeature.ThermalMonitoring,
                        Type = FeatureCardType.Display,
                        Status = "--°C"
                    };
                    thermalGroup.Features.Add(cpuTempCard);

                    var gpuTempCard = new FeatureCard
                    {
                        Title = "GPU Temperature",
                        Description = "Current GPU temperature",
                        Icon = "🌡️",
                        Feature = HardwareFeature.ThermalMonitoring,
                        Type = FeatureCardType.Display,
                        Status = "--°C"
                    };
                    thermalGroup.Features.Add(gpuTempCard);

                    if (capabilities.Features.ContainsKey(HardwareFeature.FanControl) && capabilities.Features[HardwareFeature.FanControl])
                    {
                        var fanCard = new FeatureCard
                        {
                            Title = "Fan Speed",
                            Description = "Current fan speeds",
                            Icon = "💨",
                            Feature = HardwareFeature.FanControl,
                            Type = FeatureCardType.Display,
                            Status = "--RPM"
                        };
                        thermalGroup.Features.Add(fanCard);
                    }

                    if (thermalGroup.Features.Any())
                        FeatureGroups.Add(thermalGroup);
                }

                // Quick Actions
                QuickActions.Add(new FeatureCard
                {
                    Title = "Refresh",
                    Description = "Refresh all data",
                    Icon = "🔄",
                    Type = FeatureCardType.Button,
                    OnToggled = async (card) => await RefreshAsync()
                });

                Logger.Info($"Dashboard initialized with {FeatureGroups.Sum(g => g.Features.Count)} features");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize dashboard features", ex);
                SetError("Failed to initialize features");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshAsync()
        {
            try
            {
                IsRefreshing = true;

                // Update power mode
                CurrentPowerMode = await _powerModeService.GetCurrentPowerModeAsync();
                CurrentPowerModeDisplay = CurrentPowerMode.GetDescription();
                UpdatePowerModeCard();

                // Update battery info
                BatteryInfo = await _batteryService.GetBatteryInfoAsync();
                if (BatteryInfo != null)
                {
                    BatteryLevel = BatteryInfo.ChargeLevel;
                    BatteryStatus = BatteryInfo.Status;
                    BatteryHealth = $"{BatteryInfo.Health:F0}%";
                    BatteryCycles = BatteryInfo.CycleCount;

                    if (BatteryInfo.TimeToFull.HasValue)
                        BatteryTimeRemaining = $"{BatteryInfo.TimeToFull.Value.TotalHours:F1}h to full";
                    else if (BatteryInfo.TimeToEmpty.HasValue)
                        BatteryTimeRemaining = $"{BatteryInfo.TimeToEmpty.Value.TotalHours:F1}h remaining";
                    else
                        BatteryTimeRemaining = "--";
                }

                var batteryMode = await _batteryService.GetBatteryModeAsync();
                IsConservationModeEnabled = batteryMode.ConservationMode;
                IsRapidChargeModeEnabled = batteryMode.RapidChargeMode;
                UpdateBatteryCards(batteryMode);

                // Update thermal info
                ThermalInfo = await _thermalService.GetThermalInfoAsync();
                if (ThermalInfo != null)
                {
                    CpuTemperature = ThermalInfo.CpuTemperature;
                    GpuTemperature = ThermalInfo.GpuTemperature;

                    var cpuFan = ThermalInfo.Fans.FirstOrDefault(f => f.Name.Contains("CPU"));
                    var gpuFan = ThermalInfo.Fans.FirstOrDefault(f => f.Name.Contains("GPU"));

                    CpuFanSpeed = cpuFan?.CurrentSpeed ?? 0;
                    GpuFanSpeed = gpuFan?.CurrentSpeed ?? 0;
                }
                UpdateThermalCards();

                // Update hardware info
                HardwareInfo = await _hardwareService.GetHardwareInfoAsync();
                if (HardwareInfo != null)
                {
                    SystemModel = HardwareInfo.Model;
                    BiosVersion = HardwareInfo.BiosVersion;
                    KernelVersion = HardwareInfo.KernelVersion;
                    LegionModuleStatus = HardwareInfo.HasLegionKernelModule ? "Loaded" : "Not Loaded";
                }

                // Update status items
                UpdateStatusItems();

                SetStatus("Data refreshed");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to refresh dashboard", ex);
                SetError("Refresh failed");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private void UpdatePowerModeCard()
        {
            var powerCard = FeatureGroups
                .SelectMany(g => g.Features)
                .FirstOrDefault(f => f.Feature == HardwareFeature.PowerModeControl);

            if (powerCard != null)
            {
                powerCard.SelectedOption = CurrentPowerMode.ToString();
                powerCard.Status = CurrentPowerMode.GetDescription();
            }
        }

        private void UpdateBatteryCards(BatteryMode mode)
        {
            var conservationCard = FeatureGroups
                .SelectMany(g => g.Features)
                .FirstOrDefault(f => f.Feature == HardwareFeature.BatteryConservation);

            if (conservationCard != null)
            {
                conservationCard.IsEnabled = mode.ConservationMode;
                conservationCard.Status = mode.ConservationMode ? "Enabled" : "Disabled";
            }

            var rapidChargeCard = FeatureGroups
                .SelectMany(g => g.Features)
                .FirstOrDefault(f => f.Feature == HardwareFeature.RapidCharge);

            if (rapidChargeCard != null)
            {
                rapidChargeCard.IsEnabled = mode.RapidChargeMode;
                rapidChargeCard.Status = mode.RapidChargeMode ? "Enabled" : "Disabled";
            }
        }

        private void UpdateThermalCards()
        {
            if (ThermalInfo == null)
                return;

            var cpuCard = FeatureGroups
                .SelectMany(g => g.Features)
                .FirstOrDefault(f => f.Title == "CPU Temperature");

            if (cpuCard != null)
            {
                cpuCard.Status = $"{ThermalInfo.CpuTemperature:F1}°C";
            }

            var gpuCard = FeatureGroups
                .SelectMany(g => g.Features)
                .FirstOrDefault(f => f.Title == "GPU Temperature");

            if (gpuCard != null)
            {
                gpuCard.Status = $"{ThermalInfo.GpuTemperature:F1}°C";
            }

            var fanCard = FeatureGroups
                .SelectMany(g => g.Features)
                .FirstOrDefault(f => f.Title == "Fan Speed");

            if (fanCard != null && ThermalInfo.Fans.Any())
            {
                var avgSpeed = ThermalInfo.Fans.Average(f => f.CurrentSpeed);
                fanCard.Status = $"{avgSpeed:F0} RPM";
            }
        }

        private void UpdateStatusItems()
        {
            StatusItems.Clear();

            if (BatteryInfo != null)
            {
                StatusItems.Add(new StatusItem
                {
                    Name = "Battery",
                    Value = $"{BatteryInfo.ChargeLevel}%",
                    Icon = "🔋"
                });

                StatusItems.Add(new StatusItem
                {
                    Name = "Battery Status",
                    Value = BatteryInfo.Status,
                    Icon = BatteryInfo.IsCharging ? "⚡" : "🔌"
                });
            }

            if (ThermalInfo != null)
            {
                StatusItems.Add(new StatusItem
                {
                    Name = "CPU Temp",
                    Value = $"{ThermalInfo.CpuTemperature:F1}°C",
                    Icon = "🌡️"
                });

                if (ThermalInfo.GpuTemperature > 0)
                {
                    StatusItems.Add(new StatusItem
                    {
                        Name = "GPU Temp",
                        Value = $"{ThermalInfo.GpuTemperature:F1}°C",
                        Icon = "🌡️"
                    });
                }
            }

            StatusItems.Add(new StatusItem
            {
                Name = "Power Mode",
                Value = CurrentPowerMode.ToString(),
                Icon = "⚡"
            });
        }

        private async Task SetPowerModeAsync(string mode)
        {
            try
            {
                if (Enum.TryParse<PowerMode>(mode, out var powerMode))
                {
                    var success = await _powerModeService.SetPowerModeAsync(powerMode);
                    if (success)
                    {
                        CurrentPowerMode = powerMode;
                        UpdatePowerModeCard();
                        SetStatus($"Power mode changed to {mode}");
                    }
                    else
                    {
                        SetError($"Failed to set power mode to {mode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set power mode to {mode}", ex);
                SetError($"Error setting power mode: {ex.Message}");
            }
        }

        private async Task ToggleFeatureAsync(FeatureCard card)
        {
            try
            {
                card.IsLoading = true;
                await card.OnToggled?.Invoke(card)!;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to toggle feature {card.Title}", ex);
                SetError($"Failed to toggle {card.Title}");
            }
            finally
            {
                card.IsLoading = false;
            }
        }

        private async Task ExecuteActionAsync(FeatureCard card)
        {
            try
            {
                card.IsLoading = true;
                await card.OnToggled?.Invoke(card)!;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to execute action {card.Title}", ex);
                SetError($"Failed to execute {card.Title}");
            }
            finally
            {
                card.IsLoading = false;
            }
        }

        private async Task ToggleConservationModeAsync(FeatureCard card)
        {
            var newState = !card.IsEnabled;
            var success = await _batteryService.SetConservationModeAsync(newState);
            if (success)
            {
                card.IsEnabled = newState;
                card.Status = newState ? "Enabled" : "Disabled";
                SetStatus($"Conservation mode {(newState ? "enabled" : "disabled")}");
            }
            else
            {
                SetError("Failed to toggle conservation mode");
            }
        }

        private async Task ToggleRapidChargeAsync(FeatureCard card)
        {
            var newState = !card.IsEnabled;
            var success = await _batteryService.SetRapidChargeModeAsync(newState);
            if (success)
            {
                card.IsEnabled = newState;
                card.Status = newState ? "Enabled" : "Disabled";
                SetStatus($"Rapid charge {(newState ? "enabled" : "disabled")}");
            }
            else
            {
                SetError("Failed to toggle rapid charge");
            }
        }

        private void OnPowerModeChanged(object? sender, PowerMode mode)
        {
            CurrentPowerMode = mode;
            UpdatePowerModeCard();
            UpdateStatusItems();
        }

        private void OnBatteryInfoChanged(object? sender, BatteryInfo info)
        {
            BatteryInfo = info;
            UpdateStatusItems();
        }

        private void OnThermalInfoUpdated(object? sender, ThermalInfo info)
        {
            ThermalInfo = info;
            UpdateThermalCards();
            UpdateStatusItems();
        }

        public override void Cleanup()
        {
            _powerModeService.PowerModeChanged -= OnPowerModeChanged;
            _batteryService.BatteryInfoChanged -= OnBatteryInfoChanged;
            _thermalService.ThermalInfoUpdated -= OnThermalInfoUpdated;
            base.Cleanup();
        }
    }

    public class StatusItem : ReactiveObject
    {
        private string _name = string.Empty;
        private string _value = string.Empty;
        private string _icon = string.Empty;

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public string Value
        {
            get => _value;
            set => this.RaiseAndSetIfChanged(ref _value, value);
        }

        public string Icon
        {
            get => _icon;
            set => this.RaiseAndSetIfChanged(ref _icon, value);
        }
    }
}