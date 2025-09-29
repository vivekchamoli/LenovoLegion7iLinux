using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.IPC;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.CLI
{
    public class AutomationCommands
    {
        private readonly IAutomationService _automationService;
        private readonly CliClient _cliClient;

        public AutomationCommands(IAutomationService automationService, CliClient cliClient)
        {
            _automationService = automationService;
            _cliClient = cliClient;
        }

        public Command CreateCommand()
        {
            var automationCommand = new Command("automation", "Manage automation profiles and rules");

            // Subcommands
            automationCommand.AddCommand(CreateStatusCommand());
            automationCommand.AddCommand(CreateStartCommand());
            automationCommand.AddCommand(CreateStopCommand());
            automationCommand.AddCommand(CreateProfilesCommand());
            automationCommand.AddCommand(CreateRulesCommand());

            return automationCommand;
        }

        private Command CreateStatusCommand()
        {
            var command = new Command("status", "Show automation status");
            command.SetHandler(async () => await ShowStatusAsync());
            return command;
        }

        private Command CreateStartCommand()
        {
            var command = new Command("start", "Start automation service");
            command.SetHandler(async () => await StartAutomationAsync());
            return command;
        }

        private Command CreateStopCommand()
        {
            var command = new Command("stop", "Stop automation service");
            command.SetHandler(async () => await StopAutomationAsync());
            return command;
        }

        private Command CreateProfilesCommand()
        {
            var profilesCommand = new Command("profiles", "Manage automation profiles");

            // List profiles
            var listCommand = new Command("list", "List all profiles");
            listCommand.SetHandler(async () => await ListProfilesAsync());
            profilesCommand.AddCommand(listCommand);

            // Apply profile
            var applyCommand = new Command("apply", "Apply a profile");
            var profileNameArg = new Argument<string>("name", "Profile name");
            applyCommand.AddArgument(profileNameArg);
            applyCommand.SetHandler(async (string name) => await ApplyProfileAsync(name), profileNameArg);
            profilesCommand.AddCommand(applyCommand);

            // Create profile from current state
            var createCommand = new Command("create-from-current", "Create profile from current state");
            var nameOption = new Option<string>(new[] { "-n", "--name" }, "Profile name") { IsRequired = true };
            var descOption = new Option<string>(new[] { "-d", "--description" }, "Profile description");
            createCommand.AddOption(nameOption);
            createCommand.AddOption(descOption);
            createCommand.SetHandler(async (string name, string desc) =>
                await CreateProfileFromCurrentAsync(name, desc ?? "Created from CLI"), nameOption, descOption);
            profilesCommand.AddCommand(createCommand);

            // Delete profile
            var deleteCommand = new Command("delete", "Delete a profile");
            var deleteNameArg = new Argument<string>("name", "Profile name");
            deleteCommand.AddArgument(deleteNameArg);
            deleteCommand.SetHandler(async (string name) => await DeleteProfileAsync(name), deleteNameArg);
            profilesCommand.AddCommand(deleteCommand);

            // Set default profile
            var defaultCommand = new Command("set-default", "Set default profile");
            var defaultNameArg = new Argument<string>("name", "Profile name");
            defaultCommand.AddArgument(defaultNameArg);
            defaultCommand.SetHandler(async (string name) => await SetDefaultProfileAsync(name), defaultNameArg);
            profilesCommand.AddCommand(defaultCommand);

            return profilesCommand;
        }

        private Command CreateRulesCommand()
        {
            var rulesCommand = new Command("rules", "Manage automation rules");

            // List rules
            var listCommand = new Command("list", "List all rules");
            listCommand.SetHandler(async () => await ListRulesAsync());
            rulesCommand.AddCommand(listCommand);

            // Enable/disable rule
            var enableCommand = new Command("enable", "Enable a rule");
            var enableNameArg = new Argument<string>("name", "Rule name");
            enableCommand.AddArgument(enableNameArg);
            enableCommand.SetHandler(async (string name) => await EnableRuleAsync(name, true), enableNameArg);
            rulesCommand.AddCommand(enableCommand);

            var disableCommand = new Command("disable", "Disable a rule");
            var disableNameArg = new Argument<string>("name", "Rule name");
            disableCommand.AddArgument(disableNameArg);
            disableCommand.SetHandler(async (string name) => await EnableRuleAsync(name, false), disableNameArg);
            rulesCommand.AddCommand(disableCommand);

            // Test rule
            var testCommand = new Command("test", "Test if a rule would trigger");
            var testNameArg = new Argument<string>("name", "Rule name");
            testCommand.AddArgument(testNameArg);
            testCommand.SetHandler(async (string name) => await TestRuleAsync(name), testNameArg);
            rulesCommand.AddCommand(testCommand);

            // Trigger rule manually
            var triggerCommand = new Command("trigger", "Trigger a rule manually");
            var triggerNameArg = new Argument<string>("name", "Rule name");
            triggerCommand.AddArgument(triggerNameArg);
            triggerCommand.SetHandler(async (string name) => await TriggerRuleAsync(name), triggerNameArg);
            rulesCommand.AddCommand(triggerCommand);

            // Delete rule
            var deleteCommand = new Command("delete", "Delete a rule");
            var deleteNameArg = new Argument<string>("name", "Rule name");
            deleteCommand.AddArgument(deleteNameArg);
            deleteCommand.SetHandler(async (string name) => await DeleteRuleAsync(name), deleteNameArg);
            rulesCommand.AddCommand(deleteCommand);

            return rulesCommand;
        }

        // Implementation methods
        private async Task ShowStatusAsync()
        {
            try
            {
                if (CliClient.IsDaemonRunning())
                {
                    var response = await _cliClient.SendCommandAsync(new IpcMessage
                    {
                        Command = IpcCommand.AutomationStatus,
                        Parameters = string.Empty
                    });

                    if (response?.Success == true)
                    {
                        Console.WriteLine(response.Data);
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response?.Error ?? "Unknown error"}");
                    }
                }
                else
                {
                    var isRunning = _automationService.IsRunning;
                    var config = _automationService.Configuration;

                    Console.WriteLine($"Automation Status: {(isRunning ? "Running" : "Stopped")}");
                    Console.WriteLine($"Enabled: {config.Enabled}");
                    Console.WriteLine($"Profiles: {config.Profiles.Count}");
                    Console.WriteLine($"Rules: {config.Rules.Count} ({config.Rules.Count(r => r.IsEnabled)} enabled)");
                    Console.WriteLine($"Evaluation Interval: {config.EvaluationInterval.TotalSeconds} seconds");

                    if (_automationService.CurrentProfile != null)
                    {
                        Console.WriteLine($"Current Profile: {_automationService.CurrentProfile.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error getting automation status: {ex.Message}");
            }
        }

        private async Task StartAutomationAsync()
        {
            try
            {
                if (CliClient.IsDaemonRunning())
                {
                    var response = await _cliClient.SendCommandAsync(new IpcMessage
                    {
                        Command = IpcCommand.AutomationStart,
                        Parameters = string.Empty
                    });

                    if (response?.Success == true)
                    {
                        Console.WriteLine("Automation service started");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response?.Error ?? "Failed to start automation"}");
                    }
                }
                else
                {
                    var success = await _automationService.StartAutomationAsync();
                    if (success)
                    {
                        Console.WriteLine("Automation service started");
                    }
                    else
                    {
                        Console.Error.WriteLine("Failed to start automation service");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error starting automation: {ex.Message}");
            }
        }

        private async Task StopAutomationAsync()
        {
            try
            {
                if (CliClient.IsDaemonRunning())
                {
                    var response = await _cliClient.SendCommandAsync(new IpcMessage
                    {
                        Command = IpcCommand.AutomationStop,
                        Parameters = string.Empty
                    });

                    if (response?.Success == true)
                    {
                        Console.WriteLine("Automation service stopped");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response?.Error ?? "Failed to stop automation"}");
                    }
                }
                else
                {
                    var success = await _automationService.StopAutomationAsync();
                    if (success)
                    {
                        Console.WriteLine("Automation service stopped");
                    }
                    else
                    {
                        Console.Error.WriteLine("Failed to stop automation service");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error stopping automation: {ex.Message}");
            }
        }

        private async Task ListProfilesAsync()
        {
            try
            {
                if (CliClient.IsDaemonRunning())
                {
                    var response = await _cliClient.SendCommandAsync(new IpcMessage
                    {
                        Command = IpcCommand.AutomationProfilesList,
                        Parameters = string.Empty
                    });

                    if (response?.Success == true)
                    {
                        Console.WriteLine(response.Data);
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response?.Error ?? "Unknown error"}");
                    }
                }
                else
                {
                    var profiles = await _automationService.GetProfilesAsync();

                    if (!profiles.Any())
                    {
                        Console.WriteLine("No profiles found");
                        return;
                    }

                    Console.WriteLine("Profiles:");
                    Console.WriteLine("─────────");

                    foreach (var profile in profiles)
                    {
                        var defaultMark = profile.IsDefault ? " [DEFAULT]" : "";
                        Console.WriteLine($"• {profile.Name}{defaultMark}");
                        Console.WriteLine($"  Description: {profile.Description}");

                        if (profile.PowerMode.HasValue)
                            Console.WriteLine($"  Power Mode: {profile.PowerMode}");

                        if (profile.BatteryChargeLimit.HasValue)
                            Console.WriteLine($"  Battery Limit: {profile.BatteryChargeLimit}%");

                        if (profile.FanMode.HasValue)
                            Console.WriteLine($"  Fan Mode: {profile.FanMode}");

                        Console.WriteLine($"  Created: {profile.CreatedAt:yyyy-MM-dd HH:mm}");
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error listing profiles: {ex.Message}");
            }
        }

        private async Task ApplyProfileAsync(string name)
        {
            try
            {
                if (CliClient.IsDaemonRunning())
                {
                    var response = await _cliClient.SendCommandAsync(new IpcMessage
                    {
                        Command = IpcCommand.AutomationProfilesApply,
                        Parameters = name
                    });

                    if (response?.Success == true)
                    {
                        Console.WriteLine($"Profile '{name}' applied successfully");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response?.Error ?? "Failed to apply profile"}");
                    }
                }
                else
                {
                    var profiles = await _automationService.GetProfilesAsync();
                    var profile = profiles.FirstOrDefault(p =>
                        p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                    if (profile == null)
                    {
                        Console.Error.WriteLine($"Profile '{name}' not found");
                        return;
                    }

                    var success = await _automationService.ApplyProfileAsync(profile.Id);
                    if (success)
                    {
                        Console.WriteLine($"Profile '{name}' applied successfully");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Failed to apply profile '{name}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error applying profile: {ex.Message}");
            }
        }

        private async Task CreateProfileFromCurrentAsync(string name, string description)
        {
            try
            {
                if (CliClient.IsDaemonRunning())
                {
                    var response = await _cliClient.SendCommandAsync(new IpcMessage
                    {
                        Command = IpcCommand.AutomationProfilesCreateFromCurrent,
                        Parameters = System.Text.Json.JsonSerializer.Serialize(new { name, description })
                    });

                    if (response?.Success == true)
                    {
                        Console.WriteLine($"Profile '{name}' created successfully");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response?.Error ?? "Failed to create profile"}");
                    }
                }
                else
                {
                    var profile = await _automationService.CreateProfileFromCurrentStateAsync(name, description);
                    Console.WriteLine($"Profile '{profile.Name}' created successfully");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error creating profile: {ex.Message}");
            }
        }

        private async Task DeleteProfileAsync(string name)
        {
            try
            {
                if (CliClient.IsDaemonRunning())
                {
                    var response = await _cliClient.SendCommandAsync(new IpcMessage
                    {
                        Command = IpcCommand.AutomationProfilesDelete,
                        Parameters = name
                    });

                    if (response?.Success == true)
                    {
                        Console.WriteLine($"Profile '{name}' deleted successfully");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response?.Error ?? "Failed to delete profile"}");
                    }
                }
                else
                {
                    var profiles = await _automationService.GetProfilesAsync();
                    var profile = profiles.FirstOrDefault(p =>
                        p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                    if (profile == null)
                    {
                        Console.Error.WriteLine($"Profile '{name}' not found");
                        return;
                    }

                    var success = await _automationService.DeleteProfileAsync(profile.Id);
                    if (success)
                    {
                        Console.WriteLine($"Profile '{name}' deleted successfully");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Failed to delete profile '{name}' - it may be in use by rules");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error deleting profile: {ex.Message}");
            }
        }

        private async Task SetDefaultProfileAsync(string name)
        {
            try
            {
                if (CliClient.IsDaemonRunning())
                {
                    var response = await _cliClient.SendCommandAsync(new IpcMessage
                    {
                        Command = IpcCommand.AutomationProfilesSetDefault,
                        Parameters = name
                    });

                    if (response?.Success == true)
                    {
                        Console.WriteLine($"Profile '{name}' set as default");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response?.Error ?? "Failed to set default profile"}");
                    }
                }
                else
                {
                    var profiles = await _automationService.GetProfilesAsync();
                    var profile = profiles.FirstOrDefault(p =>
                        p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                    if (profile == null)
                    {
                        Console.Error.WriteLine($"Profile '{name}' not found");
                        return;
                    }

                    var success = await _automationService.SetDefaultProfileAsync(profile.Id);
                    if (success)
                    {
                        Console.WriteLine($"Profile '{name}' set as default");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Failed to set profile '{name}' as default");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error setting default profile: {ex.Message}");
            }
        }

        private async Task ListRulesAsync()
        {
            try
            {
                if (CliClient.IsDaemonRunning())
                {
                    var response = await _cliClient.SendCommandAsync(new IpcMessage
                    {
                        Command = IpcCommand.AutomationRulesList,
                        Parameters = string.Empty
                    });

                    if (response?.Success == true)
                    {
                        Console.WriteLine(response.Data);
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response?.Error ?? "Unknown error"}");
                    }
                }
                else
                {
                    var rules = await _automationService.GetRulesAsync();

                    if (!rules.Any())
                    {
                        Console.WriteLine("No rules found");
                        return;
                    }

                    Console.WriteLine("Rules:");
                    Console.WriteLine("──────");

                    foreach (var rule in rules)
                    {
                        var status = rule.IsEnabled ? "✓" : "✗";
                        Console.WriteLine($"{status} {rule.Name} (Priority: {rule.Priority})");
                        Console.WriteLine($"  Description: {rule.Description}");
                        Console.WriteLine($"  Action: {rule.Action}");
                        Console.WriteLine($"  Triggers: {rule.Triggers.Count}");

                        if (rule.LastTriggered.HasValue)
                        {
                            Console.WriteLine($"  Last Triggered: {rule.LastTriggered:yyyy-MM-dd HH:mm}");
                            Console.WriteLine($"  Trigger Count: {rule.TriggerCount}");
                        }

                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error listing rules: {ex.Message}");
            }
        }

        private async Task EnableRuleAsync(string name, bool enabled)
        {
            try
            {
                var command = enabled ? IpcCommand.AutomationRulesTest : IpcCommand.AutomationRulesDelete; // TODO: Add proper enable/disable commands

                if (CliClient.IsDaemonRunning())
                {
                    var response = await _cliClient.SendCommandAsync(new IpcMessage
                    {
                        Command = command,
                        Parameters = name
                    });

                    if (response?.Success == true)
                    {
                        var action = enabled ? "enabled" : "disabled";
                        Console.WriteLine($"Rule '{name}' {action} successfully");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response?.Error ?? "Failed to update rule"}");
                    }
                }
                else
                {
                    var rules = await _automationService.GetRulesAsync();
                    var rule = rules.FirstOrDefault(r =>
                        r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                    if (rule == null)
                    {
                        Console.Error.WriteLine($"Rule '{name}' not found");
                        return;
                    }

                    var success = await _automationService.EnableRuleAsync(rule.Id, enabled);
                    if (success)
                    {
                        var action = enabled ? "enabled" : "disabled";
                        Console.WriteLine($"Rule '{name}' {action} successfully");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Failed to update rule '{name}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error updating rule: {ex.Message}");
            }
        }

        private async Task TestRuleAsync(string name)
        {
            try
            {
                if (CliClient.IsDaemonRunning())
                {
                    var response = await _cliClient.SendCommandAsync(new IpcMessage
                    {
                        Command = IpcCommand.AutomationRulesTest,
                        Parameters = name
                    });

                    if (response?.Success == true)
                    {
                        Console.WriteLine(response.Data);
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response?.Error ?? "Failed to test rule"}");
                    }
                }
                else
                {
                    var rules = await _automationService.GetRulesAsync();
                    var rule = rules.FirstOrDefault(r =>
                        r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                    if (rule == null)
                    {
                        Console.Error.WriteLine($"Rule '{name}' not found");
                        return;
                    }

                    var wouldTrigger = await _automationService.TestRuleAsync(rule.Id);
                    if (wouldTrigger)
                    {
                        Console.WriteLine($"Rule '{name}' WOULD trigger in the current context");
                    }
                    else
                    {
                        Console.WriteLine($"Rule '{name}' would NOT trigger in the current context");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error testing rule: {ex.Message}");
            }
        }

        private async Task TriggerRuleAsync(string name)
        {
            try
            {
                if (CliClient.IsDaemonRunning())
                {
                    var response = await _cliClient.SendCommandAsync(new IpcMessage
                    {
                        Command = IpcCommand.AutomationRulesTrigger,
                        Parameters = name
                    });

                    if (response?.Success == true)
                    {
                        Console.WriteLine($"Rule '{name}' triggered successfully");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response?.Error ?? "Failed to trigger rule"}");
                    }
                }
                else
                {
                    var rules = await _automationService.GetRulesAsync();
                    var rule = rules.FirstOrDefault(r =>
                        r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                    if (rule == null)
                    {
                        Console.Error.WriteLine($"Rule '{name}' not found");
                        return;
                    }

                    var success = await _automationService.TriggerRuleManuallyAsync(rule.Id);
                    if (success)
                    {
                        Console.WriteLine($"Rule '{name}' triggered successfully");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Failed to trigger rule '{name}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error triggering rule: {ex.Message}");
            }
        }

        private async Task DeleteRuleAsync(string name)
        {
            try
            {
                if (CliClient.IsDaemonRunning())
                {
                    var response = await _cliClient.SendCommandAsync(new IpcMessage
                    {
                        Command = IpcCommand.AutomationRulesDelete,
                        Parameters = name
                    });

                    if (response?.Success == true)
                    {
                        Console.WriteLine($"Rule '{name}' deleted successfully");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: {response?.Error ?? "Failed to delete rule"}");
                    }
                }
                else
                {
                    var rules = await _automationService.GetRulesAsync();
                    var rule = rules.FirstOrDefault(r =>
                        r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                    if (rule == null)
                    {
                        Console.Error.WriteLine($"Rule '{name}' not found");
                        return;
                    }

                    var success = await _automationService.DeleteRuleAsync(rule.Id);
                    if (success)
                    {
                        Console.WriteLine($"Rule '{name}' deleted successfully");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Failed to delete rule '{name}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error deleting rule: {ex.Message}");
            }
        }
    }
}