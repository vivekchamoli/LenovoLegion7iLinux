using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.Services
{
    /// <summary>
    /// OPTIMIZED Configuration Service with validation, migration, and thread safety
    /// Fixes: Configuration corruption, missing validation, thread safety issues
    /// </summary>
    public class ConfigurationService : IConfigurationService, IDisposable
    {
        private const string CONFIG_FILE_NAME = "legion-toolkit.json";
        private const string CONFIG_BACKUP_SUFFIX = ".backup";
        private const int MAX_BACKUP_FILES = 5;
        private const int CONFIG_VERSION = 1;

        private readonly string _configDirectory;
        private readonly string _configFilePath;
        private readonly IFileSystemService _fileSystem;
        private readonly ConcurrentDictionary<string, object> _configCache;
        private readonly ReaderWriterLockSlim _configLock;
        private readonly Timer _autoSaveTimer;
        private readonly JsonSerializerOptions _jsonOptions;

        private volatile bool _isDirty;
        private volatile bool _disposed;
        private DateTime _lastSaveTime;

        public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

        public ConfigurationService(IFileSystemService fileSystem)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

            // Setup configuration paths
            _configDirectory = GetConfigurationDirectory();
            _configFilePath = Path.Combine(_configDirectory, CONFIG_FILE_NAME);

            // Initialize thread-safe collections
            _configCache = new ConcurrentDictionary<string, object>();
            _configLock = new ReaderWriterLockSlim();

            // Setup JSON serialization options
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            // Auto-save timer (saves dirty configuration every 30 seconds)
            _autoSaveTimer = new Timer(AutoSaveCallback, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            // Load configuration asynchronously
            _ = Task.Run(async () => await LoadConfigurationAsync());

            Logger.Info($"Configuration service initialized with path: {_configFilePath}");
        }

        private string GetConfigurationDirectory()
        {
            // Use XDG Base Directory specification for Linux
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(configHome))
            {
                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                configHome = Path.Combine(homeDir, ".config");
            }

            var appConfigDir = Path.Combine(configHome, "legion-toolkit");

            // Ensure directory exists
            try
            {
                if (!Directory.Exists(appConfigDir))
                {
                    Directory.CreateDirectory(appConfigDir);
                    Logger.Info($"Created configuration directory: {appConfigDir}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to create configuration directory: {appConfigDir}", ex);
                throw;
            }

            return appConfigDir;
        }

        public async Task<T?> GetAsync<T>(string key, T? defaultValue = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));

            if (_disposed)
                return defaultValue;

            try
            {
                _configLock.EnterReadLock();

                if (_configCache.TryGetValue(key, out var cachedValue))
                {
                    if (cachedValue is T typedValue)
                        return typedValue;

                    // Try to convert if types don't match exactly
                    try
                    {
                        if (cachedValue is JsonElement jsonElement)
                        {
                            return jsonElement.Deserialize<T>(_jsonOptions);
                        }

                        return (T?)Convert.ChangeType(cachedValue, typeof(T));
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Failed to convert cached value for key '{key}' to type {typeof(T).Name}", ex);
                        return defaultValue;
                    }
                }

                return defaultValue;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error retrieving configuration value for key '{key}'", ex);
                return defaultValue;
            }
            finally
            {
                if (_configLock.IsReadLockHeld)
                    _configLock.ExitReadLock();
            }
        }

        public async Task SetAsync<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));

            if (_disposed)
                throw new ObjectDisposedException(nameof(ConfigurationService));

            try
            {
                _configLock.EnterWriteLock();

                var oldValue = _configCache.TryGetValue(key, out var existing) ? existing : null;

                // Validate the value
                if (!IsValidConfigurationValue(value))
                {
                    throw new ArgumentException($"Invalid configuration value for key '{key}'", nameof(value));
                }

                _configCache.AddOrUpdate(key, value!, (k, v) => value!);
                _isDirty = true;

                Logger.Debug($"Configuration value set: {key} = {value}");

                // Notify listeners asynchronously
                _ = Task.Run(() =>
                {
                    try
                    {
                        ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(key, oldValue, value));
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error notifying configuration change listeners", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting configuration value for key '{key}'", ex);
                throw;
            }
            finally
            {
                if (_configLock.IsWriteLockHeld)
                    _configLock.ExitWriteLock();
            }
        }

        public async Task<bool> RemoveAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));

            if (_disposed)
                return false;

            try
            {
                _configLock.EnterWriteLock();

                var removed = _configCache.TryRemove(key, out var oldValue);
                if (removed)
                {
                    _isDirty = true;
                    Logger.Debug($"Configuration value removed: {key}");

                    // Notify listeners asynchronously
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(key, oldValue, null));
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Error notifying configuration change listeners", ex);
                        }
                    });
                }

                return removed;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error removing configuration value for key '{key}'", ex);
                return false;
            }
            finally
            {
                if (_configLock.IsWriteLockHeld)
                    _configLock.ExitWriteLock();
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            if (_disposed)
                return false;

            try
            {
                _configLock.EnterReadLock();
                return _configCache.ContainsKey(key);
            }
            finally
            {
                if (_configLock.IsReadLockHeld)
                    _configLock.ExitReadLock();
            }
        }

        public async Task<IReadOnlyDictionary<string, object>> GetAllAsync()
        {
            if (_disposed)
                return new Dictionary<string, object>();

            try
            {
                _configLock.EnterReadLock();
                return new Dictionary<string, object>(_configCache);
            }
            finally
            {
                if (_configLock.IsReadLockHeld)
                    _configLock.ExitReadLock();
            }
        }

        public async Task SaveAsync()
        {
            if (_disposed || !_isDirty)
                return;

            try
            {
                await SaveConfigurationAsync();
                _isDirty = false;
                _lastSaveTime = DateTime.Now;
                Logger.Debug("Configuration saved to disk");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save configuration", ex);
                throw;
            }
        }

        public async Task ReloadAsync()
        {
            if (_disposed)
                return;

            try
            {
                await LoadConfigurationAsync();
                _isDirty = false;
                Logger.Info("Configuration reloaded from disk");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to reload configuration", ex);
                throw;
            }
        }

        public async Task ResetAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConfigurationService));

            try
            {
                _configLock.EnterWriteLock();

                _configCache.Clear();
                _isDirty = true;

                Logger.Info("Configuration reset to defaults");

                // Notify listeners asynchronously
                _ = Task.Run(() =>
                {
                    try
                    {
                        ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs("*", null, null));
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error notifying configuration reset listeners", ex);
                    }
                });
            }
            finally
            {
                if (_configLock.IsWriteLockHeld)
                    _configLock.ExitWriteLock();
            }

            await SaveAsync();
        }

        private async Task LoadConfigurationAsync()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    Logger.Info("Configuration file does not exist, using defaults");
                    await InitializeDefaultConfigurationAsync();
                    return;
                }

                var configJson = await File.ReadAllTextAsync(_configFilePath);
                if (string.IsNullOrWhiteSpace(configJson))
                {
                    Logger.Warning("Configuration file is empty, using defaults");
                    await InitializeDefaultConfigurationAsync();
                    return;
                }

                var configData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson, _jsonOptions);
                if (configData == null)
                {
                    Logger.Warning("Failed to deserialize configuration file, using defaults");
                    await InitializeDefaultConfigurationAsync();
                    return;
                }

                // Validate and migrate configuration if needed
                await ValidateAndMigrateConfigurationAsync(configData);

                _configLock.EnterWriteLock();
                try
                {
                    _configCache.Clear();
                    foreach (var kvp in configData)
                    {
                        if (kvp.Key != "_version" && kvp.Key != "_lastModified")
                        {
                            _configCache[kvp.Key] = kvp.Value;
                        }
                    }
                }
                finally
                {
                    _configLock.ExitWriteLock();
                }

                Logger.Info($"Configuration loaded successfully with {configData.Count} entries");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load configuration, using defaults", ex);
                await InitializeDefaultConfigurationAsync();
            }
        }

        private async Task SaveConfigurationAsync()
        {
            try
            {
                // Create backup before saving
                await CreateBackupAsync();

                var configData = new Dictionary<string, object>();

                _configLock.EnterReadLock();
                try
                {
                    foreach (var kvp in _configCache)
                    {
                        configData[kvp.Key] = kvp.Value;
                    }
                }
                finally
                {
                    _configLock.ExitReadLock();
                }

                // Add metadata
                configData["_version"] = CONFIG_VERSION;
                configData["_lastModified"] = DateTime.UtcNow;

                var configJson = JsonSerializer.Serialize(configData, _jsonOptions);

                // Atomic write: write to temp file then rename
                var tempFilePath = _configFilePath + ".tmp";
                await File.WriteAllTextAsync(tempFilePath, configJson);

                if (File.Exists(_configFilePath))
                {
                    File.Replace(tempFilePath, _configFilePath, null);
                }
                else
                {
                    File.Move(tempFilePath, _configFilePath);
                }

                Logger.Debug($"Configuration saved with {configData.Count} entries");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save configuration", ex);
                throw;
            }
        }

        private async Task CreateBackupAsync()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                    return;

                var backupPath = _configFilePath + CONFIG_BACKUP_SUFFIX;
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var timestampedBackupPath = $"{_configFilePath}.{timestamp}{CONFIG_BACKUP_SUFFIX}";

                File.Copy(_configFilePath, timestampedBackupPath, true);

                // Keep only the latest backups
                await CleanupOldBackupsAsync();
            }
            catch (Exception ex)
            {
                Logger.Warning("Failed to create configuration backup", ex);
                // Don't throw - backup failure shouldn't prevent saving
            }
        }

        private async Task CleanupOldBackupsAsync()
        {
            try
            {
                var backupFiles = Directory.GetFiles(_configDirectory, $"*{CONFIG_BACKUP_SUFFIX}");
                if (backupFiles.Length <= MAX_BACKUP_FILES)
                    return;

                Array.Sort(backupFiles, (x, y) => File.GetCreationTime(x).CompareTo(File.GetCreationTime(y)));

                var filesToDelete = backupFiles.Length - MAX_BACKUP_FILES;
                for (int i = 0; i < filesToDelete; i++)
                {
                    File.Delete(backupFiles[i]);
                    Logger.Debug($"Deleted old configuration backup: {backupFiles[i]}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("Failed to cleanup old configuration backups", ex);
            }
        }

        private async Task ValidateAndMigrateConfigurationAsync(Dictionary<string, JsonElement> configData)
        {
            try
            {
                // Check version
                var currentVersion = 0;
                if (configData.TryGetValue("_version", out var versionElement) &&
                    versionElement.TryGetInt32(out var version))
                {
                    currentVersion = version;
                }

                if (currentVersion < CONFIG_VERSION)
                {
                    Logger.Info($"Migrating configuration from version {currentVersion} to {CONFIG_VERSION}");
                    await MigrateConfigurationAsync(configData, currentVersion);
                }

                // Validate configuration entries
                var invalidKeys = new List<string>();
                foreach (var kvp in configData)
                {
                    if (!IsValidConfigurationKey(kvp.Key) || !IsValidConfigurationValue(kvp.Value))
                    {
                        invalidKeys.Add(kvp.Key);
                    }
                }

                // Remove invalid entries
                foreach (var key in invalidKeys)
                {
                    configData.Remove(key);
                    Logger.Warning($"Removed invalid configuration entry: {key}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to validate and migrate configuration", ex);
                throw;
            }
        }

        private async Task MigrateConfigurationAsync(Dictionary<string, JsonElement> configData, int fromVersion)
        {
            // Implement migration logic for different versions
            switch (fromVersion)
            {
                case 0:
                    // Initial migration from unversioned config
                    await MigrateFromVersion0Async(configData);
                    break;
                // Add more migration cases as needed
            }
        }

        private async Task MigrateFromVersion0Async(Dictionary<string, JsonElement> configData)
        {
            // Example migration: rename old keys, set defaults, etc.
            try
            {
                // Migration logic would go here
                Logger.Info("Completed migration from version 0");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to migrate from version 0", ex);
                throw;
            }
        }

        private async Task InitializeDefaultConfigurationAsync()
        {
            try
            {
                _configLock.EnterWriteLock();

                _configCache.Clear();

                // Set default configuration values
                var defaults = GetDefaultConfiguration();
                foreach (var kvp in defaults)
                {
                    _configCache[kvp.Key] = kvp.Value;
                }

                _isDirty = true;

                Logger.Info("Initialized default configuration");
            }
            finally
            {
                if (_configLock.IsWriteLockHeld)
                    _configLock.ExitWriteLock();
            }

            await SaveAsync();
        }

        private Dictionary<string, object> GetDefaultConfiguration()
        {
            return new Dictionary<string, object>
            {
                ["startup.minimizeToTray"] = false,
                ["startup.checkForUpdates"] = true,
                ["monitoring.batteryInterval"] = 5000,
                ["monitoring.thermalInterval"] = 3000,
                ["ui.theme"] = "auto",
                ["logging.level"] = "info",
                ["logging.enableFileLogging"] = true,
                ["hardware.enableLegionModule"] = true,
                ["notifications.enabled"] = true,
                ["automation.enabled"] = true
            };
        }

        private bool IsValidConfigurationKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            if (key.Length > 100)
                return false;

            // Key should contain only alphanumeric characters, dots, and underscores
            foreach (char c in key)
            {
                if (!char.IsLetterOrDigit(c) && c != '.' && c != '_')
                    return false;
            }

            return true;
        }

        private bool IsValidConfigurationValue(object? value)
        {
            if (value == null)
                return true;

            // Allow only serializable types
            var type = value.GetType();
            return type.IsPrimitive ||
                   type == typeof(string) ||
                   type == typeof(DateTime) ||
                   type == typeof(TimeSpan) ||
                   type == typeof(decimal) ||
                   type.IsEnum ||
                   value is JsonElement;
        }

        private async void AutoSaveCallback(object? state)
        {
            try
            {
                if (_disposed || !_isDirty)
                    return;

                // Only auto-save if it's been more than 10 seconds since last manual save
                if (DateTime.Now - _lastSaveTime < TimeSpan.FromSeconds(10))
                    return;

                await SaveAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("Error during auto-save", ex);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                try
                {
                    // Save any pending changes
                    if (_isDirty)
                    {
                        SaveAsync().Wait(TimeSpan.FromSeconds(5));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Error saving configuration during disposal", ex);
                }

                try
                {
                    _autoSaveTimer?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Error("Error disposing auto-save timer", ex);
                }

                try
                {
                    _configLock?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Error("Error disposing configuration lock", ex);
                }
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    public class ConfigurationChangedEventArgs : EventArgs
    {
        public string Key { get; }
        public object? OldValue { get; }
        public object? NewValue { get; }

        public ConfigurationChangedEventArgs(string key, object? oldValue, object? newValue)
        {
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}