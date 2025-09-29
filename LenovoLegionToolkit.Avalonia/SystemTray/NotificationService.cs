using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.SystemTray
{
    public enum NotificationType
    {
        Information,
        Warning,
        Error,
        Success,
        Temperature,
        Battery,
        Update
    }

    public class NotificationService
    {
        private readonly ConcurrentQueue<Notification> _notificationQueue = new();
        private readonly object _lockObject = new();
        private bool _isProcessing;

        public bool EnableNotifications { get; set; } = true;
        public bool EnableSoundNotifications { get; set; } = false;
        public bool EnableTemperatureAlerts { get; set; } = true;
        public bool EnableBatteryAlerts { get; set; } = true;
        public int TemperatureThreshold { get; set; } = 85; // Celsius
        public int BatteryLowThreshold { get; set; } = 20; // Percentage

        public void Show(string title, string message, NotificationType type = NotificationType.Information)
        {
            if (!EnableNotifications)
                return;

            var notification = new Notification
            {
                Title = title,
                Message = message,
                Type = type,
                Timestamp = DateTime.Now
            };

            _notificationQueue.Enqueue(notification);
            ProcessNotificationQueue();
        }

        public void ShowTemperatureAlert(double temperature, string component = "CPU")
        {
            if (!EnableTemperatureAlerts || temperature < TemperatureThreshold)
                return;

            var level = temperature >= 95 ? "Critical" :
                       temperature >= 90 ? "High" :
                       "Warning";

            Show(
                $"Temperature {level}",
                $"{component} temperature: {temperature:F1}°C",
                NotificationType.Temperature
            );
        }

        public void ShowBatteryAlert(int level, bool isCharging)
        {
            if (!EnableBatteryAlerts)
                return;

            if (!isCharging && level <= BatteryLowThreshold)
            {
                Show(
                    "Battery Low",
                    $"Battery level: {level}%\nPlease connect charger",
                    NotificationType.Battery
                );
            }
            else if (isCharging && level >= 100)
            {
                Show(
                    "Battery Full",
                    "Battery is fully charged\nConsider unplugging to preserve battery health",
                    NotificationType.Battery
                );
            }
        }

        private void ProcessNotificationQueue()
        {
            lock (_lockObject)
            {
                if (_isProcessing)
                    return;

                _isProcessing = true;
            }

            Task.Run(async () =>
            {
                try
                {
                    while (_notificationQueue.TryDequeue(out var notification))
                    {
                        await ShowSystemNotification(notification);
                        await Task.Delay(100); // Prevent notification spam
                    }
                }
                finally
                {
                    lock (_lockObject)
                    {
                        _isProcessing = false;
                    }
                }
            });
        }

        private async Task ShowSystemNotification(Notification notification)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    await ShowLinuxNotification(notification);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    await ShowWindowsNotification(notification);
                }
                else
                {
                    Logger.Warning("Notifications not supported on this platform");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to show notification: {ex.Message}", ex);
            }
        }

        private async Task ShowLinuxNotification(Notification notification)
        {
            try
            {
                // Use notify-send for Linux desktop notifications
                var urgency = notification.Type switch
                {
                    NotificationType.Error => "critical",
                    NotificationType.Warning => "normal",
                    NotificationType.Temperature when
                        notification.Message.Contains("Critical") => "critical",
                    _ => "low"
                };

                var icon = GetLinuxIcon(notification.Type);

                var processInfo = new ProcessStartInfo
                {
                    FileName = "notify-send",
                    Arguments = $"-u {urgency} -i {icon} \"{notification.Title}\" \"{notification.Message}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                }

                // Play sound if enabled and critical
                if (EnableSoundNotifications && notification.Type == NotificationType.Error)
                {
                    PlayNotificationSound();
                }

                Logger.Debug($"Linux notification shown: {notification.Title}");
            }
            catch (Exception ex)
            {
                // Fall back to console logging if notify-send is not available
                Logger.Warning($"notify-send not available: {ex.Message}");
                Console.WriteLine($"[{notification.Type}] {notification.Title}: {notification.Message}");
            }
        }

        private async Task ShowWindowsNotification(Notification notification)
        {
            // For Windows, we would use Windows.UI.Notifications
            // This is a placeholder for cross-platform compatibility
            Logger.Info($"[{notification.Type}] {notification.Title}: {notification.Message}");
            await Task.CompletedTask;
        }

        private string GetLinuxIcon(NotificationType type)
        {
            return type switch
            {
                NotificationType.Information => "dialog-information",
                NotificationType.Warning => "dialog-warning",
                NotificationType.Error => "dialog-error",
                NotificationType.Success => "emblem-default",
                NotificationType.Temperature => "sensors-temperature",
                NotificationType.Battery => "battery-low",
                NotificationType.Update => "system-software-update",
                _ => "dialog-information"
            };
        }

        private void PlayNotificationSound()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Use paplay or aplay to play system sound
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "paplay",
                        Arguments = "/usr/share/sounds/freedesktop/stereo/dialog-error.oga",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    process?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to play notification sound: {ex.Message}");
            }
        }

        public void ShowUpdateAvailable(string version, string currentVersion)
        {
            Show(
                "Update Available",
                $"Legion Toolkit {version} is available\nCurrent version: {currentVersion}\n\nCheck Settings for update",
                NotificationType.Update
            );
        }

        public void ShowProfileApplied(string profileName)
        {
            Show(
                "Profile Applied",
                $"'{profileName}' profile has been activated",
                NotificationType.Success
            );
        }

        public void ShowPowerModeChanged(string mode)
        {
            Show(
                "Power Mode Changed",
                $"Power mode set to: {mode}",
                NotificationType.Information
            );
        }

        public void ShowError(string title, string message)
        {
            Show(title, message, NotificationType.Error);
        }

        public void ShowWarning(string title, string message)
        {
            Show(title, message, NotificationType.Warning);
        }

        public void ShowInfo(string title, string message)
        {
            Show(title, message, NotificationType.Information);
        }

        private class Notification
        {
            public string Title { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public NotificationType Type { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}