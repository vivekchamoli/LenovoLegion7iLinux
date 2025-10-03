using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.Services.Linux
{
    public class LinuxKeyboardService : IKeyboardService, INotifyKeyboardStateChanged
    {
        private const string RGB_BASE_PATH = "/sys/class/leds/platform::kbd_backlight/";
        private const string RGB_LEGION_PATH = "/sys/bus/platform/drivers/legion_laptop/PNP0C09:00/";
        private const string RGB_BRIGHTNESS_PATH = RGB_BASE_PATH + "brightness";
        private const string RGB_MAX_BRIGHTNESS_PATH = RGB_BASE_PATH + "max_brightness";

        // Legion-specific RGB paths
        private const string RGB_MODE_PATH = RGB_LEGION_PATH + "rgb_mode";
        private const string RGB_EFFECT_PATH = RGB_LEGION_PATH + "rgb_effect";
        private const string RGB_SPEED_PATH = RGB_LEGION_PATH + "rgb_speed";
        private const string RGB_ZONE1_PATH = RGB_LEGION_PATH + "rgb_zone_1";
        private const string RGB_ZONE2_PATH = RGB_LEGION_PATH + "rgb_zone_2";
        private const string RGB_ZONE3_PATH = RGB_LEGION_PATH + "rgb_zone_3";
        private const string RGB_ZONE4_PATH = RGB_LEGION_PATH + "rgb_zone_4";

        private const string PROFILES_DIR = ".config/legion-toolkit/rgb-profiles/";

        private RgbKeyboardInfo? _keyboardInfo;
        private RgbKeyboardState? _currentState;

        public event EventHandler<RgbKeyboardState>? KeyboardStateChanged;

        public Task<bool> IsRgbSupportedAsync()
        {
            try
            {
                // Check for basic keyboard backlight support
                if (!File.Exists(RGB_BRIGHTNESS_PATH))
                    return Task.FromResult(false);

                // Check for Legion RGB support
                if (Directory.Exists(RGB_LEGION_PATH))
                {
                    Logger.Info("Legion RGB keyboard detected");
                    return Task.FromResult(true);
                }

                // Check for standard RGB support
                if (File.Exists(RGB_BASE_PATH + "color"))
                {
                    Logger.Info("Standard RGB keyboard detected");
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to check RGB support", ex);
                return Task.FromResult(false);
            }
        }

        public async Task<RgbKeyboardInfo?> GetKeyboardInfoAsync()
        {
            if (_keyboardInfo != null)
                return _keyboardInfo;

            try
            {
                var info = new RgbKeyboardInfo
                {
                    IsSupported = await IsRgbSupportedAsync()
                };

                if (!info.IsSupported)
                    return info;

                // Check keyboard type
                if (File.Exists(RGB_ZONE4_PATH))
                {
                    info.Is4Zone = true;
                    info.SupportedZones = new List<RgbKeyboardZone>
                    {
                        RgbKeyboardZone.All,
                        RgbKeyboardZone.Left,
                        RgbKeyboardZone.Center,
                        RgbKeyboardZone.Right,
                        RgbKeyboardZone.WASD
                    };
                }
                else
                {
                    info.Is4Zone = false;
                    info.SupportedZones = new List<RgbKeyboardZone> { RgbKeyboardZone.All };
                }

                // Check supported effects
                if (File.Exists(RGB_EFFECT_PATH))
                {
                    info.SupportedEffects = Enum.GetValues<RgbKeyboardEffect>().ToList();
                }
                else
                {
                    info.SupportedEffects = new List<RgbKeyboardEffect> { RgbKeyboardEffect.Static };
                }

                // Get max brightness
                if (File.Exists(RGB_MAX_BRIGHTNESS_PATH))
                {
                    var maxBrightnessStr = await LinuxPlatform.ReadSysfsAsync(RGB_MAX_BRIGHTNESS_PATH);
                    if (int.TryParse(maxBrightnessStr, out var maxBrightness))
                    {
                        info.MaxBrightness = maxBrightness;
                    }
                }

                _keyboardInfo = info;
                return info;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get keyboard info", ex);
                return null;
            }
        }

        public async Task<bool> SetStaticColorAsync(byte red, byte green, byte blue, RgbKeyboardZone zone = RgbKeyboardZone.All)
        {
            try
            {
                // Set to static mode first
                if (File.Exists(RGB_MODE_PATH))
                {
                    await LinuxPlatform.WriteSysfsAsync(RGB_MODE_PATH, "1"); // Static mode
                }

                if (File.Exists(RGB_EFFECT_PATH))
                {
                    await LinuxPlatform.WriteSysfsAsync(RGB_EFFECT_PATH, "0"); // Static effect
                }

                // Convert RGB to hex format for sysfs
                var colorValue = $"{red:X2}{green:X2}{blue:X2}";

                // Apply to specified zone(s)
                if (zone == RgbKeyboardZone.All)
                {
                    if (_keyboardInfo?.Is4Zone == true)
                    {
                        await LinuxPlatform.WriteSysfsAsync(RGB_ZONE1_PATH, colorValue);
                        await LinuxPlatform.WriteSysfsAsync(RGB_ZONE2_PATH, colorValue);
                        await LinuxPlatform.WriteSysfsAsync(RGB_ZONE3_PATH, colorValue);
                        await LinuxPlatform.WriteSysfsAsync(RGB_ZONE4_PATH, colorValue);
                    }
                    else if (File.Exists(RGB_BASE_PATH + "color"))
                    {
                        await LinuxPlatform.WriteSysfsAsync(RGB_BASE_PATH + "color", colorValue);
                    }
                }
                else
                {
                    var zonePath = GetZonePath(zone);
                    if (!string.IsNullOrEmpty(zonePath) && File.Exists(zonePath))
                    {
                        await LinuxPlatform.WriteSysfsAsync(zonePath, colorValue);
                    }
                }

                // Update current state
                if (_currentState != null)
                {
                    _currentState.CurrentEffect = RgbKeyboardEffect.Static;
                    _currentState.ZoneColors[zone] = new RgbColor(red, green, blue);
                    KeyboardStateChanged?.Invoke(this, _currentState);
                }

                Logger.Info($"RGB color set to {colorValue} for zone {zone}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set RGB color", ex);
                return false;
            }
        }

        public async Task<bool> SetEffectAsync(RgbKeyboardEffect effect, byte speed = 5)
        {
            try
            {
                if (!File.Exists(RGB_EFFECT_PATH))
                {
                    Logger.Warning("RGB effects not supported on this system");
                    return false;
                }

                // Map effect to sysfs value
                var effectValue = effect switch
                {
                    RgbKeyboardEffect.Static => "0",
                    RgbKeyboardEffect.Breathing => "1",
                    RgbKeyboardEffect.Wave => "2",
                    RgbKeyboardEffect.Rainbow => "3",
                    RgbKeyboardEffect.Ripple => "4",
                    RgbKeyboardEffect.Shift => "5",
                    RgbKeyboardEffect.Pulse => "6",
                    RgbKeyboardEffect.Random => "7",
                    _ => "0"
                };

                await LinuxPlatform.WriteSysfsAsync(RGB_EFFECT_PATH, effectValue);

                // Set effect speed if supported
                if (File.Exists(RGB_SPEED_PATH))
                {
                    await LinuxPlatform.WriteSysfsAsync(RGB_SPEED_PATH, speed.ToString());
                }

                // Update current state
                if (_currentState != null)
                {
                    _currentState.CurrentEffect = effect;
                    _currentState.EffectSpeed = speed;
                    KeyboardStateChanged?.Invoke(this, _currentState);
                }

                Logger.Info($"RGB effect set to {effect} with speed {speed}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set RGB effect", ex);
                return false;
            }
        }

        public async Task<bool> SetBrightnessAsync(int brightness)
        {
            try
            {
                if (!File.Exists(RGB_BRIGHTNESS_PATH))
                    return false;

                // Normalize brightness to max brightness scale
                var info = await GetKeyboardInfoAsync();
                if (info != null)
                {
                    var scaledBrightness = (brightness * info.MaxBrightness) / 100;
                    await LinuxPlatform.WriteSysfsAsync(RGB_BRIGHTNESS_PATH, scaledBrightness.ToString());
                }
                else
                {
                    await LinuxPlatform.WriteSysfsAsync(RGB_BRIGHTNESS_PATH, brightness.ToString());
                }

                // Update current state
                if (_currentState != null)
                {
                    _currentState.Brightness = brightness;
                    KeyboardStateChanged?.Invoke(this, _currentState);
                }

                Logger.Info($"RGB brightness set to {brightness}%");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set brightness", ex);
                return false;
            }
        }

        public async Task<int> GetBrightnessAsync()
        {
            try
            {
                if (!File.Exists(RGB_BRIGHTNESS_PATH))
                    return 0;

                var brightnessStr = await LinuxPlatform.ReadSysfsAsync(RGB_BRIGHTNESS_PATH);
                if (int.TryParse(brightnessStr, out var brightness))
                {
                    // Scale to percentage
                    var info = await GetKeyboardInfoAsync();
                    if (info != null)
                    {
                        return (brightness * 100) / info.MaxBrightness;
                    }
                    return brightness;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get brightness", ex);
                return 0;
            }
        }

        public async Task<bool> SetBacklightEnabledAsync(bool enabled)
        {
            if (enabled)
            {
                return await TurnOnAsync();
            }
            else
            {
                return await TurnOffAsync();
            }
        }

        public async Task<bool> TurnOffAsync()
        {
            try
            {
                await SetBrightnessAsync(0);

                if (_currentState != null)
                {
                    _currentState.IsOn = false;
                    KeyboardStateChanged?.Invoke(this, _currentState);
                }

                Logger.Info("RGB keyboard turned off");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to turn off RGB", ex);
                return false;
            }
        }

        public async Task<bool> TurnOnAsync()
        {
            try
            {
                await SetBrightnessAsync(_currentState?.Brightness ?? 100);

                if (_currentState != null)
                {
                    _currentState.IsOn = true;
                    KeyboardStateChanged?.Invoke(this, _currentState);
                }

                Logger.Info("RGB keyboard turned on");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to turn on RGB", ex);
                return false;
            }
        }

        public async Task<RgbKeyboardState?> GetCurrentStateAsync()
        {
            try
            {
                var state = new RgbKeyboardState
                {
                    Brightness = await GetBrightnessAsync(),
                    IsOn = await GetBrightnessAsync() > 0
                };

                // Read current effect
                if (File.Exists(RGB_EFFECT_PATH))
                {
                    var effectStr = await LinuxPlatform.ReadSysfsAsync(RGB_EFFECT_PATH);
                    if (int.TryParse(effectStr, out var effectValue))
                    {
                        state.CurrentEffect = (RgbKeyboardEffect)effectValue;
                    }
                }

                // Read effect speed
                if (File.Exists(RGB_SPEED_PATH))
                {
                    var speedStr = await LinuxPlatform.ReadSysfsAsync(RGB_SPEED_PATH);
                    if (byte.TryParse(speedStr, out var speed))
                    {
                        state.EffectSpeed = speed;
                    }
                }

                // Read zone colors
                if (_keyboardInfo?.Is4Zone == true)
                {
                    state.ZoneColors[RgbKeyboardZone.Left] = await ReadZoneColor(RGB_ZONE1_PATH);
                    state.ZoneColors[RgbKeyboardZone.Center] = await ReadZoneColor(RGB_ZONE2_PATH);
                    state.ZoneColors[RgbKeyboardZone.Right] = await ReadZoneColor(RGB_ZONE3_PATH);
                    state.ZoneColors[RgbKeyboardZone.WASD] = await ReadZoneColor(RGB_ZONE4_PATH);
                }

                _currentState = state;
                return state;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get current state", ex);
                return null;
            }
        }

        public async Task<bool> SaveProfileAsync(string name, RgbKeyboardProfile profile)
        {
            try
            {
                var profilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), PROFILES_DIR);
                Directory.CreateDirectory(profilesPath);

                var profilePath = Path.Combine(profilesPath, $"{name}.json");
                var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(profilePath, json);

                Logger.Info($"RGB profile '{name}' saved");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save profile '{name}'", ex);
                return false;
            }
        }

        public async Task<RgbKeyboardProfile?> LoadProfileAsync(string name)
        {
            try
            {
                var profilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), PROFILES_DIR);
                var profilePath = Path.Combine(profilesPath, $"{name}.json");

                if (!File.Exists(profilePath))
                    return null;

                var json = await File.ReadAllTextAsync(profilePath);
                var profile = JsonSerializer.Deserialize<RgbKeyboardProfile>(json);

                Logger.Info($"RGB profile '{name}' loaded");
                return profile;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load profile '{name}'", ex);
                return null;
            }
        }

        public async Task<string[]> GetAvailableProfilesAsync()
        {
            try
            {
                var profilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), PROFILES_DIR);

                if (!Directory.Exists(profilesPath))
                    return Array.Empty<string>();

                var profiles = Directory.GetFiles(profilesPath, "*.json")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .ToArray();

                return profiles;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get available profiles", ex);
                return Array.Empty<string>();
            }
        }

        private string GetZonePath(RgbKeyboardZone zone)
        {
            return zone switch
            {
                RgbKeyboardZone.Left => RGB_ZONE1_PATH,
                RgbKeyboardZone.Center => RGB_ZONE2_PATH,
                RgbKeyboardZone.Right => RGB_ZONE3_PATH,
                RgbKeyboardZone.WASD => RGB_ZONE4_PATH,
                _ => string.Empty
            };
        }

        private async Task<RgbColor> ReadZoneColor(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return RgbColor.Black;

                var colorStr = await LinuxPlatform.ReadSysfsAsync(path);
                if (colorStr.Length == 6)
                {
                    return RgbColor.FromHex(colorStr);
                }

                return RgbColor.Black;
            }
            catch
            {
                return RgbColor.Black;
            }
        }
    }
}