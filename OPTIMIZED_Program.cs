using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Services;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia;

public class Program
{
    public static IServiceProvider? ServiceProvider { get; private set; }
    private static readonly CancellationTokenSource _shutdownTokenSource = new();
    public static CancellationToken ShutdownToken => _shutdownTokenSource.Token;

    [STAThread]
    public static void Main(string[] args)
    {
        // Setup global exception handlers
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            // Initialize logger first with proper error handling
            Logger.Initialize(LogLevel.Info, true);
            Logger.Info("Legion Toolkit starting...");

            // Validate runtime environment
            ValidateRuntimeEnvironment();

            // Setup dependency injection with validation
            var services = new ServiceCollection();
            services.AddLegionToolkitServices();
            ServiceProvider = services.BuildServiceProvider();

            // Initialize critical services synchronously (required for startup)
            InitializeCriticalServices();

            // Initialize non-critical services asynchronously in background
            _ = Task.Run(async () =>
            {
                try
                {
                    Logger.Info("Initializing background services...");
                    await ServiceProvider.InitializeBackgroundServicesAsync(_shutdownTokenSource.Token);
                    Logger.Info("Background services initialized successfully");
                }
                catch (OperationCanceledException)
                {
                    Logger.Info("Background service initialization was cancelled");
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to initialize background services", ex);
                }
            }, _shutdownTokenSource.Token);

            // Build and run Avalonia app
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Logger.Critical("Fatal error during startup", ex);
            Environment.Exit(1);
        }
        finally
        {
            Shutdown();
        }
    }

    private static void ValidateRuntimeEnvironment()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("This application requires Linux to run");
        }

        if (!Directory.Exists("/sys"))
        {
            Logger.Warning("sysfs not available - some features may not work");
        }
    }

    private static void InitializeCriticalServices()
    {
        try
        {
            var settingsService = ServiceProvider?.GetService<ISettingsService>();
            if (settingsService == null)
            {
                throw new InvalidOperationException("Failed to initialize settings service");
            }

            Logger.Info("Critical services initialized");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to initialize critical services", ex);
            throw;
        }
    }

    private static void Shutdown()
    {
        try
        {
            Logger.Info("Legion Toolkit shutting down...");

            // Signal shutdown to all services
            _shutdownTokenSource.Cancel();

            // Cleanup services
            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            Logger.Info("Legion Toolkit shutdown complete");
        }
        catch (Exception ex)
        {
            Logger.Error("Error during shutdown", ex);
        }
        finally
        {
            _shutdownTokenSource.Dispose();
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Logger.Critical("Unhandled exception in application domain", ex);
        }
        else
        {
            Logger.Critical($"Unhandled non-exception object: {e.ExceptionObject}");
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Error("Unobserved task exception", e.Exception);
        e.SetObserved(); // Prevent process termination
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI()
            .LogToTrace()
            .AfterSetup(AfterSetup);

    private static void AfterSetup(AppBuilder builder)
    {
        // Additional setup after Avalonia is initialized
        Logger.Info("Avalonia setup complete");

        // Check platform
        if (!OperatingSystem.IsLinux())
        {
            Logger.Warning("This application is designed for Linux. Some features may not work correctly.");
        }

        // Check for root/admin permissions for certain operations
        if (Environment.GetEnvironmentVariable("USER") != "root")
        {
            Logger.Info("Running as non-root user. Some features may require sudo.");
        }
    }
}