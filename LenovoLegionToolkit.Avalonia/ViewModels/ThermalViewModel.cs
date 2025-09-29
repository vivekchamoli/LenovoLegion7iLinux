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
    public class ThermalViewModel : ViewModelBase, IActivatableViewModel
    {
        private readonly IThermalService _thermalService;

        private double _cpuTemperature;
        private double _gpuTemperature;
        private double _systemTemperature;
        private int _cpuFanSpeed;
        private int _gpuFanSpeed;
        private bool _fanControlEnabled;
        private int _targetFanSpeed = 50;
        private string _thermalMode = "Balanced";
        private double _cpuMaxTemp;
        private double _gpuMaxTemp;

        public ViewModelActivator Activator { get; } = new ViewModelActivator();

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

        public double SystemTemperature
        {
            get => _systemTemperature;
            set => this.RaiseAndSetIfChanged(ref _systemTemperature, value);
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

        public bool FanControlEnabled
        {
            get => _fanControlEnabled;
            set => this.RaiseAndSetIfChanged(ref _fanControlEnabled, value);
        }

        public int TargetFanSpeed
        {
            get => _targetFanSpeed;
            set => this.RaiseAndSetIfChanged(ref _targetFanSpeed, value);
        }

        public string ThermalMode
        {
            get => _thermalMode;
            set => this.RaiseAndSetIfChanged(ref _thermalMode, value);
        }

        public double CpuMaxTemp
        {
            get => _cpuMaxTemp;
            set => this.RaiseAndSetIfChanged(ref _cpuMaxTemp, value);
        }

        public double GpuMaxTemp
        {
            get => _gpuMaxTemp;
            set => this.RaiseAndSetIfChanged(ref _gpuMaxTemp, value);
        }

        public ObservableCollection<ThermalDataPoint> CpuTempHistory { get; }
        public ObservableCollection<ThermalDataPoint> GpuTempHistory { get; }
        public ObservableCollection<ThermalDataPoint> FanSpeedHistory { get; }

        public string[] ThermalModes { get; } = { "Quiet", "Balanced", "Performance", "Custom" };

        public ReactiveCommand<Unit, Unit> ToggleFanControlCommand { get; }
        public ReactiveCommand<int, Unit> SetFanSpeedCommand { get; }
        public ReactiveCommand<string, Unit> SetThermalModeCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        public ReactiveCommand<Unit, Unit> ResetMaxTempsCommand { get; }

        public ThermalViewModel(IThermalService thermalService)
        {
            _thermalService = thermalService ?? throw new ArgumentNullException(nameof(thermalService));

            CpuTempHistory = new ObservableCollection<ThermalDataPoint>();
            GpuTempHistory = new ObservableCollection<ThermalDataPoint>();
            FanSpeedHistory = new ObservableCollection<ThermalDataPoint>();

            ToggleFanControlCommand = ReactiveCommand.CreateFromTask(ToggleFanControlAsync);
            SetFanSpeedCommand = ReactiveCommand.CreateFromTask<int>(SetFanSpeedAsync);
            SetThermalModeCommand = ReactiveCommand.CreateFromTask<string>(SetThermalModeAsync);
            RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
            ResetMaxTempsCommand = ReactiveCommand.Create(ResetMaxTemps);

            this.WhenActivated(disposables =>
            {
                // Update temperatures every 2 seconds
                Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(2))
                    .SelectMany(_ => Observable.FromAsync(RefreshAsync))
                    .Subscribe()
                    .DisposeWith(disposables);

                // Update history every 5 seconds
                Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(5))
                    .SelectMany(_ => Observable.FromAsync(UpdateHistoryAsync))
                    .Subscribe()
                    .DisposeWith(disposables);
            });
        }

        private async Task ToggleFanControlAsync()
        {
            var newState = !FanControlEnabled;
            var success = await _thermalService.SetFanControlAsync(newState);
            if (success)
            {
                FanControlEnabled = newState;
                if (newState)
                {
                    await SetFanSpeedAsync(TargetFanSpeed);
                }
            }
        }

        private async Task SetFanSpeedAsync(int speed)
        {
            if (!FanControlEnabled)
                return;

            var success = await _thermalService.SetFanSpeedAsync(speed, speed);
            if (success)
            {
                TargetFanSpeed = speed;
            }
        }

        private async Task SetThermalModeAsync(string mode)
        {
            ThermalMode = mode;
            // Adjust fan curves based on mode
            switch (mode)
            {
                case "Quiet":
                    await SetFanSpeedAsync(30);
                    break;
                case "Balanced":
                    await SetFanSpeedAsync(50);
                    break;
                case "Performance":
                    await SetFanSpeedAsync(80);
                    break;
                case "Custom":
                    // Keep current settings
                    break;
            }
        }

        private void ResetMaxTemps()
        {
            CpuMaxTemp = CpuTemperature;
            GpuMaxTemp = GpuTemperature;
        }

        private async Task RefreshAsync()
        {
            try
            {
                var thermalInfo = await _thermalService.GetThermalInfoAsync();
                if (thermalInfo != null)
                {
                    CpuTemperature = thermalInfo.CpuTemperature;
                    GpuTemperature = thermalInfo.GpuTemperature;
                    SystemTemperature = thermalInfo.SystemTemperature;
                    CpuFanSpeed = thermalInfo.CpuFanRpm;
                    GpuFanSpeed = thermalInfo.GpuFanRpm;

                    // Track maximum temperatures
                    if (CpuTemperature > CpuMaxTemp)
                        CpuMaxTemp = CpuTemperature;
                    if (GpuTemperature > GpuMaxTemp)
                        GpuMaxTemp = GpuTemperature;
                }

                FanControlEnabled = await _thermalService.GetFanControlStateAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing thermal state: {ex.Message}");
            }
        }

        private async Task UpdateHistoryAsync()
        {
            await RefreshAsync();

            var now = DateTime.Now;

            // Add new data points
            CpuTempHistory.Add(new ThermalDataPoint { Timestamp = now, Value = CpuTemperature });
            GpuTempHistory.Add(new ThermalDataPoint { Timestamp = now, Value = GpuTemperature });
            FanSpeedHistory.Add(new ThermalDataPoint { Timestamp = now, Value = CpuFanSpeed });

            // Keep only last 60 data points (5 minutes of data at 5 second intervals)
            while (CpuTempHistory.Count > 60)
                CpuTempHistory.RemoveAt(0);
            while (GpuTempHistory.Count > 60)
                GpuTempHistory.RemoveAt(0);
            while (FanSpeedHistory.Count > 60)
                FanSpeedHistory.RemoveAt(0);
        }
    }

    public class ThermalDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }
}