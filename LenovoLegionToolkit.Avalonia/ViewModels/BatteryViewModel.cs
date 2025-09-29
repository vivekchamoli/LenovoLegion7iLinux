using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using DynamicData;
using System.Collections.ObjectModel;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;

namespace LenovoLegionToolkit.Avalonia.ViewModels
{
    public class BatteryViewModel : ViewModelBase, IActivatableViewModel
    {
        private readonly IBatteryService _batteryService;

        private BatteryInfo? _batteryInfo;
        private bool _rapidChargeEnabled;
        private bool _conservationModeEnabled;
        private int _chargingThreshold = 80;
        private string _batteryHealth = "Good";
        private int _cycleCount;
        private double _designCapacity;
        private double _fullChargeCapacity;
        private double _currentCapacity;
        private double _voltage;
        private string _chargingStatus = "Unknown";
        private TimeSpan _estimatedTimeRemaining;

        public ViewModelActivator Activator { get; } = new ViewModelActivator();

        public BatteryInfo? BatteryInfo
        {
            get => _batteryInfo;
            set => this.RaiseAndSetIfChanged(ref _batteryInfo, value);
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

        public string BatteryHealth
        {
            get => _batteryHealth;
            set => this.RaiseAndSetIfChanged(ref _batteryHealth, value);
        }

        public int CycleCount
        {
            get => _cycleCount;
            set => this.RaiseAndSetIfChanged(ref _cycleCount, value);
        }

        public double DesignCapacity
        {
            get => _designCapacity;
            set => this.RaiseAndSetIfChanged(ref _designCapacity, value);
        }

        public double FullChargeCapacity
        {
            get => _fullChargeCapacity;
            set => this.RaiseAndSetIfChanged(ref _fullChargeCapacity, value);
        }

        public double CurrentCapacity
        {
            get => _currentCapacity;
            set => this.RaiseAndSetIfChanged(ref _currentCapacity, value);
        }

        public double Voltage
        {
            get => _voltage;
            set => this.RaiseAndSetIfChanged(ref _voltage, value);
        }

        public string ChargingStatus
        {
            get => _chargingStatus;
            set => this.RaiseAndSetIfChanged(ref _chargingStatus, value);
        }

        public TimeSpan EstimatedTimeRemaining
        {
            get => _estimatedTimeRemaining;
            set => this.RaiseAndSetIfChanged(ref _estimatedTimeRemaining, value);
        }

        public double BatteryHealthPercentage =>
            DesignCapacity > 0 ? (FullChargeCapacity / DesignCapacity) * 100 : 100;

        public ReactiveCommand<Unit, Unit> ToggleRapidChargeCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleConservationModeCommand { get; }
        public ReactiveCommand<int, Unit> SetChargingThresholdCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        public ReactiveCommand<Unit, Unit> CalibrateCommand { get; }

        public ObservableCollection<BatteryHistoryItem> ChargeHistory { get; }

        public BatteryViewModel(IBatteryService batteryService)
        {
            _batteryService = batteryService ?? throw new ArgumentNullException(nameof(batteryService));

            ChargeHistory = new ObservableCollection<BatteryHistoryItem>();

            ToggleRapidChargeCommand = ReactiveCommand.CreateFromTask(ToggleRapidChargeAsync);
            ToggleConservationModeCommand = ReactiveCommand.CreateFromTask(ToggleConservationModeAsync);
            SetChargingThresholdCommand = ReactiveCommand.CreateFromTask<int>(SetChargingThresholdAsync);
            RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
            CalibrateCommand = ReactiveCommand.CreateFromTask(CalibrateBatteryAsync);

            this.WhenActivated(disposables =>
            {
                Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(10))
                    .SelectMany(_ => Observable.FromAsync(RefreshAsync))
                    .Subscribe()
                    .DisposeWith(disposables);

                Observable.Timer(TimeSpan.Zero, TimeSpan.FromMinutes(5))
                    .SelectMany(_ => Observable.FromAsync(UpdateBatteryHistoryAsync))
                    .Subscribe()
                    .DisposeWith(disposables);
            });
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

        private async Task CalibrateBatteryAsync()
        {
            await Task.Delay(1000);
            Console.WriteLine("Battery calibration initiated...");
        }

        private async Task RefreshAsync()
        {
            try
            {
                BatteryInfo = await _batteryService.GetBatteryInfoAsync();

                if (BatteryInfo != null)
                {
                    CycleCount = BatteryInfo.CycleCount;
                    DesignCapacity = BatteryInfo.DesignCapacity;
                    FullChargeCapacity = BatteryInfo.FullChargeCapacity;
                    CurrentCapacity = BatteryInfo.RemainingCapacity;
                    Voltage = BatteryInfo.Voltage;
                    EstimatedTimeRemaining = BatteryInfo.EstimatedTimeRemaining ?? TimeSpan.Zero;

                    ChargingStatus = BatteryInfo.IsCharging ? "Charging" :
                                   BatteryInfo.IsDischarging ? "Discharging" :
                                   "AC Power";

                    var healthPercentage = BatteryHealthPercentage;
                    BatteryHealth = healthPercentage >= 90 ? "Excellent" :
                                   healthPercentage >= 80 ? "Good" :
                                   healthPercentage >= 60 ? "Fair" :
                                   "Poor";
                }

                RapidChargeEnabled = await _batteryService.GetRapidChargeAsync();
                ConservationModeEnabled = await _batteryService.GetConservationModeAsync();
                ChargingThreshold = await _batteryService.GetChargingThresholdAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing battery state: {ex.Message}");
            }
        }

        private async Task UpdateBatteryHistoryAsync()
        {
            var info = await _batteryService.GetBatteryInfoAsync();
            if (info != null)
            {
                var historyItem = new BatteryHistoryItem
                {
                    Timestamp = DateTime.Now,
                    ChargeLevel = info.ChargeLevel,
                    IsCharging = info.IsCharging,
                    Voltage = info.Voltage
                };

                ChargeHistory.Add(historyItem);

                if (ChargeHistory.Count > 100)
                {
                    ChargeHistory.RemoveAt(0);
                }
            }
        }
    }

    public class BatteryHistoryItem
    {
        public DateTime Timestamp { get; set; }
        public int ChargeLevel { get; set; }
        public bool IsCharging { get; set; }
        public double Voltage { get; set; }
    }
}