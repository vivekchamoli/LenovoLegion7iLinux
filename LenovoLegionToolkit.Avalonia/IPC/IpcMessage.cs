using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LenovoLegionToolkit.Avalonia.IPC
{
    public class IpcMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = string.Empty;
        public IpcCommand Command { get; set; } = IpcCommand.Unknown;
        public string? Parameters { get; set; }  // Changed to string for JSON serialized parameters
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class IpcResponse
    {
        public string Id { get; set; } = string.Empty;
        public bool Success { get; set; }
        public object? Data { get; set; }  // Changed from Result to Data to match CLI
        public string? Error { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public enum IpcCommand
    {
        Unknown,
        Ping,
        GetStatus,

        // Power Mode
        GetPowerMode,
        SetPowerMode,
        GetAvailablePowerModes,
        ListPowerModes,

        // Battery
        GetBatteryInfo,
        GetBatteryStatus,
        GetBatteryMode,
        SetConservationMode,
        SetRapidCharge,
        SetRapidChargeMode,

        // Thermal
        GetThermalInfo,
        GetThermalStatus,
        GetCpuTemperature,
        GetGpuTemperature,
        GetFanSpeed,
        SetFanSpeed,
        SetFanMode,

        // GPU
        GetGpuInfo,
        GetGpuStatus,
        GetHybridMode,
        SetHybridMode,
        SetDiscreteGpuPower,
        GetDiscreteGpuStatus,

        // RGB Keyboard
        SetRgbColor,
        SetRgbEffect,
        RgbOff,
        GetKeyboardStatus,
        SetKeyboardBrightness,
        SaveRgbProfile,
        LoadRgbProfile,
        ListRgbProfiles,

        // Profiles
        ListProfiles,
        ApplyProfile,
        CreateProfile,
        DeleteProfile,

        // Hardware
        GetHardwareInfo,
        GetCapabilities,
        CheckFeatureSupport,

        // Settings
        GetSettings,
        UpdateSettings,
        ResetSettings,

        // System
        GetSystemStatus,
        GetDaemonStatus,
        Restart,
        Shutdown,
        GetLogs,

        // Automation
        AutomationStatus,
        AutomationStart,
        AutomationStop,
        AutomationProfilesList,
        AutomationProfilesApply,
        AutomationProfilesCreateFromCurrent,
        AutomationProfilesDelete,
        AutomationProfilesSetDefault,
        AutomationRulesList,
        AutomationRulesTest,
        AutomationRulesTrigger,
        AutomationRulesDelete
    }

    public class IpcMessageBuilder
    {
        public static IpcMessage CreateCommand(IpcCommand command, string? parameters = null)
        {
            return new IpcMessage
            {
                Type = "Command",
                Command = command,
                Parameters = parameters
            };
        }

        public static IpcResponse CreateSuccessResponse(string messageId, object? data = null)
        {
            return new IpcResponse
            {
                Id = messageId,
                Success = true,
                Data = data
            };
        }

        public static IpcResponse CreateErrorResponse(string messageId, string error)
        {
            return new IpcResponse
            {
                Id = messageId,
                Success = false,
                Error = error
            };
        }
    }

    public class IpcSerializer
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        public static string Serialize<T>(T obj)
        {
            return JsonSerializer.Serialize(obj, _options);
        }

        public static T? Deserialize<T>(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json, _options);
            }
            catch
            {
                return default;
            }
        }

        public static object? DeserializeParameter(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Array => element.EnumerateArray().Select(DeserializeParameter).ToList(),
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => DeserializeParameter(p.Value)),
                _ => element.ToString()
            };
        }
    }

    public class IpcCommandInfo
    {
        public IpcCommand Command { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<IpcParameterInfo> Parameters { get; set; } = new();
        public string? ReturnType { get; set; }
    }

    public class IpcParameterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Required { get; set; }
        public object? DefaultValue { get; set; }
        public string? Description { get; set; }
    }

    public static class IpcCommandRegistry
    {
        private static readonly Dictionary<IpcCommand, IpcCommandInfo> _commands = new()
        {
            [IpcCommand.Ping] = new()
            {
                Command = IpcCommand.Ping,
                Name = "ping",
                Description = "Check if the service is running",
                ReturnType = "string"
            },

            [IpcCommand.GetPowerMode] = new()
            {
                Command = IpcCommand.GetPowerMode,
                Name = "get-power-mode",
                Description = "Get the current power mode",
                ReturnType = "PowerMode"
            },

            [IpcCommand.SetPowerMode] = new()
            {
                Command = IpcCommand.SetPowerMode,
                Name = "set-power-mode",
                Description = "Set the power mode",
                Parameters = new()
                {
                    new() { Name = "mode", Type = "PowerMode", Required = true, Description = "The power mode to set (quiet, balanced, performance, custom)" }
                },
                ReturnType = "bool"
            },

            [IpcCommand.GetBatteryInfo] = new()
            {
                Command = IpcCommand.GetBatteryInfo,
                Name = "get-battery",
                Description = "Get battery information",
                ReturnType = "BatteryInfo"
            },

            [IpcCommand.SetConservationMode] = new()
            {
                Command = IpcCommand.SetConservationMode,
                Name = "set-conservation",
                Description = "Enable or disable battery conservation mode",
                Parameters = new()
                {
                    new() { Name = "enabled", Type = "bool", Required = true, Description = "Enable (true) or disable (false) conservation mode" }
                },
                ReturnType = "bool"
            },

            [IpcCommand.GetCpuTemperature] = new()
            {
                Command = IpcCommand.GetCpuTemperature,
                Name = "get-cpu-temp",
                Description = "Get current CPU temperature",
                ReturnType = "double"
            },

            [IpcCommand.GetGpuTemperature] = new()
            {
                Command = IpcCommand.GetGpuTemperature,
                Name = "get-gpu-temp",
                Description = "Get current GPU temperature",
                ReturnType = "double"
            },

            [IpcCommand.GetFanSpeed] = new()
            {
                Command = IpcCommand.GetFanSpeed,
                Name = "get-fan-speed",
                Description = "Get fan speeds",
                Parameters = new()
                {
                    new() { Name = "fanId", Type = "int", Required = false, Description = "Fan ID (0=CPU, 1=GPU), omit for all fans" }
                },
                ReturnType = "FanInfo[]"
            },

            [IpcCommand.SetFanSpeed] = new()
            {
                Command = IpcCommand.SetFanSpeed,
                Name = "set-fan-speed",
                Description = "Set fan speed",
                Parameters = new()
                {
                    new() { Name = "fanId", Type = "int", Required = true, Description = "Fan ID (0=CPU, 1=GPU)" },
                    new() { Name = "speed", Type = "int", Required = true, Description = "Target speed in RPM" }
                },
                ReturnType = "bool"
            }
        };

        public static IpcCommandInfo? GetCommandInfo(IpcCommand command)
        {
            return _commands.GetValueOrDefault(command);
        }

        public static IpcCommandInfo? GetCommandInfo(string name)
        {
            return _commands.Values.FirstOrDefault(c => c.Name == name);
        }

        public static IEnumerable<IpcCommandInfo> GetAllCommands()
        {
            return _commands.Values;
        }
    }
}