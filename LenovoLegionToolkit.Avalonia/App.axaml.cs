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
                // Get service provider from Program
                var serviceProvider = Program.ServiceProvider;
                if (serviceProvider == null)
                {
                    throw new InvalidOperationException("Service provider not initialized");
                }

                // Create main window with view model from DI
                var mainViewModel = serviceProvider.GetRequiredService<MainViewModel>();
                var mainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };

                desktop.MainWindow = mainWindow;
                desktop.ShutdownRequested += OnShutdownRequested;

                // Initialize the main view model
                mainViewModel.Initialize();

                Logger.Info("Application framework initialization completed");
            }
        }
        catch (Exception ex)
        {
            Logger.Critical("Failed to initialize application", ex);

            // Show error to user if possible
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // In a real app, you'd show an error dialog here
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
