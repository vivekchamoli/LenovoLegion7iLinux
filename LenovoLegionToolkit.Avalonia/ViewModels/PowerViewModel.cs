using System;
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
    public class PowerViewModel : ViewModelBase, IActivatableViewModel
    {
        private readonly IPowerModeService _powerModeService;
        private readonly IBatteryService _batteryService;

        private PowerMode _currentPowerMode;
        private bool _isCharging;
        private int _batteryLevel;
        private bool _rapidChargeEnabled;
        private bool _conservationModeEnabled;
        private int _chargingThreshold = 80;

        public ViewModelActivator Activator { get; } = new ViewModelActivator();

        public PowerMode CurrentPowerMode
        {
            get => _currentPowerMode;
            set => this.RaiseAndSetIfChanged(ref _currentPowerMode, value);
        }

        public bool IsCharging
        {
            get => _isCharging;
            set => this.RaiseAndSetIfChanged(ref _isCharging, value);
        }

        public int BatteryLevel
        {
            get => _batteryLevel;
            set => this.RaiseAndSetIfChanged(ref _batteryLevel, value);
        }

        public bool RapidChargeEnabled
        {
            get => _rapidChargeEnabled;
            set => this.RaiseAndSetIfChanged(ref _rapidChargeEnabled, value);
        }

        public bool ConservationModeEnabled
        {
            get => _conservationModeEnabled;
            set => this.RaiseAndSetIfChanged(ref _conservationModeEnabled, value);
        }

        public int ChargingThreshold
        {
            get => _chargingThreshold;
            set => this.RaiseAndSetIfChanged(ref _chargingThreshold, value);
        }

        public ReactiveCommand<PowerMode, Unit> SetPowerModeCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleRapidChargeCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleConservationModeCommand { get; }
        public ReactiveCommand<int, Unit> SetChargingThresholdCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

        public PowerMode[] AvailablePowerModes { get; } =
        {
            PowerMode.Quiet,
            PowerMode.Balanced,
            PowerMode.Performance,
            PowerMode.Custom
        };

        public int[] ChargingThresholds { get; } = { 60, 70, 80, 90, 100 };

        public PowerViewModel(IPowerModeService powerModeService, IBatteryService batteryService)
        {
            _powerModeService = powerModeService ?? throw new ArgumentNullException(nameof(powerModeService));
            _batteryService = batteryService ?? throw new ArgumentNullException(nameof(batteryService));

            SetPowerModeCommand = ReactiveCommand.CreateFromTask<PowerMode>(SetPowerModeAsync);
            ToggleRapidChargeCommand = ReactiveCommand.CreateFromTask(ToggleRapidChargeAsync);
            ToggleConservationModeCommand = ReactiveCommand.CreateFromTask(ToggleConservationModeAsync);
            SetChargingThresholdCommand = ReactiveCommand.CreateFromTask<int>(SetChargingThresholdAsync);
            RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);

            this.WhenActivated(disposables =>
            {
                Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(5))
                    .SelectMany(_ => Observable.FromAsync(RefreshAsync))
                    .Subscribe()
                    .DisposeWith(disposables);

                if (_powerModeService is INotifyPowerModeChanged notifier)
                {
                    Observable.FromEventPattern<PowerMode>(
                        h => notifier.PowerModeChanged += h,
                        h => notifier.PowerModeChanged -= h)
                        .Select(e => e.EventArgs)
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(mode => CurrentPowerMode = mode)
                        .DisposeWith(disposables);
                }
            });
        }

        private async Task SetPowerModeAsync(PowerMode mode)
        {
            var success = await _powerModeService.SetPowerModeAsync(mode);
            if (success)
            {
                CurrentPowerMode = mode;
            }
        }

        private async Task ToggleRapidChargeAsync()
        {
            var newState = !RapidChargeEnabled;
            var success = await _batteryService.SetRapidChargeAsync(newState);
            if (success)
            {
                RapidChargeEnabled = newState;
                if (newState)
                {
                    ConservationModeEnabled = false;
                }
            }
        }

        private async Task ToggleConservationModeAsync()
        {
            var newState = !ConservationModeEnabled;
            var success = await _batteryService.SetConservationModeAsync(newState);
            if (success)
            {
                ConservationModeEnabled = newState;
                if (newState)
                {
                    RapidChargeEnabled = false;
                }
            }
        }

        private async Task SetChargingThresholdAsync(int threshold)
        {
            var success = await _batteryService.SetChargingThresholdAsync(threshold);
            if (success)
            {
                ChargingThreshold = threshold;
            }
        }

        private async Task RefreshAsync()
        {
            try
            {
                CurrentPowerMode = await _powerModeService.GetCurrentPowerModeAsync();

                var batteryInfo = await _batteryService.GetBatteryInfoAsync();
                if (batteryInfo != null)
                {
                    BatteryLevel = batteryInfo.ChargeLevel;
                    IsCharging = batteryInfo.IsCharging;
                }

                RapidChargeEnabled = await _batteryService.GetRapidChargeAsync();
                ConservationModeEnabled = await _batteryService.GetConservationModeAsync();
                ChargingThreshold = await _batteryService.GetChargingThresholdAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing power state: {ex.Message}");
            }
        }
    }
}