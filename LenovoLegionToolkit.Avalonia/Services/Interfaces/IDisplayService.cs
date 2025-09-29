using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;

namespace LenovoLegionToolkit.Avalonia.Services.Interfaces
{
    public interface IDisplayService
    {
        Task<List<DisplayInfo>> GetDisplaysAsync();
        Task<DisplayInfo?> GetPrimaryDisplayAsync();
        Task<DisplayConfiguration?> GetDisplayConfigurationAsync(string displayId);
        Task<bool> SetRefreshRateAsync(string displayId, int refreshRate);
        Task<bool> SetResolutionAsync(string displayId, int width, int height);
        Task<bool> SetHdrAsync(string displayId, bool enabled);
        Task<bool> SetHdrEnabledAsync(bool enabled); // Convenience method for primary display
        Task<bool> SetNightLightAsync(bool enabled, int temperature = 4500);
        Task<bool> SetNightLightEnabledAsync(bool enabled); // Convenience method
        Task<NightLightSettings?> GetNightLightSettingsAsync();
        Task<bool> SetNightLightScheduleAsync(TimeSpan startTime, TimeSpan endTime);
        Task<bool> SetBrightnessAsync(string displayId, int brightness);
        Task<int> GetBrightnessAsync(string displayId);
        Task<bool> SetColorProfileAsync(string displayId, ColorProfile profile);
        Task<bool> SetOverscanAsync(string displayId, int overscan);
        Task<bool> ApplyDisplayPresetAsync(string presetName);
        Task<bool> SaveDisplayPresetAsync(string presetName, DisplayConfiguration config);
        Task<List<string>> GetAvailablePresetsAsync();

        event EventHandler<DisplayChangedEventArgs>? DisplayChanged;
    }

    public interface INotifyDisplayChanged
    {
        event EventHandler<DisplayChangedEventArgs>? DisplayChanged;
    }

    public class DisplayChangedEventArgs : EventArgs
    {
        public string DisplayId { get; set; } = string.Empty;
        public DisplayChangeType ChangeType { get; set; }
        public object? NewValue { get; set; }
    }

    public enum DisplayChangeType
    {
        Connected,
        Disconnected,
        RefreshRateChanged,
        ResolutionChanged,
        HdrChanged,
        BrightnessChanged,
        ColorProfileChanged
    }
}