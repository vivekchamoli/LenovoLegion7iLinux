using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Services;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia;

public class Program
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Initialize logger first
            Logger.Initialize(LogLevel.Info, true);
            Logger.Info("Legion Toolkit starting...");

            // Setup dependency injection
            var services = new ServiceCollection();
            services.AddLegionToolkitServices();
            ServiceProvider = services.BuildServiceProvider();

            // Initialize services asynchronously in background
            Task.Run(async () =>
            {
                try
                {
                    Logger.Info("Initializing services...");
                    await ServiceProvider.InitializeServicesAsync();
                    Logger.Info("Services initialized successfully");
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to initialize services", ex);
                }
            });

            // Build and run Avalonia app
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Logger.Critical("Fatal error during startup", ex);
            throw;
        }
        finally
        {
            // Cleanup
            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            Logger.Info("Legion Toolkit shutdown complete");
        }
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