using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;
using LenovoLegionToolkit.Avalonia.ViewModels;
using LenovoLegionToolkit.Avalonia.Views;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Logger.Info("Starting application framework initialization");

                // Get service provider from Program
                var serviceProvider = Program.ServiceProvider;
                if (serviceProvider == null)
                {
                    Logger.Critical("Service provider is null!");
                    throw new InvalidOperationException("Service provider not initialized");
                }

                Logger.Info("Service provider obtained");

                // Create main window with view model from DI
                var mainViewModel = serviceProvider.GetRequiredService<MainViewModel>();
                Logger.Info("MainViewModel created");

                var mainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };
                Logger.Info("MainWindow created");

                desktop.MainWindow = mainWindow;
                desktop.ShutdownRequested += OnShutdownRequested;

                Logger.Info("Main window assigned to desktop lifetime");

                // Initialize the main view model after window is set
                try
                {
                    mainViewModel.Initialize();
                    Logger.Info("MainViewModel initialized");
                }
                catch (Exception initEx)
                {
                    Logger.Error("Failed to initialize MainViewModel", initEx);
                    // Continue anyway - window should still show
                }

                // Force show the window
                mainWindow.Show();
                mainWindow.Activate();
                Logger.Info("Window Show() and Activate() called");

                Logger.Info("Application framework initialization completed");
            }
            else
            {
                Logger.Error("ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime");
            }
        }
        catch (Exception ex)
        {
            Logger.Critical("Failed to initialize application", ex);
            Console.Error.WriteLine($"CRITICAL ERROR: {ex}");

            // Try to show error to user
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Console.Error.WriteLine("Shutting down due to initialization failure");
                desktop.Shutdown(1);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        Logger.Info("Application shutdown requested");

        // Cleanup view models
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.Cleanup();
        }
    }
}
