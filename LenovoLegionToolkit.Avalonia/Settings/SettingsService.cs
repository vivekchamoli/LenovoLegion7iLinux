using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.Settings
{

    public class SettingsService : ISettingsService
    {
        private readonly string _settingsPath;
        private readonly string _backupPath;
        private readonly SemaphoreSlim _saveSemaphore = new(1, 1);
        private readonly JsonSerializerOptions _jsonOptions;
        private AppSettings _settings = new();
        private FileSystemWatcher? _fileWatcher;

        public AppSettings Settings => _settings;
        public event EventHandler<AppSettings>? SettingsChanged;

        public SettingsService()
        {
            var configDir = LinuxPlatform.GetConfigDirectory();
            _settingsPath = Path.Combine(configDir, "settings.json");
            _backupPath = Path.Combine(configDir, "settings.backup.json");

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Converters =
                {
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
                },
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            InitializeFileWatcher();
        }

        public AppSettings LoadSettings()
        {
            return LoadSettingsAsync().GetAwaiter().GetResult();
        }

        public async Task<AppSettings> LoadSettingsAsync()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    Logger.Info("Settings file not found, creating with defaults");
                    _settings = new AppSettings();
                    await SaveSettingsAsync(_settings);
                    return _settings;
                }

                var json = await File.ReadAllTextAsync(_settingsPath);
                var loadedSettings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);

                if (loadedSettings == null)
                {
                    Logger.Warning("Failed to deserialize settings, using defaults");
                    _settings = new AppSettings();
                    return _settings;
                }

                // Check if migration is needed
                if (loadedSettings.Version < _settings.Version)
                {
                    Logger.Info($"Migrating settings from version {loadedSettings.Version} to {_settings.Version}");
                    loadedSettings = await MigrateSettingsAsync(loadedSettings);
                }

                _settings = loadedSettings;
                Logger.Info($"Settings loaded successfully (version {_settings.Version})");

                SettingsChanged?.Invoke(this, _settings);
                return _settings;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load settings", ex);

                // Try to restore from backup
                if (File.Exists(_backupPath))
                {
                    try
                    {
                        Logger.Info("Attempting to restore from backup");
                        File.Copy(_backupPath, _settingsPath, true);
                        return await LoadSettingsAsync();
                    }
                    catch (Exception backupEx)
                    {
                        Logger.Error("Failed to restore from backup", backupEx);
                    }
                }

                _settings = new AppSettings();
                return _settings;
            }
        }

        // Keep old LoadAsync for backward compatibility
        private async Task<bool> LoadAsync()
        {
            try
            {
                await LoadSettingsAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            SaveSettingsAsync(settings).GetAwaiter().GetResult();
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            await _saveSemaphore.WaitAsync();

            try
            {
                _settings = settings;
                // Update last modified time
                _settings.LastModified = DateTime.Now;

                // Create backup of existing settings
                if (File.Exists(_settingsPath))
                {
                    try
                    {
                        File.Copy(_settingsPath, _backupPath, true);
                    }
                    catch
                    {
                        // Ignore backup errors
                    }
                }

                // Serialize settings
                var json = JsonSerializer.Serialize(_settings, _jsonOptions);

                // Write to temp file first (atomic operation)
                var tempPath = _settingsPath + ".tmp";
                await File.WriteAllTextAsync(tempPath, json);

                // Move temp file to actual path
                File.Move(tempPath, _settingsPath, true);

                Logger.Debug("Settings saved successfully");
                SettingsChanged?.Invoke(this, _settings);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save settings", ex);
                throw;
            }
            finally
            {
                _saveSemaphore.Release();
            }
        }

        // Keep old SaveAsync for backward compatibility
        private async Task<bool> SaveAsync()
        {
            try
            {
                await SaveSettingsAsync(_settings);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void ResetSettings()
        {
            try
            {
                Logger.Info("Resetting settings to defaults");

                // Keep some user preferences
                var autoStart = _settings.General.AutoStart;
                var language = _settings.General.Language;

                _settings = new AppSettings
                {
                    General =
                    {
                        AutoStart = autoStart,
                        Language = language
                    }
                };

                SaveSettings(_settings);
                SettingsChanged?.Invoke(this, _settings);

                Logger.Info("Settings reset to defaults");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to reset settings", ex);
                throw;
            }
        }

        public async Task MigrateSettingsIfNeededAsync()
        {
            if (_settings.Version < new AppSettings().Version)
            {
                _settings = await MigrateSettingsAsync(_settings);
                await SaveSettingsAsync(_settings);
            }
        }

        public void UpdateSetting<T>(Action<AppSettings> updateAction)
        {
            try
            {
                updateAction(_settings);
                Task.Run(async () => await SaveSettingsAsync(_settings));
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to update setting", ex);
            }
        }

        public T? GetSetting<T>(Func<AppSettings, T> selector)
        {
            try
            {
                return selector(_settings);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get setting", ex);
                return default;
            }
        }

        private async Task<AppSettings> MigrateSettingsAsync(AppSettings oldSettings)
        {
            try
            {
                var newSettings = new AppSettings();

                // Migrate version 1 to current
                if (oldSettings.Version == 1)
                {
                    // Copy all existing settings
                    var json = JsonSerializer.Serialize(oldSettings, _jsonOptions);
                    var migrated = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? newSettings;

                    // Update version
                    migrated.Version = newSettings.Version;

                    // Add any new default values for new properties
                    // (JsonSerializer will handle missing properties with defaults)

                    Logger.Info($"Settings migrated from version {oldSettings.Version} to {migrated.Version}");
                    return migrated;
                }

                // Return as-is if no migration needed
                return oldSettings;
            }
            catch (Exception ex)
            {
                Logger.Error("Settings migration failed", ex);
                return new AppSettings();
            }
        }

        private void InitializeFileWatcher()
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                if (string.IsNullOrEmpty(directory))
                    return;

                _fileWatcher = new FileSystemWatcher(directory)
                {
                    Filter = "settings.json",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };

                _fileWatcher.Changed += OnSettingsFileChanged;
                _fileWatcher.EnableRaisingEvents = true;

                Logger.Debug("Settings file watcher initialized");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to initialize settings file watcher: {ex.Message}");
            }
        }

        private async void OnSettingsFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // Debounce multiple change events
                await Task.Delay(500);

                Logger.Debug("External settings change detected, reloading");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to reload settings after external change", ex);
            }
        }

        public void Dispose()
        {
            _fileWatcher?.Dispose();
            _saveSemaphore?.Dispose();
        }
    }

    // Extension methods for common settings operations
    public static class SettingsServiceExtensions
    {
        public static bool IsFirstRun(this ISettingsService service)
        {
            return !File.Exists(Path.Combine(LinuxPlatform.GetConfigDirectory(), "settings.json"));
        }

        public static void SetAutoStart(this ISettingsService service, bool enabled)
        {
            service.UpdateSetting<bool>(s => s.General.AutoStart = enabled);

            // Also update systemd service or autostart file
            Task.Run(async () => await ConfigureAutoStartAsync(enabled));
        }

        private static async Task ConfigureAutoStartAsync(bool enabled)
        {
            try
            {
                var autostartDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config",
                    "autostart"
                );

                Directory.CreateDirectory(autostartDir);

                var desktopFile = Path.Combine(autostartDir, "legion-toolkit.desktop");

                if (enabled)
                {
                    var content = @"[Desktop Entry]
Type=Application
Name=Legion Toolkit
Comment=Start Legion Toolkit on login
Exec=legion-toolkit --minimized
Hidden=false
NoDisplay=false
X-GNOME-Autostart-enabled=true";

                    await File.WriteAllTextAsync(desktopFile, content);
                    Logger.Info("Autostart enabled");
                }
                else
                {
                    if (File.Exists(desktopFile))
                    {
                        File.Delete(desktopFile);
                        Logger.Info("Autostart disabled");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to configure autostart", ex);
            }
        }
    }
}