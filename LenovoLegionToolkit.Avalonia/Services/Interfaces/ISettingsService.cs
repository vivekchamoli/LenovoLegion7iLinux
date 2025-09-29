using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Settings;

namespace LenovoLegionToolkit.Avalonia.Services.Interfaces
{
    public interface ISettingsService
    {
        AppSettings Settings { get; }
        AppSettings LoadSettings();
        Task<AppSettings> LoadSettingsAsync();
        void SaveSettings(AppSettings settings);
        Task SaveSettingsAsync(AppSettings settings);
        void ResetSettings();
        Task MigrateSettingsIfNeededAsync();
        void UpdateSetting<T>(Action<AppSettings> updateAction);
        T? GetSetting<T>(Func<AppSettings, T> selector);
    }
}