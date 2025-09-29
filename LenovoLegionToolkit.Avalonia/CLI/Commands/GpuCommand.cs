using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.IPC;
using LenovoLegionToolkit.Avalonia.Models;

namespace LenovoLegionToolkit.Avalonia.CLI.Commands
{
    public static class GpuCommand
    {
        public static async Task GetGpuStatusAsync(CliClient client, bool jsonOutput)
        {
            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.GetGpuStatus
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
                        Console.WriteLine("GPU Status:");
                        Console.WriteLine("-----------");
                        Console.WriteLine(response.Data);
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response.Error}");
                    }
                }
            }
        }

        public static async Task GetHybridModeAsync(CliClient client, bool jsonOutput)
        {
            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.GetHybridMode
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
                        Console.WriteLine($"Current hybrid mode: {response.Data}");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response.Error}");
                    }
                }
            }
        }

        public static async Task SetHybridModeAsync(CliClient client, string mode, bool jsonOutput)
        {
            HybridModeState hybridMode;
            switch (mode.ToLower())
            {
                case "on":
                case "hybrid":
                    hybridMode = HybridModeState.On;
                    break;
                case "off":
                case "discrete":
                    hybridMode = HybridModeState.Off;
                    break;
                case "igpu-only":
                case "integrated":
                    hybridMode = HybridModeState.OnIGPUOnly;
                    break;
                case "auto":
                case "automatic":
                    hybridMode = HybridModeState.OnAuto;
                    break;
                default:
                    Console.Error.WriteLine($"Invalid mode: {mode}. Valid modes are: on, off, igpu-only, auto");
                    return;
            }

            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.SetHybridMode,
                Parameters = IpcSerializer.Serialize(hybridMode)
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
                        Console.WriteLine($"Hybrid mode set to: {hybridMode}");
                        Console.WriteLine("Note: System restart may be required for changes to take effect.");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response.Error}");
                    }
                }
            }
        }

        public static async Task SetDiscreteGpuPowerAsync(CliClient client, string state, bool jsonOutput)
        {
            bool powerOn = state.ToLower() switch
            {
                "on" or "enable" => true,
                "off" or "disable" => false,
                _ => throw new ArgumentException($"Invalid state: {state}. Use 'on' or 'off'")
            };

            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.SetDiscreteGpuPower,
                Parameters = IpcSerializer.Serialize(powerOn)
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
                        Console.WriteLine($"Discrete GPU {(powerOn ? "powered on" : "powered off")}");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response.Error}");
                    }
                }
            }
        }

        public static async Task GetGpuInfoAsync(CliClient client, bool jsonOutput)
        {
            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.GetGpuInfo
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
                        Console.WriteLine("GPU Information:");
                        Console.WriteLine("----------------");
                        Console.WriteLine(response.Data);
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