using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.IPC;
using LenovoLegionToolkit.Avalonia.Models;

namespace LenovoLegionToolkit.Avalonia.CLI.Commands
{
    public static class PowerCommand
    {
        public static async Task GetPowerModeAsync(CliClient client, bool jsonOutput)
        {
            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.GetPowerMode
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
                        Console.WriteLine($"Current power mode: {response.Data}");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response.Error}");
                    }
                }
            }
        }

        public static async Task SetPowerModeAsync(CliClient client, string mode, bool jsonOutput)
        {
            PowerMode powerMode;
            switch (mode.ToLower())
            {
                case "quiet":
                case "silent":
                    powerMode = PowerMode.Quiet;
                    break;
                case "balanced":
                case "balance":
                    powerMode = PowerMode.Balanced;
                    break;
                case "performance":
                case "perf":
                    powerMode = PowerMode.Performance;
                    break;
                case "custom":
                    powerMode = PowerMode.Custom;
                    break;
                default:
                    Console.Error.WriteLine($"Invalid power mode: {mode}");
                    Console.Error.WriteLine("Valid modes: quiet, balanced, performance, custom");
                    return;
            }

            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.SetPowerMode,
                Parameters = powerMode.ToString()
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
                        Console.WriteLine($"✓ Power mode set to: {powerMode}");
                    }
                    else
                    {
                        Console.Error.WriteLine($"✗ Failed to set power mode: {response.Error}");
                    }
                }
            }
        }

        public static async Task ListPowerModesAsync(CliClient client, bool jsonOutput)
        {
            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.ListPowerModes
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
                        Console.WriteLine("Available power modes:");
                        Console.WriteLine("  • quiet       - Low power, silent operation");
                        Console.WriteLine("  • balanced    - Balanced performance and efficiency");
                        Console.WriteLine("  • performance - Maximum performance");
                        Console.WriteLine("  • custom      - User-defined settings");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response.Error}");
                    }
                }
            }
        }
    }
}