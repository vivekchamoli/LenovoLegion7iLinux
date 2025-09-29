using System;
using System.Text.Json;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.IPC;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.CLI.Commands
{
    public static class AutomationCommand
    {
        public static async Task GetStatusAsync(CliClient client, bool json = false)
        {
            try
            {
                if (!CliClient.IsDaemonRunning())
                {
                    var message = "Daemon is not running. Start it with: legion-toolkit daemon start";
                    if (json)
                    {
                        var result = new { success = false, error = message };
                        Console.WriteLine(JsonSerializer.Serialize(result));
                    }
                    else
                    {
                        Console.Error.WriteLine(message);
                    }
                    return;
                }

                var response = await client.SendCommandAsync(new IpcMessage
                {
                    Command = IpcCommand.AutomationStatus,
                    Parameters = string.Empty
                });

                if (response?.Success == true)
                {
                    if (json)
                    {
                        Console.WriteLine(response.Data);
                    }
                    else
                    {
                        var dataString = response.Data.ToString() ?? "{}";
                        var data = JsonSerializer.Deserialize<JsonElement>(dataString);
                        Console.WriteLine($"Automation Status: {data.GetProperty("isRunning").GetBoolean()}");
                        Console.WriteLine($"Enabled: {data.GetProperty("enabled").GetBoolean()}");
                        Console.WriteLine($"Profiles: {data.GetProperty("profileCount").GetInt32()}");
                        Console.WriteLine($"Rules: {data.GetProperty("ruleCount").GetInt32()} ({data.GetProperty("enabledRuleCount").GetInt32()} enabled)");

                        if (data.TryGetProperty("currentProfile", out var profile) && profile.ValueKind != JsonValueKind.Null)
                        {
                            Console.WriteLine($"Current Profile: {profile.GetString()}");
                        }
                    }
                }
                else
                {
                    var error = response?.Error ?? "Failed to get automation status";
                    if (json)
                    {
                        var result = new { success = false, error };
                        Console.WriteLine(JsonSerializer.Serialize(result));
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get automation status", ex);
                if (json)
                {
                    var result = new { success = false, error = ex.Message };
                    Console.WriteLine(JsonSerializer.Serialize(result));
                }
                else
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        public static async Task StartAsync(CliClient client, bool json = false)
        {
            try
            {
                if (!CliClient.IsDaemonRunning())
                {
                    var message = "Daemon is not running. Start it with: legion-toolkit daemon start";
                    if (json)
                    {
                        var result = new { success = false, error = message };
                        Console.WriteLine(JsonSerializer.Serialize(result));
                    }
                    else
                    {
                        Console.Error.WriteLine(message);
                    }
                    return;
                }

                var response = await client.SendCommandAsync(new IpcMessage
                {
                    Command = IpcCommand.AutomationStart,
                    Parameters = string.Empty
                });

                if (response?.Success == true)
                {
                    if (json)
                    {
                        var result = new { success = true, message = "Automation service started" };
                        Console.WriteLine(JsonSerializer.Serialize(result));
                    }
                    else
                    {
                        Console.WriteLine("Automation service started successfully");
                    }
                }
                else
                {
                    var error = response?.Error ?? "Failed to start automation service";
                    if (json)
                    {
                        var result = new { success = false, error };
                        Console.WriteLine(JsonSerializer.Serialize(result));
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to start automation", ex);
                if (json)
                {
                    var result = new { success = false, error = ex.Message };
                    Console.WriteLine(JsonSerializer.Serialize(result));
                }
                else
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        public static async Task StopAsync(CliClient client, bool json = false)
        {
            try
            {
                if (!CliClient.IsDaemonRunning())
                {
                    var message = "Daemon is not running. Start it with: legion-toolkit daemon start";
                    if (json)
                    {
                        var result = new { success = false, error = message };
                        Console.WriteLine(JsonSerializer.Serialize(result));
                    }
                    else
                    {
                        Console.Error.WriteLine(message);
                    }
                    return;
                }

                var response = await client.SendCommandAsync(new IpcMessage
                {
                    Command = IpcCommand.AutomationStop,
                    Parameters = string.Empty
                });

                if (response?.Success == true)
                {
                    if (json)
                    {
                        var result = new { success = true, message = "Automation service stopped" };
                        Console.WriteLine(JsonSerializer.Serialize(result));
                    }
                    else
                    {
                        Console.WriteLine("Automation service stopped successfully");
                    }
                }
                else
                {
                    var error = response?.Error ?? "Failed to stop automation service";
                    if (json)
                    {
                        var result = new { success = false, error };
                        Console.WriteLine(JsonSerializer.Serialize(result));
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to stop automation", ex);
                if (json)
                {
                    var result = new { success = false, error = ex.Message };
                    Console.WriteLine(JsonSerializer.Serialize(result));
                }
                else
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                }
            }
        }
    }
}