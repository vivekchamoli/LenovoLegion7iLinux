using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Services;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Services.Linux;
using LenovoLegionToolkit.Avalonia.ViewModels;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.Extensions
{
    /// <summary>
    /// OPTIMIZED Service Collection Extensions with improved startup performance and reliability
    /// Fixes: Service registration order, lifecycle management, startup performance
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLegionToolkitServices(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            try
            {
                Logger.Info("Registering Legion Toolkit services...");

                // Register core services first (these are needed by everything else)
                RegisterCoreServices(services);

                // Register Linux-specific services
                RegisterLinuxServices(services);

                // Register view models
                RegisterViewModels(services);

                // Register background services
                RegisterBackgroundServices(services);

                Logger.Info("All Legion Toolkit services registered successfully");
                return services;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to register Legion Toolkit services", ex);
                throw;
            }
        }

        private static void RegisterCoreServices(IServiceCollection services)
        {
            Logger.Debug("Registering core services...");

            // File system service (fundamental dependency)
            services.TryAddSingleton<IFileSystemService, FileSystemService>();

            // Process runner service
            services.TryAddSingleton<IProcessRunner, ProcessRunner>();

            // Configuration service
            services.TryAddSingleton<IConfigurationService, ConfigurationService>();

            // Settings service (depends on configuration and file system)
            services.TryAddSingleton<ISettingsService, SettingsService>();

            // Notification service
            services.TryAddSingleton<INotificationService, LinuxNotificationService>();

            // Logger configuration service
            services.TryAddSingleton<ILoggerConfigurationService, LoggerConfigurationService>();

            Logger.Debug("Core services registered");
        }

        private static void RegisterLinuxServices(IServiceCollection services)
        {
            Logger.Debug("Registering Linux-specific services...");

            // Hardware services
            services.TryAddSingleton<IBatteryService, LinuxBatteryService>();
            services.TryAddSingleton<IThermalService, LinuxThermalService>();
            services.TryAddSingleton<IPowerService, LinuxPowerService>();
            services.TryAddSingleton<IRGBService, LinuxRGBService>();
            services.TryAddSingleton<IFanService, LinuxFanService>();
            services.TryAddSingleton<IDisplayService, LinuxDisplayService>();
            services.TryAddSingleton<ISystemService, LinuxSystemService>();

            // Hardware abstraction service
            services.TryAddSingleton<IHardwareService, LinuxHardwareService>();

            Logger.Debug("Linux-specific services registered");
        }

        private static void RegisterViewModels(IServiceCollection services)
        {
            Logger.Debug("Registering view models...");

            // Main view model (transient to allow multiple instances if needed)
            services.TryAddTransient<MainViewModel>();

            // Feature view models
            services.TryAddTransient<BatteryViewModel>();
            services.TryAddTransient<ThermalViewModel>();
            services.TryAddTransient<PowerViewModel>();
            services.TryAddTransient<RGBViewModel>();
            services.TryAddTransient<DisplayViewModel>();
            services.TryAddTransient<SettingsViewModel>();
            services.TryAddTransient<DashboardViewModel>();

            Logger.Debug("View models registered");
        }

        private static void RegisterBackgroundServices(IServiceCollection services)
        {
            Logger.Debug("Registering background services...");

            // Monitoring services (these start background tasks)
            services.TryAddSingleton<ISystemMonitoringService, SystemMonitoringService>();
            services.TryAddSingleton<IAutomationService, AutomationService>();
            services.TryAddSingleton<ISystemTrayService, LinuxSystemTrayService>();

            // Update service
            services.TryAddSingleton<IUpdateService, UpdateService>();

            Logger.Debug("Background services registered");
        }

        /// <summary>
        /// Initialize background services asynchronously after application startup
        /// </summary>
        public static async Task InitializeBackgroundServicesAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            try
            {
                Logger.Info("Initializing background services...");

                // Initialize services that need async initialization
                var initializationTasks = new[]
                {
                    InitializeServiceAsync<ISystemMonitoringService>(serviceProvider, cancellationToken),
                    InitializeServiceAsync<IAutomationService>(serviceProvider, cancellationToken),
                    InitializeServiceAsync<ISystemTrayService>(serviceProvider, cancellationToken),
                    InitializeServiceAsync<IUpdateService>(serviceProvider, cancellationToken)
                };

                // Wait for all background services to initialize with timeout
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                var initTask = Task.WhenAll(initializationTasks);
                var completedTask = await Task.WhenAny(initTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Logger.Warning("Background service initialization timed out after 30 seconds");
                }
                else
                {
                    Logger.Info("All background services initialized successfully");
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Background service initialization was cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize background services", ex);
                throw;
            }
        }

        private static async Task InitializeServiceAsync<T>(IServiceProvider serviceProvider, CancellationToken cancellationToken)
            where T : class
        {
            try
            {
                var service = serviceProvider.GetService<T>();
                if (service == null)
                {
                    Logger.Warning($"Service {typeof(T).Name} not found in service provider");
                    return;
                }

                // Check if service has async initialization method
                if (service is IAsyncInitializable asyncInitializable)
                {
                    await asyncInitializable.InitializeAsync(cancellationToken);
                    Logger.Debug($"Async initialized service: {typeof(T).Name}");
                }
                else if (service is IInitializable initializable)
                {
                    // Run sync initialization in background thread
                    await Task.Run(() => initializable.Initialize(), cancellationToken);
                    Logger.Debug($"Sync initialized service: {typeof(T).Name}");
                }
                else
                {
                    Logger.Debug($"Service {typeof(T).Name} does not require initialization");
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Debug($"Initialization cancelled for service: {typeof(T).Name}");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize service {typeof(T).Name}", ex);
                // Don't rethrow - allow other services to continue initializing
            }
        }

        /// <summary>
        /// Validate that all critical services are properly registered
        /// </summary>
        public static bool ValidateServiceRegistrations(this IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            var criticalServices = new[]
            {
                typeof(IFileSystemService),
                typeof(IProcessRunner),
                typeof(IConfigurationService),
                typeof(ISettingsService),
                typeof(IBatteryService),
                typeof(IThermalService),
                typeof(IPowerService),
                typeof(MainViewModel)
            };

            try
            {
                foreach (var serviceType in criticalServices)
                {
                    var service = serviceProvider.GetService(serviceType);
                    if (service == null)
                    {
                        Logger.Error($"Critical service not registered: {serviceType.Name}");
                        return false;
                    }
                }

                Logger.Info("All critical services are properly registered");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Error validating service registrations", ex);
                return false;
            }
        }

        /// <summary>
        /// Perform health checks on all services
        /// </summary>
        public static async Task<bool> PerformHealthChecksAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            try
            {
                Logger.Info("Performing service health checks...");

                var healthCheckTasks = new[]
                {
                    CheckServiceHealthAsync<IBatteryService>(serviceProvider, cancellationToken),
                    CheckServiceHealthAsync<IThermalService>(serviceProvider, cancellationToken),
                    CheckServiceHealthAsync<IPowerService>(serviceProvider, cancellationToken),
                    CheckServiceHealthAsync<IFileSystemService>(serviceProvider, cancellationToken)
                };

                var results = await Task.WhenAll(healthCheckTasks);
                var allHealthy = Array.TrueForAll(results, r => r);

                if (allHealthy)
                {
                    Logger.Info("All services passed health checks");
                }
                else
                {
                    Logger.Warning("Some services failed health checks");
                }

                return allHealthy;
            }
            catch (Exception ex)
            {
                Logger.Error("Error performing health checks", ex);
                return false;
            }
        }

        private static async Task<bool> CheckServiceHealthAsync<T>(IServiceProvider serviceProvider, CancellationToken cancellationToken)
            where T : class
        {
            try
            {
                var service = serviceProvider.GetService<T>();
                if (service == null)
                {
                    Logger.Warning($"Service {typeof(T).Name} not available for health check");
                    return false;
                }

                // Check if service has health check method
                if (service is IHealthCheckable healthCheckable)
                {
                    var isHealthy = await healthCheckable.CheckHealthAsync(cancellationToken);
                    if (!isHealthy)
                    {
                        Logger.Warning($"Service {typeof(T).Name} failed health check");
                    }
                    return isHealthy;
                }

                // If no health check method, assume healthy if we can get the service
                Logger.Debug($"Service {typeof(T).Name} has no health check - assuming healthy");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Health check failed for service {typeof(T).Name}", ex);
                return false;
            }
        }
    }

    /// <summary>
    /// Interface for services that require async initialization
    /// </summary>
    public interface IAsyncInitializable
    {
        Task InitializeAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for services that require sync initialization
    /// </summary>
    public interface IInitializable
    {
        void Initialize();
    }

    /// <summary>
    /// Interface for services that support health checks
    /// </summary>
    public interface IHealthCheckable
    {
        Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
    }
}