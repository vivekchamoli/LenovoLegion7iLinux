using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;
using ReactiveUI;

namespace LenovoLegionToolkit.Avalonia.ViewModels
{
    public class GpuViewModel : ViewModelBase
    {
        private readonly IGpuService _gpuService;

        private ObservableCollection<GpuInfo> _gpus = new();
        private GpuInfo? _primaryGpu;
        private GpuInfo? _discreteGpu;
        private HybridModeState _currentHybridMode;
        private bool _isHybridModeChanging;
        private bool _isHybridModeSupported;
        private string _statusMessage = "Detecting GPUs...";
        private bool _isLoading = true;

        public ObservableCollection<GpuInfo> Gpus
        {
            get => _gpus;
            set => this.RaiseAndSetIfChanged(ref _gpus, value);
        }

        public GpuInfo? PrimaryGpu
        {
            get => _primaryGpu;
            set => this.RaiseAndSetIfChanged(ref _primaryGpu, value);
        }

        public GpuInfo? DiscreteGpu
        {
            get => _discreteGpu;
            set => this.RaiseAndSetIfChanged(ref _discreteGpu, value);
        }

        public HybridModeState CurrentHybridMode
        {
            get => _currentHybridMode;
            set => this.RaiseAndSetIfChanged(ref _currentHybridMode, value);
        }

        public bool IsHybridModeChanging
        {
            get => _isHybridModeChanging;
            set => this.RaiseAndSetIfChanged(ref _isHybridModeChanging, value);
        }

        public bool IsHybridModeSupported
        {
            get => _isHybridModeSupported;
            set => this.RaiseAndSetIfChanged(ref _isHybridModeSupported, value);
        }

        public new string StatusMessage
        {
            get => _statusMessage;
            set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        public new bool IsLoading
        {
            get => _isLoading;
            set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        // Commands
        public ReactiveCommand<HybridModeState, Unit> SetHybridModeCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        public ReactiveCommand<Unit, Unit> PowerOffDiscreteCommand { get; }
        public ReactiveCommand<Unit, Unit> PowerOnDiscreteCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenNvidiaSettingsCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenAmdSettingsCommand { get; }

        public GpuViewModel(IGpuService gpuService)
        {
            _gpuService = gpuService;

            // Create commands
            SetHybridModeCommand = ReactiveCommand.CreateFromTask<HybridModeState>(
                SetHybridModeAsync,
                this.WhenAnyValue(x => x.IsHybridModeChanging)
                    .Select(changing => !changing));

            RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);

            PowerOffDiscreteCommand = ReactiveCommand.CreateFromTask(
                PowerOffDiscreteAsync,
                this.WhenAnyValue(x => x.DiscreteGpu)
                    .Select(gpu => gpu != null && gpu.IsActive));

            PowerOnDiscreteCommand = ReactiveCommand.CreateFromTask(
                PowerOnDiscreteAsync,
                this.WhenAnyValue(x => x.DiscreteGpu)
                    .Select(gpu => gpu != null && !gpu.IsActive));

            OpenNvidiaSettingsCommand = ReactiveCommand.CreateFromTask(OpenNvidiaSettingsAsync);
            OpenAmdSettingsCommand = ReactiveCommand.CreateFromTask(OpenAmdSettingsAsync);

            // Subscribe to GPU state changes
            _gpuService.GpuStateChanged
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(OnGpuStateChanged);

            // Initial load
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Detecting GPUs...";

                await RefreshAsync();

                IsHybridModeSupported = await _gpuService.IsHybridModeSupportedAsync();
                if (IsHybridModeSupported)
                {
                    CurrentHybridMode = await _gpuService.GetHybridModeAsync();
                    StatusMessage = $"Hybrid Mode: {CurrentHybridMode}";
                }
                else
                {
                    StatusMessage = "Single GPU system";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize GPU view model", ex);
                StatusMessage = "Failed to detect GPUs";
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
                var gpus = await _gpuService.GetGpuInfoAsync();
                Gpus = new ObservableCollection<GpuInfo>(gpus);

                PrimaryGpu = await _gpuService.GetPrimaryGpuAsync();
                DiscreteGpu = await _gpuService.GetDiscreteGpuAsync();

                if (IsHybridModeSupported)
                {
                    CurrentHybridMode = await _gpuService.GetHybridModeAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to refresh GPU info", ex);
            }
        }

        private async Task SetHybridModeAsync(HybridModeState mode)
        {
            try
            {
                IsHybridModeChanging = true;
                StatusMessage = $"Switching to {mode}...";

                var success = await _gpuService.SetHybridModeAsync(mode);
                if (success)
                {
                    CurrentHybridMode = mode;
                    StatusMessage = $"Hybrid mode set to {mode}. Restart may be required.";
                    await RefreshAsync();
                }
                else
                {
                    StatusMessage = $"Failed to set hybrid mode to {mode}";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set hybrid mode to {mode}", ex);
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsHybridModeChanging = false;
            }
        }

        private async Task PowerOffDiscreteAsync()
        {
            try
            {
                StatusMessage = "Powering off discrete GPU...";
                var success = await _gpuService.PowerOffDiscreteGpuAsync();

                if (success)
                {
                    StatusMessage = "Discrete GPU powered off";
                    await RefreshAsync();
                }
                else
                {
                    StatusMessage = "Failed to power off discrete GPU";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to power off discrete GPU", ex);
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        private async Task PowerOnDiscreteAsync()
        {
            try
            {
                StatusMessage = "Powering on discrete GPU...";
                var success = await _gpuService.PowerOnDiscreteGpuAsync();

                if (success)
                {
                    StatusMessage = "Discrete GPU powered on";
                    await RefreshAsync();
                }
                else
                {
                    StatusMessage = "Failed to power on discrete GPU";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to power on discrete GPU", ex);
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        private async Task OpenNvidiaSettingsAsync()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "nvidia-settings",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to open NVIDIA settings", ex);
                StatusMessage = "nvidia-settings not found";
            }
        }

        private async Task OpenAmdSettingsAsync()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "amdgpu-pro-control",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to open AMD settings", ex);
                StatusMessage = "AMD control panel not found";
            }
        }

        private void OnGpuStateChanged(GpuInfo gpu)
        {
            var existingGpu = Gpus.FirstOrDefault(g => g.BusId == gpu.BusId);
            if (existingGpu != null)
            {
                var index = Gpus.IndexOf(existingGpu);
                Gpus[index] = gpu;
            }
        }
    }
}