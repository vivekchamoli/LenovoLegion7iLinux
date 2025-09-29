using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.Services.Linux
{
    public class LinuxDisplayService : IDisplayService, INotifyDisplayChanged
    {
        private const string PRESETS_DIR = ".config/legion-toolkit/display-presets/";
        private readonly string _presetsPath;
        private readonly Dictionary<string, DisplayInfo> _displays = new();
        private NightLightSettings _nightLightSettings = new();
        private bool _isWayland;

        public event EventHandler<DisplayChangedEventArgs>? DisplayChanged;

        public LinuxDisplayService()
        {
            _presetsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), PRESETS_DIR);
            Directory.CreateDirectory(_presetsPath);
            DetectDisplayServer();
        }

        private void DetectDisplayServer()
        {
            _isWayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
            Logger.Info($"Display server: {(_isWayland ? "Wayland" : "X11")}");
        }

        public async Task<List<DisplayInfo>> GetDisplaysAsync()
        {
            _displays.Clear();

            if (_isWayland)
            {
                await GetWaylandDisplaysAsync();
            }
            else
            {
                await GetXrandrDisplaysAsync();
            }

            return _displays.Values.ToList();
        }

        private async Task GetXrandrDisplaysAsync()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "xrandr",
                        Arguments = "--verbose",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                ParseXrandrOutput(output);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get displays via xrandr", ex);
            }
        }

        private void ParseXrandrOutput(string output)
        {
            var lines = output.Split('\n');
            DisplayInfo? currentDisplay = null;
            var capabilities = new DisplayCapabilities();

            foreach (var line in lines)
            {
                // Parse display connection
                var displayMatch = Regex.Match(line, @"^(\S+) (connected|disconnected)");
                if (displayMatch.Success)
                {
                    if (currentDisplay != null)
                    {
                        currentDisplay.Capabilities = capabilities;
                        _displays[currentDisplay.Id] = currentDisplay;
                    }

                    currentDisplay = new DisplayInfo
                    {
                        Id = displayMatch.Groups[1].Value,
                        Name = displayMatch.Groups[1].Value,
                        IsConnected = displayMatch.Groups[2].Value == "connected",
                        IsInternal = displayMatch.Groups[1].Value.StartsWith("eDP") ||
                                   displayMatch.Groups[1].Value.StartsWith("LVDS")
                    };

                    capabilities = new DisplayCapabilities();

                    // Check if primary
                    if (line.Contains("primary"))
                        currentDisplay.IsPrimary = true;

                    // Parse current mode
                    var modeMatch = Regex.Match(line, @"(\d+)x(\d+)\+\d+\+\d+");
                    if (modeMatch.Success)
                    {
                        currentDisplay.CurrentConfiguration = new DisplayConfiguration
                        {
                            DisplayId = currentDisplay.Id,
                            Width = int.Parse(modeMatch.Groups[1].Value),
                            Height = int.Parse(modeMatch.Groups[2].Value)
                        };
                    }
                }

                // Parse supported modes
                var resolutionMatch = Regex.Match(line.Trim(), @"^\s*(\d+)x(\d+)\s+(.+)");
                if (resolutionMatch.Success && currentDisplay != null)
                {
                    var width = int.Parse(resolutionMatch.Groups[1].Value);
                    var height = int.Parse(resolutionMatch.Groups[2].Value);
                    var refreshRates = new List<int>();

                    // Parse refresh rates
                    var rateMatches = Regex.Matches(resolutionMatch.Groups[3].Value, @"(\d+\.\d+)\*?");
                    foreach (Match rateMatch in rateMatches)
                    {
                        var rate = (int)Math.Round(double.Parse(rateMatch.Groups[1].Value));
                        refreshRates.Add(rate);

                        if (rateMatch.Value.Contains("*") && currentDisplay.CurrentConfiguration != null)
                        {
                            currentDisplay.CurrentConfiguration.RefreshRate = rate;
                        }
                    }

                    var resolution = capabilities.SupportedResolutions.FirstOrDefault(r => r.Width == width && r.Height == height);
                    if (resolution == null)
                    {
                        resolution = new Resolution { Width = width, Height = height };
                        capabilities.SupportedResolutions.Add(resolution);
                    }
                    resolution.RefreshRates.AddRange(refreshRates);
                }

                // Parse EDID for physical size
                if (line.Contains("mm x") && currentDisplay != null)
                {
                    var sizeMatch = Regex.Match(line, @"(\d+)mm x (\d+)mm");
                    if (sizeMatch.Success)
                    {
                        capabilities.PhysicalWidth = double.Parse(sizeMatch.Groups[1].Value);
                        capabilities.PhysicalHeight = double.Parse(sizeMatch.Groups[2].Value);
                    }
                }
            }

            // Add last display
            if (currentDisplay != null)
            {
                currentDisplay.Capabilities = capabilities;
                _displays[currentDisplay.Id] = currentDisplay;
            }
        }

        private async Task GetWaylandDisplaysAsync()
        {
            // Wayland display enumeration is compositor-specific
            // Try common methods

            // Method 1: wlr-randr (for wlroots compositors)
            if (await TryWlrRandr())
                return;

            // Method 2: Read from /sys/class/drm
            await ReadDrmDisplays();
        }

        private async Task<bool> TryWlrRandr()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "wlr-randr",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    ParseWlrRandrOutput(output);
                    return true;
                }
            }
            catch
            {
                // wlr-randr not available
            }

            return false;
        }

        private void ParseWlrRandrOutput(string output)
        {
            // Parse wlr-randr output format
            // Similar to xrandr parsing but adapted for wlr-randr format
            Logger.Debug("Parsing wlr-randr output");
        }

        private async Task ReadDrmDisplays()
        {
            try
            {
                var drmPath = "/sys/class/drm";
                if (!Directory.Exists(drmPath))
                    return;

                foreach (var card in Directory.GetDirectories(drmPath, "card*"))
                {
                    foreach (var connector in Directory.GetDirectories(card))
                    {
                        var name = Path.GetFileName(connector);
                        if (!name.Contains("-"))
                            continue;

                        var statusFile = Path.Combine(connector, "status");
                        if (!File.Exists(statusFile))
                            continue;

                        var status = await File.ReadAllTextAsync(statusFile);
                        var isConnected = status.Trim() == "connected";

                        if (isConnected)
                        {
                            var display = new DisplayInfo
                            {
                                Id = name,
                                Name = name,
                                IsConnected = true,
                                IsInternal = name.StartsWith("eDP") || name.StartsWith("LVDS")
                            };

                            // Read EDID if available
                            var edidFile = Path.Combine(connector, "edid");
                            if (File.Exists(edidFile))
                            {
                                await ParseEdid(edidFile, display);
                            }

                            _displays[display.Id] = display;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to read DRM displays", ex);
            }
        }

        private async Task ParseEdid(string edidFile, DisplayInfo display)
        {
            try
            {
                var edidBytes = await File.ReadAllBytesAsync(edidFile);
                if (edidBytes.Length >= 128)
                {
                    // Parse EDID header for manufacturer and model
                    // This is a simplified EDID parser
                    // Bytes 8-9: Manufacturer ID
                    // Bytes 10-11: Product code
                    // Bytes 12-15: Serial number
                    // Bytes 54-125: Descriptor blocks
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to parse EDID: {ex.Message}");
            }
        }

        public async Task<DisplayInfo?> GetPrimaryDisplayAsync()
        {
            var displays = await GetDisplaysAsync();
            return displays.FirstOrDefault(d => d.IsPrimary) ?? displays.FirstOrDefault(d => d.IsInternal);
        }

        public async Task<DisplayConfiguration?> GetDisplayConfigurationAsync(string displayId)
        {
            if (_displays.TryGetValue(displayId, out var display))
            {
                return display.CurrentConfiguration;
            }

            await GetDisplaysAsync();
            return _displays.TryGetValue(displayId, out display) ? display.CurrentConfiguration : null;
        }

        public async Task<bool> SetRefreshRateAsync(string displayId, int refreshRate)
        {
            try
            {
                if (_isWayland)
                {
                    return await SetWaylandRefreshRate(displayId, refreshRate);
                }
                else
                {
                    return await SetXrandrRefreshRate(displayId, refreshRate);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set refresh rate: {ex.Message}", ex);
                return false;
            }
        }

        private async Task<bool> SetXrandrRefreshRate(string displayId, int refreshRate)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "xrandr",
                    Arguments = $"--output {displayId} --rate {refreshRate}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                DisplayChanged?.Invoke(this, new DisplayChangedEventArgs
                {
                    DisplayId = displayId,
                    ChangeType = DisplayChangeType.RefreshRateChanged,
                    NewValue = refreshRate
                });
                return true;
            }

            return false;
        }

        private async Task<bool> SetWaylandRefreshRate(string displayId, int refreshRate)
        {
            // Try wlr-randr
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "wlr-randr",
                    Arguments = $"--output {displayId} --mode current@{refreshRate}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }

        public async Task<bool> SetResolutionAsync(string displayId, int width, int height)
        {
            try
            {
                if (_isWayland)
                {
                    return await SetWaylandResolution(displayId, width, height);
                }
                else
                {
                    return await SetXrandrResolution(displayId, width, height);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set resolution: {ex.Message}", ex);
                return false;
            }
        }

        private async Task<bool> SetXrandrResolution(string displayId, int width, int height)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "xrandr",
                    Arguments = $"--output {displayId} --mode {width}x{height}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                DisplayChanged?.Invoke(this, new DisplayChangedEventArgs
                {
                    DisplayId = displayId,
                    ChangeType = DisplayChangeType.ResolutionChanged,
                    NewValue = new { Width = width, Height = height }
                });
                return true;
            }

            return false;
        }

        private async Task<bool> SetWaylandResolution(string displayId, int width, int height)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "wlr-randr",
                    Arguments = $"--output {displayId} --mode {width}x{height}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }

        public async Task<bool> SetHdrAsync(string displayId, bool enabled)
        {
            // HDR support on Linux is limited and depends on kernel/driver support
            // This is a placeholder for future implementation
            Logger.Warning("HDR control is not yet fully supported on Linux");
            return await Task.FromResult(false);
        }

        public async Task<bool> SetNightLightAsync(bool enabled, int temperature = 4500)
        {
            try
            {
                _nightLightSettings.Enabled = enabled;
                _nightLightSettings.Temperature = temperature;

                // Use redshift or gammastep for color temperature adjustment
                if (enabled)
                {
                    return await EnableNightLight(temperature);
                }
                else
                {
                    return await DisableNightLight();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set night light: {ex.Message}", ex);
                return false;
            }
        }

        public async Task<bool> SetHdrEnabledAsync(bool enabled)
        {
            var primaryDisplay = await GetPrimaryDisplayAsync();
            if (primaryDisplay != null)
            {
                return await SetHdrAsync(primaryDisplay.Id, enabled);
            }
            return false;
        }

        public async Task<bool> SetNightLightEnabledAsync(bool enabled)
        {
            return await SetNightLightAsync(enabled);
        }

        private async Task<bool> EnableNightLight(int temperature)
        {
            // Try redshift first
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "redshift",
                    Arguments = $"-P -O {temperature}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }

        private async Task<bool> DisableNightLight()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "redshift",
                    Arguments = "-x",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }

        public Task<NightLightSettings?> GetNightLightSettingsAsync()
        {
            return Task.FromResult<NightLightSettings?>(_nightLightSettings);
        }

        public Task<bool> SetNightLightScheduleAsync(TimeSpan startTime, TimeSpan endTime)
        {
            _nightLightSettings.UseSchedule = true;
            _nightLightSettings.ScheduleStartTime = startTime;
            _nightLightSettings.ScheduleEndTime = endTime;
            return Task.FromResult(true);
        }

        public async Task<bool> SetBrightnessAsync(string displayId, int brightness)
        {
            try
            {
                // Try DDC/CI first (requires i2c-dev)
                if (await SetDdcBrightness(displayId, brightness))
                    return true;

                // Fall back to xrandr brightness (software adjustment)
                return await SetXrandrBrightness(displayId, brightness);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set brightness: {ex.Message}", ex);
                return false;
            }
        }

        private async Task<bool> SetDdcBrightness(string displayId, int brightness)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ddcutil",
                        Arguments = $"setvcp 10 {brightness}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> SetXrandrBrightness(string displayId, int brightness)
        {
            var normalizedBrightness = brightness / 100.0;
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "xrandr",
                    Arguments = $"--output {displayId} --brightness {normalizedBrightness:F2}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }

        public async Task<int> GetBrightnessAsync(string displayId)
        {
            try
            {
                // Try DDC/CI first
                var ddcBrightness = await GetDdcBrightness(displayId);
                if (ddcBrightness >= 0)
                    return ddcBrightness;

                // Default
                return 100;
            }
            catch
            {
                return 100;
            }
        }

        private async Task<int> GetDdcBrightness(string displayId)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ddcutil",
                        Arguments = "getvcp 10",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    var match = Regex.Match(output, @"current value\s*=\s*(\d+)");
                    if (match.Success)
                    {
                        return int.Parse(match.Groups[1].Value);
                    }
                }
            }
            catch { }

            return -1;
        }

        public Task<bool> SetColorProfileAsync(string displayId, ColorProfile profile)
        {
            // Color profile management would require integration with colord
            Logger.Warning("Color profile management not yet implemented");
            return Task.FromResult(false);
        }

        public Task<bool> SetOverscanAsync(string displayId, int overscan)
        {
            // Overscan adjustment via xrandr transform
            Logger.Warning("Overscan adjustment not yet implemented");
            return Task.FromResult(false);
        }

        public async Task<bool> ApplyDisplayPresetAsync(string presetName)
        {
            try
            {
                var presetFile = Path.Combine(_presetsPath, $"{presetName}.json");
                if (!File.Exists(presetFile))
                    return false;

                var json = await File.ReadAllTextAsync(presetFile);
                var preset = JsonSerializer.Deserialize<DisplayPreset>(json);

                if (preset == null)
                    return false;

                foreach (var config in preset.DisplayConfigurations.Values)
                {
                    await SetResolutionAsync(config.DisplayId, config.Width, config.Height);
                    await SetRefreshRateAsync(config.DisplayId, config.RefreshRate);
                    await SetBrightnessAsync(config.DisplayId, config.Brightness);
                }

                Logger.Info($"Applied display preset: {presetName}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to apply display preset: {ex.Message}", ex);
                return false;
            }
        }

        public async Task<bool> SaveDisplayPresetAsync(string presetName, DisplayConfiguration config)
        {
            try
            {
                var preset = new DisplayPreset
                {
                    Name = presetName,
                    CreatedAt = DateTime.Now,
                    ModifiedAt = DateTime.Now,
                    DisplayConfigurations = new Dictionary<string, DisplayConfiguration>
                    {
                        [config.DisplayId] = config
                    }
                };

                var presetFile = Path.Combine(_presetsPath, $"{presetName}.json");
                var json = JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(presetFile, json);

                Logger.Info($"Saved display preset: {presetName}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save display preset: {ex.Message}", ex);
                return false;
            }
        }

        public Task<List<string>> GetAvailablePresetsAsync()
        {
            try
            {
                var presets = Directory.GetFiles(_presetsPath, "*.json")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .ToList();

                return Task.FromResult(presets);
            }
            catch
            {
                return Task.FromResult(new List<string>());
            }
        }
    }
}