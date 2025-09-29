using System;
using System.Text.Json;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.IPC;

namespace LenovoLegionToolkit.Avalonia.CLI.Commands
{
    public static class StatusCommand
    {
        public static async Task GetSystemStatusAsync(CliClient client, bool verbose, bool jsonOutput)
        {
            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.GetSystemStatus,
                Parameters = verbose.ToString()
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
                            SystemStatus? status = null;
                            if (response.Data is JsonElement element)
                            {
                                status = JsonSerializer.Deserialize<SystemStatus>(element.GetRawText());
                            }

                            if (status != null)
                            {
                                Console.WriteLine("Legion Toolkit System Status");
                                Console.WriteLine("============================");
                                Console.WriteLine();

                                // Hardware Info
                                Console.WriteLine("Hardware:");
                                Console.WriteLine($"  Model:         {status.Model}");
                                Console.WriteLine($"  BIOS Version:  {status.BiosVersion}");
                                Console.WriteLine($"  Legion Module: {(status.KernelModuleLoaded ? "Loaded" : "Not Loaded")}");
                                Console.WriteLine();

                                // Power Status
                                Console.WriteLine("Power:");
                                Console.WriteLine($"  Mode:          {status.PowerMode}");
                                Console.WriteLine($"  Battery:       {status.BatteryLevel}%");
                                Console.WriteLine($"  AC Connected:  {(status.IsAcConnected ? "Yes" : "No")}");
                                Console.WriteLine($"  Rapid Charge:  {(status.RapidChargeEnabled ? "Enabled" : "Disabled")}");
                                Console.WriteLine($"  Conservation:  {(status.ConservationModeEnabled ? $"Enabled ({status.ChargingThreshold}%)" : "Disabled")}");
                                Console.WriteLine();

                                // Thermal Status
                                Console.WriteLine("Thermal:");
                                Console.WriteLine($"  CPU Temp:      {status.CpuTemperature:F1}°C");
                                Console.WriteLine($"  GPU Temp:      {status.GpuTemperature:F1}°C");
                                Console.WriteLine($"  CPU Fan:       {status.CpuFanSpeed} RPM");
                                Console.WriteLine($"  GPU Fan:       {status.GpuFanSpeed} RPM");
                                Console.WriteLine();

                                // Features
                                Console.WriteLine("Features:");
                                Console.WriteLine($"  RGB Keyboard:  {(status.RgbKeyboardSupported ? "Supported" : "Not Available")}");
                                Console.WriteLine($"  Display Ctrl:  {(status.DisplayControlSupported ? "Supported" : "Not Available")}");
                                Console.WriteLine($"  Automation:    {(status.AutomationEnabled ? "Enabled" : "Disabled")}");

                                if (verbose)
                                {
                                    Console.WriteLine();
                                    Console.WriteLine("Additional Details:");
                                    Console.WriteLine($"  Uptime:        {status.Uptime}");
                                    Console.WriteLine($"  Config File:   {status.ConfigPath}");
                                    Console.WriteLine($"  Log File:      {status.LogPath}");
                                }
                            }
                        }
                        catch
                        {
                            Console.WriteLine($"System status: {response.Data}");
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response.Error}");
                    }
                }
            }
        }

        private class SystemStatus
        {
            public string Model { get; set; } = "Unknown";
            public string BiosVersion { get; set; } = "Unknown";
            public bool KernelModuleLoaded { get; set; }
            public string PowerMode { get; set; } = "Unknown";
            public int BatteryLevel { get; set; }
            public bool IsAcConnected { get; set; }
            public bool RapidChargeEnabled { get; set; }
            public bool ConservationModeEnabled { get; set; }
            public int ChargingThreshold { get; set; }
            public double CpuTemperature { get; set; }
            public double GpuTemperature { get; set; }
            public int CpuFanSpeed { get; set; }
            public int GpuFanSpeed { get; set; }
            public bool RgbKeyboardSupported { get; set; }
            public bool DisplayControlSupported { get; set; }
            public bool AutomationEnabled { get; set; }
            public string Uptime { get; set; } = "Unknown";
            public string ConfigPath { get; set; } = "Unknown";
            public string LogPath { get; set; } = "Unknown";
        }
    }
}