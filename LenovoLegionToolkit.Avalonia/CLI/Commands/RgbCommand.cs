using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.IPC;

namespace LenovoLegionToolkit.Avalonia.CLI.Commands
{
    public static class RgbCommand
    {
        public static async Task SetRgbAsync(CliClient client, string color, int? brightness, string? zone, bool jsonOutput)
        {
            // Parse color - could be hex or effect name
            var isHexColor = IsHexColor(color);

            var parameters = new
            {
                Color = color,
                Brightness = brightness ?? 100,
                Zone = zone ?? "all",
                IsEffect = !isHexColor
            };

            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.SetRgbColor,
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
                        if (isHexColor)
                        {
                            Console.WriteLine($"✓ RGB color set to #{color} in zone {zone ?? "all"}");
                        }
                        else
                        {
                            Console.WriteLine($"✓ RGB effect '{color}' applied to zone {zone ?? "all"}");
                        }

                        if (brightness.HasValue)
                        {
                            Console.WriteLine($"  Brightness: {brightness}%");
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine($"✗ Failed to set RGB: {response.Error}");
                    }
                }
            }
        }

        public static async Task SetEffectAsync(CliClient client, string effectName, int? speed, bool jsonOutput)
        {
            var validEffects = new[] { "static", "breathing", "wave", "rainbow", "ripple", "shift" };

            if (!Array.Exists(validEffects, e => e.Equals(effectName, StringComparison.OrdinalIgnoreCase)))
            {
                Console.Error.WriteLine($"Invalid effect: {effectName}");
                Console.Error.WriteLine($"Valid effects: {string.Join(", ", validEffects)}");
                return;
            }

            var parameters = new
            {
                Effect = effectName.ToLower(),
                Speed = speed ?? 5
            };

            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.SetRgbEffect,
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
                        Console.WriteLine($"✓ RGB effect set to '{effectName}'");
                        if (speed.HasValue)
                        {
                            Console.WriteLine($"  Speed: {speed} (1-10 scale)");
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine($"✗ Failed to set RGB effect: {response.Error}");
                    }
                }
            }
        }

        public static async Task TurnOffAsync(CliClient client, bool jsonOutput)
        {
            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.RgbOff
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
                        Console.WriteLine("✓ RGB lighting turned off");
                    }
                    else
                    {
                        Console.Error.WriteLine($"✗ Failed to turn off RGB: {response.Error}");
                    }
                }
            }
        }

        private static bool IsHexColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color))
                return false;

            // Remove # if present
            color = color.TrimStart('#');

            // Check if it's a valid 6-character hex string
            return color.Length == 6 &&
                   int.TryParse(color, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
        }
    }
}