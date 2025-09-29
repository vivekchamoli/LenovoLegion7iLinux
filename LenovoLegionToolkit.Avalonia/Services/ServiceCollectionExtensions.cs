using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Services.Linux;
using LenovoLegionToolkit.Avalonia.Settings;
using LenovoLegionToolkit.Avalonia.ViewModels;
using LenovoLegionToolkit.Avalonia.IPC;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLegionToolkitServices(this IServiceCollection services)
        {
            // Core services
            services.AddLogging(ConfigureLogging);
            services.AddSettings();
            services.AddAbstractions();
            services.AddPlatformServices();
            services.AddViewModels();
            services.AddIpcServices();

            return services;
        }

        private static void ConfigureLogging(ILoggingBuilder builder)
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);

            // Add custom logger provider
            builder.AddProvider(new LegionToolkitLoggerProvider());

#if DEBUG
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
            builder.AddConsole();
#endif
        }

        private static void AddSettings(this IServiceCollection services)
        {
            services.AddSingleton<ISettingsService, SettingsService>();
        }

        private static void AddAbstractions(this IServiceCollection services)
        {
            services.AddSingleton<IFileSystemService, FileSystemService>();
            services.AddSingleton<IProcessRunner, ProcessRunner>();
        }

        private static void AddPlatformServices(this IServiceCollection services)
        {
            // Register platform-specific services
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                services.AddLinuxServices();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Future: Add Windows service implementations if needed for cross-platform support
                throw new PlatformNotSupportedException("Windows is not yet supported in this build");
            }
            else
            {
                throw new PlatformNotSupportedException($"Platform {RuntimeInformation.OSDescription} is not supported");
            }
        }

        private static void AddLinuxServices(this IServiceCollection services)
        {
            // Hardware services
            services.AddSingleton<IHardwareService, LinuxHardwareService>();
            services.AddSingleton<IPowerModeService, LinuxPowerModeService>();
            services.AddSingleton<IBatteryService, LinuxBatteryService>();
            services.AddSingleton<IThermalService, LinuxThermalService>();
            services.AddSingleton<IKeyboardService, LinuxKeyboardService>();
            services.AddSingleton<IDisplayService, LinuxDisplayService>();
            services.AddSingleton<IAutomationService, LinuxAutomationService>();
            services.AddSingleton<INotificationService, LinuxNotificationService>();
            services.AddSingleton<IGpuService, LinuxGpuService>();

            Logger.Info("Linux services registered");
        }

        private static void AddViewModels(this IServiceCollection services)
        {
            // Register ViewModels as transient so each view gets its own instance
            services.AddTransient<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<PowerViewModel>();
            services.AddTransient<BatteryViewModel>();
            services.AddTransient<GpuViewModel>();
            services.AddTransient<ThermalViewModel>();
            services.AddTransient<KeyboardViewModel>();
            services.AddTransient<DisplayViewModel>();
            services.AddTransient<AutomationViewModel>();

            Logger.Debug("ViewModels registered");
        }

        private static void AddIpcServices(this IServiceCollection services)
        {
            // Register IPC server as singleton
            services.AddSingleton<IpcServer>();

            // Register IPC command handlers
            // services.AddTransient<IPowerModeCommandHandler, PowerModeCommandHandler>();
            // services.AddTransient<IBatteryCommandHandler, BatteryCommandHandler>();

            Logger.Debug("IPC services registered");
        }

        // Service provider extensions for convenience
        public static T GetRequiredService<T>(this IServiceProvider provider) where T : notnull
        {
            return provider.GetService<T>()
                ?? throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered");
        }

        public static async Task InitializeServicesAsync(this IServiceProvider provider)
        {
            try
            {
                Logger.Info("Initializing services...");

                // Load settings first
                var settingsService = provider.GetRequiredService<ISettingsService>();
                await settingsService.LoadSettingsAsync();

                // Check and load kernel module if needed
                var hardwareService = provider.GetRequiredService<IHardwareService>();
                if (!await hardwareService.CheckKernelModuleAsync())
                {
                    Logger.Warning("Legion kernel module not loaded. Some features may be unavailable.");

                    // Try to load the module
                    if (LinuxPlatform.IsRunningAsRoot)
                    {
                        if (await hardwareService.LoadKernelModuleAsync())
                        {
                            Logger.Info("Legion kernel module loaded successfully");
                        }
                    }
                }

                // Detect hardware capabilities
                var capabilities = await hardwareService.DetectCapabilitiesAsync();
                Logger.Info($"Detected {capabilities.Features.Count(f => f.Value)} supported features");

                // Start monitoring if enabled
                var settings = settingsService.Settings;
                if (settings.Monitoring.EnableMonitoring)
                {
                    var thermalService = provider.GetRequiredService<IThermalService>();
                    var interval = TimeSpan.FromSeconds(settings.Monitoring.UpdateIntervalSeconds);
                    await thermalService.StartMonitoringAsync(interval);
                }

                // Start IPC server if enabled
                if (settings.Advanced.EnableIpcServer)
                {
                    var ipcServer = provider.GetRequiredService<IpcServer>();
                    await ipcServer.StartAsync();
                }

                Logger.Info("Services initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize services", ex);
                throw;
            }
        }

        public static async Task ShutdownServicesAsync(this IServiceProvider provider)
        {
            try
            {
                Logger.Info("Shutting down services...");

                // Stop monitoring
                var thermalService = provider.GetService<IThermalService>();
                if (thermalService != null)
                {
                    await thermalService.StopMonitoringAsync();
                }

                // Stop IPC server
                var ipcServer = provider.GetService<IpcServer>();
                if (ipcServer != null)
                {
                    await ipcServer.StopAsync();
                }

                // Save settings
                var settingsService = provider.GetService<ISettingsService>();
                if (settingsService != null)
                {
                    var settings = settingsService.LoadSettings();
                    await settingsService.SaveSettingsAsync(settings);
                }

                Logger.Info("Services shut down successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Error during service shutdown", ex);
            }
        }
    }

    // Custom logger provider that uses our Logger utility
    internal class LegionToolkitLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new LegionToolkitLogger(categoryName);
        }

        public void Dispose()
        {
        }
    }

    internal class LegionToolkitLogger : ILogger
    {
        private readonly string _categoryName;

        public LegionToolkitLogger(string categoryName)
        {
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return logLevel >= Microsoft.Extensions.Logging.LogLevel.Information;
        }

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);

            switch (logLevel)
            {
                case Microsoft.Extensions.Logging.LogLevel.Trace:
                case Microsoft.Extensions.Logging.LogLevel.Debug:
                    Logger.Debug(message, _categoryName);
                    break;
                case Microsoft.Extensions.Logging.LogLevel.Information:
                    Logger.Info(message, _categoryName);
                    break;
                case Microsoft.Extensions.Logging.LogLevel.Warning:
                    Logger.Warning(message, _categoryName);
                    break;
                case Microsoft.Extensions.Logging.LogLevel.Error:
                    Logger.Error(message, exception, _categoryName);
                    break;
                case Microsoft.Extensions.Logging.LogLevel.Critical:
                    Logger.Critical(message, exception, _categoryName);
                    break;
            }
        }
    }
}