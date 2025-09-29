using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.IPC;
using LenovoLegionToolkit.Avalonia.Models;

namespace LenovoLegionToolkit.Avalonia.CLI.Commands
{
    public static class ThermalCommand
    {
        public static async Task GetThermalStatusAsync(CliClient client, bool jsonOutput)
        {
            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.GetThermalStatus
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
                            ThermalInfo? thermalInfo = null;
                            if (response.Data is JsonElement element)
                            {
                                thermalInfo = JsonSerializer.Deserialize<ThermalInfo>(element.GetRawText());
                            }

                            if (thermalInfo != null)
                            {
                                Console.WriteLine("Thermal Status:");
                                Console.WriteLine($"  CPU Temperature:    {thermalInfo.CpuTemperature:F1}°C");
                                Console.WriteLine($"  GPU Temperature:    {thermalInfo.GpuTemperature:F1}°C");
                                Console.WriteLine($"  System Temperature: {thermalInfo.SystemTemperature:F1}°C");
                                Console.WriteLine($"  CPU Fan Speed:      {thermalInfo.CpuFanRpm} RPM");
                                Console.WriteLine($"  GPU Fan Speed:      {thermalInfo.GpuFanRpm} RPM");
                                Console.WriteLine($"  Thermal Mode:       {thermalInfo.ThermalMode}");
                            }
                        }
                        catch
                        {
                            Console.WriteLine($"Thermal status: {response.Data}");
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response.Error}");
                    }
                }
            }
        }

        public static async Task SetFanModeAsync(CliClient client, string mode, int? speed, bool jsonOutput)
        {
            if (mode.ToLower() == "manual" && !speed.HasValue)
            {
                Console.Error.WriteLine("Error: Fan speed (--speed) is required for manual mode");
                return;
            }

            if (speed.HasValue && (speed < 0 || speed > 100))
            {
                Console.Error.WriteLine("Error: Fan speed must be between 0 and 100");
                return;
            }

            var parameters = new
            {
                Mode = mode.ToLower(),
                Speed = speed
            };

            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.SetFanMode,
                Parameters = JsonSerializer.Serialize(parameters)
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
                        if (mode.ToLower() == "manual")
                        {
                            Console.WriteLine($"✓ Fan mode set to manual with {speed}% speed");
                        }
                        else
                        {
                            Console.WriteLine("✓ Fan mode set to automatic");
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine($"✗ Failed to set fan mode: {response.Error}");
                    }
                }
            }
        }

        public static async Task MonitorTemperaturesAsync(CliClient client, int interval, bool jsonOutput)
        {
            Console.WriteLine("Monitoring temperatures... Press Ctrl+C to stop");
            Console.WriteLine();

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var response = await client.SendCommandAsync(new IpcMessage
                    {
                        Command = IpcCommand.GetThermalStatus
                    });

                    if (response != null && response.Success && response.Data != null)
                    {
                        if (jsonOutput)
                        {
                            Console.WriteLine(JsonSerializer.Serialize(response.Data));
                        }
                        else
                        {
                            try
                            {
                                ThermalInfo? thermalInfo = null;
                                if (response.Data is JsonElement element)
                                {
                                    thermalInfo = JsonSerializer.Deserialize<ThermalInfo>(element.GetRawText());
                                }

                                if (thermalInfo != null)
                                {
                                    Console.Write($"\r[{DateTime.Now:HH:mm:ss}] ");
                                    Console.Write($"CPU: {GetColoredTemp(thermalInfo.CpuTemperature)} ");
                                    Console.Write($"GPU: {GetColoredTemp(thermalInfo.GpuTemperature)} ");
                                    Console.Write($"System: {GetColoredTemp(thermalInfo.SystemTemperature)} ");
                                    Console.Write($"Fans: {thermalInfo.CpuFanRpm}/{thermalInfo.GpuFanRpm} RPM");
                                }
                            }
                            catch
                            {
                                Console.WriteLine($"\r[{DateTime.Now:HH:mm:ss}] {response.Data}");
                            }
                        }
                    }

                    await Task.Delay(interval * 1000, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\n\nMonitoring stopped.");
            }
        }

        private static string GetColoredTemp(double temp)
        {
            string tempStr = $"{temp:F1}°C";

            // Note: In a real terminal application, you might want to use ANSI color codes
            // For now, we'll just add indicators
            if (temp >= 90)
                return $"[!]{tempStr}"; // Critical
            else if (temp >= 80)
                return $"[*]{tempStr}"; // Hot
            else if (temp >= 70)
                return $" {tempStr} "; // Warm
            else
                return $" {tempStr} "; // Normal
        }
    }
}