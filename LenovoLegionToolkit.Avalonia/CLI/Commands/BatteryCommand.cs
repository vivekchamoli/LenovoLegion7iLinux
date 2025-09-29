using System;
using System.Text.Json;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.IPC;
using LenovoLegionToolkit.Avalonia.Models;

namespace LenovoLegionToolkit.Avalonia.CLI.Commands
{
    public static class BatteryCommand
    {
        public static async Task GetBatteryStatusAsync(CliClient client, bool jsonOutput)
        {
            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.GetBatteryStatus
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
                            BatteryInfo? batteryInfo = null;
                            if (response.Data is JsonElement element)
                            {
                                batteryInfo = JsonSerializer.Deserialize<BatteryInfo>(element.GetRawText());
                            }

                            if (batteryInfo != null)
                            {
                                Console.WriteLine("Battery Status:");
                                Console.WriteLine($"  Charge Level:    {batteryInfo.ChargeLevel}%");
                                Console.WriteLine($"  Status:          {(batteryInfo.IsCharging ? "Charging" : batteryInfo.IsDischarging ? "Discharging" : "AC Power")}");
                                Console.WriteLine($"  Health:          {(batteryInfo.FullChargeCapacity / batteryInfo.DesignCapacity * 100):F1}%");
                                Console.WriteLine($"  Cycle Count:     {batteryInfo.CycleCount}");
                                Console.WriteLine($"  Voltage:         {batteryInfo.Voltage:F2}V");

                                if (batteryInfo.EstimatedTimeRemaining?.TotalMinutes > 0)
                                {
                                    Console.WriteLine($"  Time Remaining:  {batteryInfo.EstimatedTimeRemaining.Value:hh\\:mm}");
                                }
                            }
                        }
                        catch
                        {
                            Console.WriteLine($"Battery status: {response.Data}");
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response.Error}");
                    }
                }
            }
        }

        public static async Task SetRapidChargeAsync(CliClient client, bool enable, bool jsonOutput)
        {
            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.SetRapidCharge,
                Parameters = enable.ToString()
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
                        Console.WriteLine($"✓ Rapid charge {(enable ? "enabled" : "disabled")}");
                    }
                    else
                    {
                        Console.Error.WriteLine($"✗ Failed to {(enable ? "enable" : "disable")} rapid charge: {response.Error}");
                    }
                }
            }
        }

        public static async Task SetConservationModeAsync(CliClient client, bool enable, int? threshold, bool jsonOutput)
        {
            var parameters = new
            {
                Enable = enable,
                Threshold = threshold ?? 80
            };

            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.SetConservationMode,
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
                        if (enable)
                        {
                            Console.WriteLine($"✓ Conservation mode enabled with {threshold ?? 80}% threshold");
                        }
                        else
                        {
                            Console.WriteLine("✓ Conservation mode disabled");
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine($"✗ Failed to configure conservation mode: {response.Error}");
                    }
                }
            }
        }
    }
}