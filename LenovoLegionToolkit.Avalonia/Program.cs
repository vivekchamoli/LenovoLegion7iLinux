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
            Logger.Info($"Command line args: {string.Join(" ", args)}");

            // Setup dependency injection
            Logger.Info("Building service provider...");
            var services = new ServiceCollection();
            services.AddLegionToolkitServices();
            ServiceProvider = services.BuildServiceProvider();
            Logger.Info("Service provider created");

            // Initialize services asynchronously in background
            // Don't wait for this - let it run in parallel with UI startup
            var initTask = Task.Run(async () =>
            {
                try
                {
                    Logger.Info("Initializing services in background...");
                    await ServiceProvider.InitializeServicesAsync();
                    Logger.Info("Services initialized successfully");
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to initialize services (non-fatal)", ex);
                }
            });

            Logger.Info("Starting Avalonia application...");

            // Build and run Avalonia app
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);

            Logger.Info("Avalonia application exited");
        }
        catch (Exception ex)
        {
            Logger.Critical("Fatal error during startup", ex);
            Console.Error.WriteLine($"FATAL: {ex}");
            throw;
        }
        finally
        {
            // Cleanup
            Logger.Info("Cleaning up...");
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