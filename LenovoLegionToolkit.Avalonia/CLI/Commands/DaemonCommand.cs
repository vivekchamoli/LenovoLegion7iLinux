using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.IPC;

namespace LenovoLegionToolkit.Avalonia.CLI.Commands
{
    public static class DaemonCommand
    {
        public static async Task StartDaemonAsync(bool jsonOutput)
        {
            try
            {
                // Check if daemon is already running
                var client = new CliClient();
                if (await client.CheckDaemonStatusAsync())
                {
                    if (jsonOutput)
                    {
                        var response = new { success = false, error = "Daemon is already running" };
                        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(response));
                    }
                    else
                    {
                        Console.WriteLine("Legion Toolkit daemon is already running");
                    }
                    return;
                }

                // Start the daemon process
                var startInfo = new ProcessStartInfo
                {
                    FileName = "LenovoLegionToolkit.Avalonia",
                    Arguments = "--daemon",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var process = Process.Start(startInfo);
                if (process != null)
                {
                    // Give it a moment to start
                    await Task.Delay(2000);

                    // Check if it started successfully
                    if (await client.CheckDaemonStatusAsync())
                    {
                        if (jsonOutput)
                        {
                            var response = new { success = true, pid = process.Id };
                            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(response));
                        }
                        else
                        {
                            Console.WriteLine($"✓ Legion Toolkit daemon started (PID: {process.Id})");
                        }
                    }
                    else
                    {
                        if (jsonOutput)
                        {
                            var response = new { success = false, error = "Daemon failed to start" };
                            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(response));
                        }
                        else
                        {
                            Console.Error.WriteLine("✗ Failed to start daemon");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (jsonOutput)
                {
                    var response = new { success = false, error = ex.Message };
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(response));
                }
                else
                {
                    Console.Error.WriteLine($"✗ Error starting daemon: {ex.Message}");
                }
            }
        }

        public static async Task StopDaemonAsync(CliClient client, bool jsonOutput)
        {
            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.Shutdown
            });

            if (response != null)
            {
                if (jsonOutput)
                {
                    client.PrintResponse(response, true);
                }
                else
                {
                    if (response.Success)
                    {
                        Console.WriteLine("✓ Legion Toolkit daemon stopped");
                    }
                    else
                    {
                        Console.Error.WriteLine($"✗ Failed to stop daemon: {response.Error}");
                    }
                }
            }
            else
            {
                if (jsonOutput)
                {
                    var errorResponse = new { success = false, error = "Daemon is not running" };
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(errorResponse));
                }
                else
                {
                    Console.Error.WriteLine("✗ Daemon is not running");
                }
            }
        }

        public static async Task RestartDaemonAsync(CliClient client, bool jsonOutput)
        {
            if (!jsonOutput)
            {
                Console.WriteLine("Restarting Legion Toolkit daemon...");
            }

            // Stop the daemon
            var stopResponse = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.Shutdown
            });

            if (stopResponse != null && stopResponse.Success)
            {
                // Wait for it to fully stop
                await Task.Delay(2000);
            }

            // Start it again
            await StartDaemonAsync(jsonOutput);
        }

        public static async Task GetDaemonStatusAsync(CliClient client, bool jsonOutput)
        {
            var isRunning = await client.CheckDaemonStatusAsync();

            if (isRunning)
            {
                // Try to get more detailed status
                var response = await client.SendCommandAsync(new IpcMessage
                {
                    Command = IpcCommand.GetDaemonStatus
                });

                if (response != null)
                {
                    if (jsonOutput)
                    {
                        client.PrintResponse(response, true);
                    }
                    else
                    {
                        if (response.Success && response.Data != null)
                        {
                            try
                            {
                                DaemonStatus? status = null;
                                if (response.Data is System.Text.Json.JsonElement element)
                                {
                                    status = System.Text.Json.JsonSerializer.Deserialize<DaemonStatus>(element.GetRawText());
                                }

                                if (status != null)
                                {
                                    Console.WriteLine("Legion Toolkit Daemon Status");
                                    Console.WriteLine("===========================");
                                    Console.WriteLine($"  Status:         Running");
                                    Console.WriteLine($"  PID:            {status.ProcessId}");
                                    Console.WriteLine($"  Version:        {status.Version}");
                                    Console.WriteLine($"  Uptime:         {status.Uptime}");
                                    Console.WriteLine($"  Memory Usage:   {status.MemoryUsageMB:F1} MB");
                                    Console.WriteLine($"  IPC Clients:    {status.ConnectedClients}");
                                    Console.WriteLine($"  Auto-start:     {(status.AutoStartEnabled ? "Enabled" : "Disabled")}");
                                }
                                else
                                {
                                    Console.WriteLine("✓ Legion Toolkit daemon is running");
                                }
                            }
                            catch
                            {
                                Console.WriteLine("✓ Legion Toolkit daemon is running");
                            }
                        }
                        else
                        {
                            Console.WriteLine("✓ Legion Toolkit daemon is running");
                        }
                    }
                }
                else
                {
                    if (jsonOutput)
                    {
                        var statusResponse = new { running = true };
                        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(statusResponse));
                    }
                    else
                    {
                        Console.WriteLine("✓ Legion Toolkit daemon is running");
                    }
                }
            }
            else
            {
                if (jsonOutput)
                {
                    var response = new { running = false };
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(response));
                }
                else
                {
                    Console.WriteLine("✗ Legion Toolkit daemon is not running");
                    Console.WriteLine("  Start it with: legion-toolkit daemon start");
                }
            }
        }

        private class DaemonStatus
        {
            public int ProcessId { get; set; }
            public string Version { get; set; } = "Unknown";
            public string Uptime { get; set; } = "Unknown";
            public double MemoryUsageMB { get; set; }
            public int ConnectedClients { get; set; }
            public bool AutoStartEnabled { get; set; }
        }
    }
}