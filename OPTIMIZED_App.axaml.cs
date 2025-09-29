using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.ViewModels;
using LenovoLegionToolkit.Avalonia.Views;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia;

/// <summary>
/// OPTIMIZED Application class with proper lifecycle management and error handling
/// Fixes: Startup crashes, service initialization race conditions, proper disposal
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private MainViewModel? _mainViewModel;
    private readonly object _lockObject = new object();
    private bool _shutdownInProgress;

    public override void Initialize()
    {
        try
        {
            AvaloniaXamlLoader.Load(this);
            Logger.Info("Avalonia XAML loaded successfully");
        }
        catch (Exception ex)
        {
            Logger.Critical("Failed to load Avalonia XAML", ex);
            throw;
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Wait for service provider to be available with timeout
                var serviceProvider = WaitForServiceProvider(TimeSpan.FromSeconds(10));
                if (serviceProvider == null)
                {
                    throw new InvalidOperationException("Service provider not available within timeout");
                }

                _serviceProvider = serviceProvider;

                // Initialize main window on UI thread
                InitializeMainWindow(desktop);

                // Hook up shutdown events
                desktop.ShutdownRequested += OnShutdownRequested;
                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;

                Logger.Info("Application framework initialization completed successfully");
            }
            else
            {
                Logger.Warning("Application lifetime is not classic desktop style");
            }
        }
        catch (Exception ex)
        {
            Logger.Critical("Fatal error during application initialization", ex);
            HandleCriticalError(ex);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private IServiceProvider? WaitForServiceProvider(TimeSpan timeout)
    {
        var startTime = DateTime.Now;
        var checkInterval = TimeSpan.FromMilliseconds(50);

        while (DateTime.Now - startTime < timeout)
        {
            var serviceProvider = Program.ServiceProvider;
            if (serviceProvider != null)
            {
                Logger.Info("Service provider obtained successfully");
                return serviceProvider;
            }

            // Small delay to avoid busy waiting
            Task.Delay(checkInterval).Wait();
        }

        Logger.Error($"Service provider not available after {timeout.TotalSeconds} seconds");
        return null;
    }

    private void InitializeMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            // Create main view model with error handling
            _mainViewModel = _serviceProvider!.GetRequiredService<MainViewModel>();
            if (_mainViewModel == null)
            {
                throw new InvalidOperationException("Failed to create MainViewModel from service provider");
            }

            // Create main window
            var mainWindow = new MainWindow
            {
                DataContext = _mainViewModel
            };

            // Set main window
            desktop.MainWindow = mainWindow;

            // Initialize view model asynchronously to avoid blocking UI thread
            _ = Task.Run(async () =>
            {
                try
                {
                    await InitializeViewModelAsync();
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to initialize main view model", ex);
                }
            });

            Logger.Info("Main window initialized successfully");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to initialize main window", ex);
            throw;
        }
    }

    private async Task InitializeViewModelAsync()
    {
        try
        {
            if (_mainViewModel == null) return;

            // Initialize on UI thread if required
            if (_mainViewModel.RequiresUIThread)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _mainViewModel.Initialize();
                });
            }
            else
            {
                _mainViewModel.Initialize();
            }

            Logger.Info("Main view model initialized successfully");
        }
        catch (Exception ex)
        {
            Logger.Error("Error during view model initialization", ex);

            // Show error to user without crashing the application
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    await ShowInitializationErrorAsync(ex);
                }
                catch (Exception errorEx)
                {
                    Logger.Error("Failed to show initialization error", errorEx);
                }
            });
        }
    }

    private async Task ShowInitializationErrorAsync(Exception ex)
    {
        try
        {
            // Create a simple error dialog
            var errorWindow = new Window
            {
                Title = "Legion Toolkit - Initialization Error",
                Width = 500,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                CanResize = false
            };

            var errorContent = new StackPanel
            {
                Margin = new Thickness(20)
            };

            errorContent.Children.Add(new TextBlock
            {
                Text = "Application Initialization Error",
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            errorContent.Children.Add(new TextBlock
            {
                Text = "The application encountered an error during initialization. Some features may not work correctly.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            errorContent.Children.Add(new TextBlock
            {
                Text = $"Error: {ex.Message}",
                FontFamily = "Consolas",
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Background = Brushes.LightGray,
                Padding = new Thickness(5)
            });

            var okButton = new Button
            {
                Content = "Continue",
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0),
                Padding = new Thickness(20, 5)
            };

            okButton.Click += (s, e) => errorWindow.Close();
            errorContent.Children.Add(okButton);

            errorWindow.Content = errorContent;
            await errorWindow.ShowDialog((Window)((IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!).MainWindow!);
        }
        catch (Exception dialogEx)
        {
            Logger.Error("Failed to show error dialog", dialogEx);
        }
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        lock (_lockObject)
        {
            if (_shutdownInProgress) return;
            _shutdownInProgress = true;
        }

        Logger.Info("Application shutdown requested - starting cleanup");

        try
        {
            // Cancel shutdown temporarily to allow proper cleanup
            e.Cancel = true;

            // Perform cleanup asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    await PerformShutdownCleanupAsync();
                }
                finally
                {
                    // Now actually shutdown
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                            {
                                desktop.Shutdown(0);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Error during final shutdown", ex);
                            Environment.Exit(0); // Force exit if normal shutdown fails
                        }
                    });
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Error during shutdown request handling", ex);
            // If cleanup fails, force shutdown
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown(1);
            }
        }
    }

    private async Task PerformShutdownCleanupAsync()
    {
        var cleanupTasks = new List<Task>();

        try
        {
            // Cleanup main view model
            if (_mainViewModel != null)
            {
                cleanupTasks.Add(Task.Run(() =>
                {
                    try
                    {
                        _mainViewModel.Cleanup();
                        Logger.Info("Main view model cleanup completed");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error during main view model cleanup", ex);
                    }
                }));
            }

            // Cleanup services
            if (_serviceProvider != null)
            {
                cleanupTasks.Add(Task.Run(() =>
                {
                    try
                    {
                        // Dispose services that implement IDisposable
                        if (_serviceProvider is IDisposable disposableProvider)
                        {
                            disposableProvider.Dispose();
                            Logger.Info("Service provider disposed");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error during service provider cleanup", ex);
                    }
                }));
            }

            // Wait for all cleanup tasks with timeout
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var cleanupTask = Task.WhenAll(cleanupTasks);
            var completedTask = await Task.WhenAny(cleanupTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                Logger.Warning("Cleanup operations timed out after 5 seconds");
            }
            else
            {
                Logger.Info("All cleanup operations completed successfully");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error during shutdown cleanup", ex);
        }
    }

    private void HandleCriticalError(Exception ex)
    {
        try
        {
            // Try to show error dialog if possible
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                try
                {
                    // Create minimal error display
                    var errorMessage = $"Critical Error: {ex.Message}\n\nThe application will now exit.";

                    // Try to create a simple message box equivalent
                    var errorWindow = new Window
                    {
                        Title = "Legion Toolkit - Critical Error",
                        Width = 400,
                        Height = 200,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                    };

                    var content = new StackPanel { Margin = new Thickness(20) };
                    content.Children.Add(new TextBlock
                    {
                        Text = errorMessage,
                        TextWrapping = TextWrapping.Wrap
                    });

                    var exitButton = new Button
                    {
                        Content = "Exit",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 20, 0, 0)
                    };

                    exitButton.Click += (s, e) =>
                    {
                        desktop.Shutdown(1);
                    };

                    content.Children.Add(exitButton);
                    errorWindow.Content = content;

                    // Try to show the window
                    desktop.MainWindow = errorWindow;
                }
                catch
                {
                    // If we can't show the error window, just shutdown
                    desktop.Shutdown(1);
                }
            }
            else
            {
                // No desktop lifetime, exit immediately
                Environment.Exit(1);
            }
        }
        catch
        {
            // Last resort - force exit
            Environment.Exit(1);
        }
    }

    // Property to check if application is ready
    public bool IsInitialized => _serviceProvider != null && _mainViewModel != null;

    // Method to get service provider safely
    public IServiceProvider? GetServiceProvider() => _serviceProvider;
}