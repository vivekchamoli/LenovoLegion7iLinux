using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Avalonia.Utils
{
    public static class LinuxPlatform
    {
        private static string? _distribution;
        private static string? _kernelVersion;
        private static bool? _hasSystemd;
        private static bool? _isRunningAsRoot;
        private static bool? _hasLegionKernelModule;

        public static string Distribution => _distribution ??= DetectDistribution();
        public static string KernelVersion => _kernelVersion ??= GetKernelVersion();
        public static bool HasSystemd => _hasSystemd ??= CheckSystemd();
        public static bool IsRunningAsRoot => _isRunningAsRoot ??= CheckRoot();
        public static bool HasLegionKernelModule => _hasLegionKernelModule ??= CheckLegionModule();

        private static string DetectDistribution()
        {
            try
            {
                if (File.Exists("/etc/os-release"))
                {
                    var lines = File.ReadAllLines("/etc/os-release");
                    var nameLine = lines.FirstOrDefault(l => l.StartsWith("PRETTY_NAME="));
                    if (!string.IsNullOrEmpty(nameLine))
                    {
                        var value = nameLine.Substring("PRETTY_NAME=".Length).Trim('"');
                        return value;
                    }
                }

                if (File.Exists("/etc/lsb-release"))
                {
                    var lines = File.ReadAllLines("/etc/lsb-release");
                    var descLine = lines.FirstOrDefault(l => l.StartsWith("DISTRIB_DESCRIPTION="));
                    if (!string.IsNullOrEmpty(descLine))
                    {
                        var value = descLine.Substring("DISTRIB_DESCRIPTION=".Length).Trim('"');
                        return value;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to detect distribution", ex);
            }

            return "Unknown Linux";
        }

        private static string GetKernelVersion()
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "uname",
                    Arguments = "-r",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process != null)
                {
                    process.WaitForExit();
                    return process.StandardOutput.ReadToEnd().Trim();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get kernel version", ex);
            }

            return "Unknown";
        }

        private static bool CheckSystemd()
        {
            try
            {
                return Directory.Exists("/run/systemd/system");
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckRoot()
        {
            return Environment.GetEnvironmentVariable("EUID") == "0" ||
                   Environment.GetEnvironmentVariable("USER") == "root";
        }

        private static bool CheckLegionModule()
        {
            try
            {
                // Check if any legion module is loaded
                var modulesPath = "/proc/modules";
                if (File.Exists(modulesPath))
                {
                    var content = File.ReadAllText(modulesPath);
                    // Check for enhanced module first, then standard module
                    if (content.Contains("legion_laptop_enhanced") || content.Contains("legion_laptop"))
                    {
                        Logger.Info("Legion kernel module found: loaded");
                        return true;
                    }
                }

                // Check if either module exists but not loaded (try enhanced first)
                foreach (var moduleName in new[] { "legion_laptop_enhanced", "legion-laptop-enhanced", "legion_laptop", "legion-laptop" })
                {
                    try
                    {
                        var process = Process.Start(new ProcessStartInfo
                        {
                            FileName = "modinfo",
                            Arguments = moduleName,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });

                        if (process != null)
                        {
                            process.WaitForExit();
                            if (process.ExitCode == 0)
                            {
                                Logger.Info($"Legion kernel module found: {moduleName} (available but not loaded)");
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Try next module name
                    }
                }

                Logger.Debug("No Legion kernel module found");
            }
            catch (Exception ex)
            {
                Logger.Debug($"Legion module check failed: {ex.Message}");
            }

            return false;
        }

        public static async Task<bool> LoadLegionModuleAsync()
        {
            if (HasLegionKernelModule)
                return true;

            // Try loading enhanced module first, then standard module
            foreach (var moduleName in new[] { "legion_laptop_enhanced", "legion-laptop" })
            {
                try
                {
                    Logger.Info($"Attempting to load kernel module: {moduleName}");
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "sudo",
                        Arguments = $"modprobe {moduleName}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    });

                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        if (process.ExitCode == 0)
                        {
                            Logger.Info($"Successfully loaded kernel module: {moduleName}");
                            _hasLegionKernelModule = null; // Reset cache
                            return CheckLegionModule();
                        }
                        else
                        {
                            var error = await process.StandardError.ReadToEndAsync();
                            Logger.Debug($"Failed to load {moduleName}: {error}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Failed to load {moduleName}: {ex.Message}");
                }
            }

            Logger.Warning("Failed to load any Legion kernel module");
            return false;
        }

        public static bool HasPermission(string path)
        {
            try
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                    return false;

                var info = new FileInfo(path);
                using var stream = info.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool CanWriteTo(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    var testFile = Path.Combine(path, ".write_test");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    return true;
                }

                if (File.Exists(path))
                {
                    using var stream = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.None);
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<string?> ReadSysfsAsync(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    Logger.Warning($"Sysfs path does not exist: {path}");
                    return null;
                }

                var content = await File.ReadAllTextAsync(path);
                return content.Trim();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to read sysfs {path}", ex);
                return null;
            }
        }

        public static async Task<bool> WriteSysfsAsync(string path, string value)
        {
            try
            {
                if (!File.Exists(path))
                {
                    Logger.Warning($"Sysfs path does not exist: {path}");
                    return false;
                }

                // Try direct write first
                try
                {
                    await File.WriteAllTextAsync(path, value);
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    // Fall back to sudo
                    Logger.Info($"Direct write failed, trying with sudo: {path}");
                }

                // Use sudo for privileged write
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "sudo",
                    Arguments = $"sh -c 'echo {value} > {path}'",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                });

                if (process != null)
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode != 0)
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        Logger.Error($"Failed to write sysfs with sudo: {error}");
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to write sysfs {path}", ex);
            }

            return false;
        }

        public static string GetConfigDirectory()
        {
            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config",
                "LegionToolkit"
            );
            Directory.CreateDirectory(configDir);
            return configDir;
        }

        public static string GetDataDirectory()
        {
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LegionToolkit"
            );
            Directory.CreateDirectory(dataDir);
            return dataDir;
        }
    }
}