using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.IPC;

namespace LenovoLegionToolkit.Avalonia.CLI.Commands
{
    public static class ProfileCommand
    {
        public static async Task ListProfilesAsync(CliClient client, bool jsonOutput)
        {
            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.ListProfiles
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
                            List<ProfileInfo>? profiles = null;
                            if (response.Data is JsonElement element)
                            {
                                profiles = JsonSerializer.Deserialize<List<ProfileInfo>>(element.GetRawText());
                            }

                            if (profiles != null && profiles.Count > 0)
                            {
                                Console.WriteLine("Available Profiles:");
                                Console.WriteLine();

                                foreach (var profile in profiles)
                                {
                                    Console.WriteLine($"  {profile.Name}");
                                    Console.WriteLine($"    Description: {profile.Description}");
                                    Console.WriteLine($"    Power Mode:  {profile.PowerMode}");
                                    Console.WriteLine($"    Created:     {profile.CreatedAt:yyyy-MM-dd HH:mm}");
                                    if (profile.IsActive)
                                    {
                                        Console.WriteLine("    ** Currently Active **");
                                    }
                                    Console.WriteLine();
                                }
                            }
                            else
                            {
                                Console.WriteLine("No profiles found.");
                                Console.WriteLine("Create a profile with: legion-toolkit profile create <name>");
                            }
                        }
                        catch
                        {
                            Console.WriteLine($"Profiles: {response.Data}");
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response.Error}");
                    }
                }
            }
        }

        public static async Task ApplyProfileAsync(CliClient client, string profileName, bool jsonOutput)
        {
            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.ApplyProfile,
                Parameters = profileName
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
                        Console.WriteLine($"✓ Profile '{profileName}' applied successfully");
                    }
                    else
                    {
                        Console.Error.WriteLine($"✗ Failed to apply profile: {response.Error}");
                    }
                }
            }
        }

        public static async Task CreateProfileAsync(CliClient client, string profileName, bool jsonOutput)
        {
            // First check if profile already exists
            var listResponse = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.ListProfiles
            });

            if (listResponse != null && listResponse.Success && listResponse.Data != null)
            {
                try
                {
                    List<ProfileInfo>? profiles = null;
                    if (listResponse.Data is JsonElement element)
                    {
                        profiles = JsonSerializer.Deserialize<List<ProfileInfo>>(element.GetRawText());
                    }

                    if (profiles != null && profiles.Exists(p => p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        Console.Error.WriteLine($"Profile '{profileName}' already exists");
                        return;
                    }
                }
                catch { }
            }

            var response = await client.SendCommandAsync(new IpcMessage
            {
                Command = IpcCommand.CreateProfile,
                Parameters = profileName
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
                        Console.WriteLine($"✓ Profile '{profileName}' created successfully");
                        Console.WriteLine("  Current system settings have been saved to this profile");
                    }
                    else
                    {
                        Console.Error.WriteLine($"✗ Failed to create profile: {response.Error}");
                    }
                }
            }
        }

        private class ProfileInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string PowerMode { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public bool IsActive { get; set; }
        }
    }
}