using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using LenovoLegionToolkit.Avalonia.ViewModels;
using LenovoLegionToolkit.Avalonia.Views;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.SystemTray
{
    public class SystemTrayIcon : IDisposable
    {
        private global::Avalonia.Controls.TrayIcon? _icon;
        private readonly MainViewModel _mainViewModel;
        private readonly Window _mainWindow;
        private readonly CompositeDisposable _disposables = new();
        private readonly TrayMenu _menu;
        private readonly NotificationService _notificationService;

        public SystemTrayIcon(MainViewModel mainViewModel, Window mainWindow, NotificationService notificationService)
        {
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _menu = new TrayMenu(mainViewModel);
        }

        public void Initialize()
        {
            try
            {
                // Create the tray icon
                _icon = new global::Avalonia.Controls.TrayIcon
                {
                    ToolTipText = "Legion Toolkit",
                    Icon = GetIcon(),
                    IsVisible = true,
                    Menu = _menu.CreateMenu()
                };

                // Handle double-click to show/hide window
                _icon.Clicked += OnTrayIconClicked;

                // Subscribe to main window state changes
                _mainWindow.PropertyChanged += OnMainWindowPropertyChanged;

                // Subscribe to status updates
                Observable.Interval(TimeSpan.FromSeconds(10))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ => UpdateTooltip())
                    .DisposeWith(_disposables);

                Logger.Info("System tray icon initialized");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize tray icon", ex);
            }
        }

        private WindowIcon GetIcon()
        {
            try
            {
                // Try to load the application icon
                var assembly = Application.Current?.GetType().Assembly;
                if (assembly != null)
                {
                    var iconStream = assembly.GetManifestResourceStream("LenovoLegionToolkit.Avalonia.Assets.icon.ico");
                    if (iconStream != null)
                    {
                        return new WindowIcon(iconStream);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to load tray icon: {ex.Message}");
            }

            // Fallback to null - tray will use default
            return null!;
        }

        private void OnTrayIconClicked(object? sender, EventArgs e)
        {
            ToggleMainWindow();
        }

        private void OnMainWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Window.WindowStateProperty)
            {
                UpdateTrayMenuState();
            }
        }

        public void ToggleMainWindow()
        {
            if (_mainWindow.WindowState == WindowState.Minimized || !_mainWindow.IsVisible)
            {
                ShowMainWindow();
            }
            else
            {
                HideMainWindow();
            }
        }

        public void ShowMainWindow()
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _mainWindow.Topmost = true;
            _mainWindow.Topmost = false;
            UpdateTrayMenuState();
        }

        public void HideMainWindow()
        {
            _mainWindow.Hide();
            UpdateTrayMenuState();
        }

        private void UpdateTooltip()
        {
            try
            {
                if (_icon != null && _mainViewModel != null)
                {
                    var status = $"Legion Toolkit\n" +
                                $"Power: {_mainViewModel.StatusText}";
                    _icon.ToolTipText = status;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to update tray tooltip: {ex.Message}");
            }
        }

        private void UpdateTrayMenuState()
        {
            _menu.UpdateShowHideText(_mainWindow.IsVisible);
        }

        public void ShowNotification(string title, string message, NotificationType type = NotificationType.Information)
        {
            _notificationService.Show(title, message, type);
        }

        public void ShowBalloon(string title, string message, int duration = 3000)
        {
            try
            {
                // Note: Avalonia.Tray doesn't have built-in balloon notifications
                // We'll use the notification service instead
                _notificationService.Show(title, message, NotificationType.Information);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to show balloon: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_icon != null)
                {
                    _icon.Clicked -= OnTrayIconClicked;
                    _icon.IsVisible = false;
                    _icon.Dispose();
                    _icon = null;
                }

                if (_mainWindow != null)
                {
                    _mainWindow.PropertyChanged -= OnMainWindowPropertyChanged;
                }

                _disposables?.Dispose();
                _menu?.Dispose();

                Logger.Info("System tray icon disposed");
            }
            catch (Exception ex)
            {
                Logger.Error("Error disposing tray icon", ex);
            }
        }
    }
}