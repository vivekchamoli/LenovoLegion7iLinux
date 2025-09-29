using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;

namespace LenovoLegionToolkit.Avalonia.Services.Interfaces
{
    public enum NotificationType
    {
        Information,
        Success,
        Warning,
        Error
    }

    public class Notification
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; } = NotificationType.Information;
        public TimeSpan? Duration { get; set; }
        public string? ActionText { get; set; }
        public Action? Action { get; set; }
    }

    public interface INotificationService
    {
        event EventHandler<Notification>? NotificationShown;

        Task ShowNotificationAsync(Notification notification);
        Task ShowNotificationAsync(string title, string message, NotificationType type = NotificationType.Information);
        Task ShowSuccessAsync(string message);
        Task ShowWarningAsync(string message);
        Task ShowErrorAsync(string message);
        Task ShowInfoAsync(string message);
    }
}