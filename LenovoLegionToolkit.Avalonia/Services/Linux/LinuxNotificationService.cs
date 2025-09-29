using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.Services.Linux
{
    public class LinuxNotificationService : INotificationService
    {
        public event EventHandler<Notification>? NotificationShown;

        private readonly bool _notifySendAvailable;

        public LinuxNotificationService()
        {
            _notifySendAvailable = CheckNotifySendAvailable();

            if (!_notifySendAvailable)
            {
                Logger.Warning("notify-send is not available. Desktop notifications will be disabled.");
            }
        }

        public async Task ShowNotificationAsync(Notification notification)
        {
            if (_notifySendAvailable)
            {
                await ShowLinuxNotification(notification);
            }
            else
            {
                // Fallback to console output
                Console.WriteLine($"[{notification.Type}] {notification.Title}: {notification.Message}");
            }

            NotificationShown?.Invoke(this, notification);
        }

        public Task ShowNotificationAsync(string title, string message, NotificationType type = NotificationType.Information)
        {
            return ShowNotificationAsync(new Notification
            {
                Title = title,
                Message = message,
                Type = type
            });
        }

        public Task ShowSuccessAsync(string message)
        {
            return ShowNotificationAsync(new Notification
            {
                Title = "Success",
                Message = message,
                Type = NotificationType.Success
            });
        }

        public Task ShowWarningAsync(string message)
        {
            return ShowNotificationAsync(new Notification
            {
                Title = "Warning",
                Message = message,
                Type = NotificationType.Warning
            });
        }

        public Task ShowErrorAsync(string message)
        {
            return ShowNotificationAsync(new Notification
            {
                Title = "Error",
                Message = message,
                Type = NotificationType.Error
            });
        }

        public Task ShowInfoAsync(string message)
        {
            return ShowNotificationAsync(new Notification
            {
                Title = "Information",
                Message = message,
                Type = NotificationType.Information
            });
        }

        private bool CheckNotifySendAvailable()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "which",
                        Arguments = "notify-send",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit(1000);
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task ShowLinuxNotification(Notification notification)
        {
            try
            {
                // Map notification type to urgency
                var urgency = notification.Type switch
                {
                    NotificationType.Error => "critical",
                    NotificationType.Warning => "normal",
                    NotificationType.Success => "normal",
                    NotificationType.Information => "low",
                    _ => "low"
                };

                // Map notification type to icon
                var icon = notification.Type switch
                {
                    NotificationType.Error => "dialog-error",
                    NotificationType.Warning => "dialog-warning",
                    NotificationType.Success => "dialog-information",
                    NotificationType.Information => "dialog-information",
                    _ => "dialog-information"
                };

                // Build arguments
                var args = $"-u {urgency} -i {icon}";

                // Add timeout if specified
                if (notification.Duration.HasValue)
                {
                    args += $" -t {(int)notification.Duration.Value.TotalMilliseconds}";
                }

                // Add category
                args += " -c \"device\"";

                // Add app name
                args += " -a \"Legion Toolkit\"";

                // Add title and message
                args += $" \"{EscapeForShell(notification.Title)}\" \"{EscapeForShell(notification.Message)}\"";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "notify-send",
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    Logger.Warning($"notify-send failed: {error}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to show notification", ex);

                // Fallback to console
                Console.WriteLine($"[{notification.Type}] {notification.Title}: {notification.Message}");
            }
        }

        private static string EscapeForShell(string input)
        {
            // Escape special characters for shell
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("$", "\\$")
                .Replace("`", "\\`")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }
    }
}