using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;

namespace LenovoLegionToolkit.Avalonia.Services.Interfaces
{
    public interface IKeyboardService
    {
        Task<bool> IsRgbSupportedAsync();
        Task<RgbKeyboardInfo?> GetKeyboardInfoAsync();
        Task<bool> SetStaticColorAsync(byte red, byte green, byte blue, RgbKeyboardZone zone = RgbKeyboardZone.All);
        Task<bool> SetEffectAsync(RgbKeyboardEffect effect, byte speed = 5);
        Task<bool> SetBrightnessAsync(int brightness);
        Task<int> GetBrightnessAsync();
        Task<bool> SetBacklightEnabledAsync(bool enabled);
        Task<bool> TurnOffAsync();
        Task<bool> TurnOnAsync();
        Task<RgbKeyboardState?> GetCurrentStateAsync();
        Task<bool> SaveProfileAsync(string name, RgbKeyboardProfile profile);
        Task<RgbKeyboardProfile?> LoadProfileAsync(string name);
        Task<string[]> GetAvailableProfilesAsync();

        event EventHandler<RgbKeyboardState>? KeyboardStateChanged;
    }

    public interface INotifyKeyboardStateChanged
    {
        event EventHandler<RgbKeyboardState>? KeyboardStateChanged;
    }
}