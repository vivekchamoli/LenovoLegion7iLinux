using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.CLI.Commands;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.CLI
{
    public class CommandLineInterface
    {
        private readonly RootCommand _rootCommand;
        private readonly CliClient _cliClient;

        public CommandLineInterface()
        {
            _cliClient = new CliClient();

            _rootCommand = new RootCommand("Legion Toolkit CLI - Control your Legion laptop from the command line");

            // Add global options
            var verboseOption = new Option<bool>(
                new[] { "--verbose", "-v" },
                "Enable verbose output");
            _rootCommand.AddGlobalOption(verboseOption);

            var jsonOption = new Option<bool>(
                new[] { "--json", "-j" },
                "Output in JSON format");
            _rootCommand.AddGlobalOption(jsonOption);

            // Add commands
            AddPowerCommand();
            AddBatteryCommand();
            AddGpuCommand();
            AddThermalCommand();
            AddRgbCommand();
            AddProfileCommand();
            AddAutomationCommand();
            AddStatusCommand();
            AddDaemonCommand();
        }

        public async Task<int> RunAsync(string[] args)
        {
            try
            {
                return await _rootCommand.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                Logger.Error($"CLI error: {ex.Message}", ex);
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private void AddPowerCommand()
        {
            var powerCommand = new Command("power", "Manage power modes");

            // Subcommand: get
            var getCommand = new Command("get", "Get current power mode");
            getCommand.SetHandler(async (InvocationContext context) =>
            {
                var json = context.ParseResult.GetValueForOption(
                    context.ParseResult.RootCommandResult.Command.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
                await PowerCommand.GetPowerModeAsync(_cliClient, json);
            });
            powerCommand.AddCommand(getCommand);

            // Subcommand: set
            var setCommand = new Command("set", "Set power mode");
            var modeArgument = new Argument<string>("mode", "Power mode (quiet, balanced, performance, custom)");
            setCommand.AddArgument(modeArgument);
            setCommand.SetHandler(async (string mode, bool json) =>
            {
                await PowerCommand.SetPowerModeAsync(_cliClient, mode, json);
            }, modeArgument, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            powerCommand.AddCommand(setCommand);

            // Subcommand: list
            var listCommand = new Command("list", "List available power modes");
            listCommand.SetHandler(async (bool json) =>
            {
                await PowerCommand.ListPowerModesAsync(_cliClient, json);
            }, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            powerCommand.AddCommand(listCommand);

            _rootCommand.AddCommand(powerCommand);
        }

        private void AddBatteryCommand()
        {
            var batteryCommand = new Command("battery", "Manage battery settings");

            // Subcommand: status
            var statusCommand = new Command("status", "Get battery status");
            statusCommand.SetHandler(async (bool json) =>
            {
                await BatteryCommand.GetBatteryStatusAsync(_cliClient, json);
            }, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            batteryCommand.AddCommand(statusCommand);

            // Subcommand: rapid-charge
            var rapidChargeCommand = new Command("rapid-charge", "Manage rapid charge");
            var rapidEnableArgument = new Argument<string>("action", "enable or disable");
            rapidChargeCommand.AddArgument(rapidEnableArgument);
            rapidChargeCommand.SetHandler(async (string action, bool json) =>
            {
                await BatteryCommand.SetRapidChargeAsync(_cliClient, action == "enable", json);
            }, rapidEnableArgument, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            batteryCommand.AddCommand(rapidChargeCommand);

            // Subcommand: conservation
            var conservationCommand = new Command("conservation", "Manage conservation mode");
            var conserveEnableArgument = new Argument<string>("action", "enable or disable");
            conservationCommand.AddArgument(conserveEnableArgument);
            var thresholdOption = new Option<int?>("--threshold", "Charging threshold (60-100)");
            conservationCommand.AddOption(thresholdOption);
            conservationCommand.SetHandler(async (string action, int? threshold, bool json) =>
            {
                await BatteryCommand.SetConservationModeAsync(_cliClient, action == "enable", threshold, json);
            }, conserveEnableArgument, thresholdOption, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            batteryCommand.AddCommand(conservationCommand);

            _rootCommand.AddCommand(batteryCommand);
        }

        private void AddGpuCommand()
        {
            var gpuCommand = new Command("gpu", "Manage GPU settings and hybrid mode");

            // Subcommand: status
            var statusCommand = new Command("status", "Get GPU status");
            statusCommand.SetHandler(async (bool json) =>
            {
                await GpuCommand.GetGpuStatusAsync(_cliClient, json);
            }, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            gpuCommand.AddCommand(statusCommand);

            // Subcommand: info
            var infoCommand = new Command("info", "Get detailed GPU information");
            infoCommand.SetHandler(async (bool json) =>
            {
                await GpuCommand.GetGpuInfoAsync(_cliClient, json);
            }, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            gpuCommand.AddCommand(infoCommand);

            // Subcommand: hybrid-mode
            var hybridCommand = new Command("hybrid-mode", "Get or set hybrid graphics mode");
            var modeArgument = new Argument<string?>("mode", () => null, "Mode: on, off, igpu-only, auto (omit to get current mode)");
            hybridCommand.AddArgument(modeArgument);
            hybridCommand.SetHandler(async (string? mode, bool json) =>
            {
                if (string.IsNullOrEmpty(mode))
                {
                    await GpuCommand.GetHybridModeAsync(_cliClient, json);
                }
                else
                {
                    await GpuCommand.SetHybridModeAsync(_cliClient, mode, json);
                }
            }, modeArgument, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            gpuCommand.AddCommand(hybridCommand);

            // Subcommand: discrete
            var discreteCommand = new Command("discrete", "Control discrete GPU power");
            var stateArgument = new Argument<string>("state", "State: on or off");
            discreteCommand.AddArgument(stateArgument);
            discreteCommand.SetHandler(async (string state, bool json) =>
            {
                await GpuCommand.SetDiscreteGpuPowerAsync(_cliClient, state, json);
            }, stateArgument, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            gpuCommand.AddCommand(discreteCommand);

            _rootCommand.AddCommand(gpuCommand);
        }

        private void AddThermalCommand()
        {
            var thermalCommand = new Command("thermal", "Monitor and control thermals");

            // Subcommand: status
            var statusCommand = new Command("status", "Get thermal status");
            statusCommand.SetHandler(async (bool json) =>
            {
                await ThermalCommand.GetThermalStatusAsync(_cliClient, json);
            }, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            thermalCommand.AddCommand(statusCommand);

            // Subcommand: fan
            var fanCommand = new Command("fan", "Control fan speeds");
            var fanModeArgument = new Argument<string>("mode", "auto or manual");
            fanCommand.AddArgument(fanModeArgument);
            var speedOption = new Option<int?>("--speed", "Fan speed percentage (0-100) for manual mode");
            fanCommand.AddOption(speedOption);
            fanCommand.SetHandler(async (string mode, int? speed, bool json) =>
            {
                await ThermalCommand.SetFanModeAsync(_cliClient, mode, speed, json);
            }, fanModeArgument, speedOption, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            thermalCommand.AddCommand(fanCommand);

            // Subcommand: monitor
            var monitorCommand = new Command("monitor", "Monitor temperatures in real-time");
            var intervalOption = new Option<int>("--interval", () => 2, "Update interval in seconds");
            monitorCommand.AddOption(intervalOption);
            monitorCommand.SetHandler(async (int interval, bool json) =>
            {
                await ThermalCommand.MonitorTemperaturesAsync(_cliClient, interval, json);
            }, intervalOption, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            thermalCommand.AddCommand(monitorCommand);

            _rootCommand.AddCommand(thermalCommand);
        }

        private void AddRgbCommand()
        {
            var rgbCommand = new Command("rgb", "Control RGB keyboard lighting");

            // Subcommand: set
            var setCommand = new Command("set", "Set RGB color or effect");
            var colorArgument = new Argument<string>("color", "Color in hex format (e.g., FF0000) or effect name");
            setCommand.AddArgument(colorArgument);
            var brightnessOption = new Option<int?>("--brightness", "Brightness level (0-100)");
            setCommand.AddOption(brightnessOption);
            var zoneOption = new Option<string?>("--zone", "Specific zone (all, left, right, center, wasd)");
            setCommand.AddOption(zoneOption);
            setCommand.SetHandler(async (string color, int? brightness, string? zone, bool json) =>
            {
                await RgbCommand.SetRgbAsync(_cliClient, color, brightness, zone, json);
            }, colorArgument, brightnessOption, zoneOption, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            rgbCommand.AddCommand(setCommand);

            // Subcommand: effect
            var effectCommand = new Command("effect", "Set RGB effect");
            var effectNameArgument = new Argument<string>("name", "Effect name (static, breathing, wave, rainbow)");
            effectCommand.AddArgument(effectNameArgument);
            var speedOption = new Option<int?>("--speed", "Effect speed (1-10)");
            effectCommand.AddOption(speedOption);
            effectCommand.SetHandler(async (string name, int? speed, bool json) =>
            {
                await RgbCommand.SetEffectAsync(_cliClient, name, speed, json);
            }, effectNameArgument, speedOption, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            rgbCommand.AddCommand(effectCommand);

            // Subcommand: off
            var offCommand = new Command("off", "Turn off RGB lighting");
            offCommand.SetHandler(async (bool json) =>
            {
                await RgbCommand.TurnOffAsync(_cliClient, json);
            }, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            rgbCommand.AddCommand(offCommand);

            _rootCommand.AddCommand(rgbCommand);
        }

        private void AddProfileCommand()
        {
            var profileCommand = new Command("profile", "Manage automation profiles");

            // Subcommand: list
            var listCommand = new Command("list", "List all profiles");
            listCommand.SetHandler(async (bool json) =>
            {
                await ProfileCommand.ListProfilesAsync(_cliClient, json);
            }, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            profileCommand.AddCommand(listCommand);

            // Subcommand: apply
            var applyCommand = new Command("apply", "Apply a profile");
            var profileNameArgument = new Argument<string>("name", "Profile name");
            applyCommand.AddArgument(profileNameArgument);
            applyCommand.SetHandler(async (string name, bool json) =>
            {
                await ProfileCommand.ApplyProfileAsync(_cliClient, name, json);
            }, profileNameArgument, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            profileCommand.AddCommand(applyCommand);

            // Subcommand: create
            var createCommand = new Command("create", "Create a new profile from current settings");
            var newProfileNameArgument = new Argument<string>("name", "Profile name");
            createCommand.AddArgument(newProfileNameArgument);
            createCommand.SetHandler(async (string name, bool json) =>
            {
                await ProfileCommand.CreateProfileAsync(_cliClient, name, json);
            }, newProfileNameArgument, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            profileCommand.AddCommand(createCommand);

            _rootCommand.AddCommand(profileCommand);
        }

        private void AddStatusCommand()
        {
            var statusCommand = new Command("status", "Get overall system status");
            statusCommand.SetHandler(async (bool verbose, bool json) =>
            {
                await StatusCommand.GetSystemStatusAsync(_cliClient, verbose, json);
            }, _rootCommand.Options.FirstOrDefault(o => o.Name == "verbose") as Option<bool>,
               _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            _rootCommand.AddCommand(statusCommand);
        }

        private void AddDaemonCommand()
        {
            var daemonCommand = new Command("daemon", "Control the Legion Toolkit daemon");

            // Subcommand: start
            var startCommand = new Command("start", "Start the daemon");
            startCommand.SetHandler(async (bool json) =>
            {
                await DaemonCommand.StartDaemonAsync(json);
            }, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            daemonCommand.AddCommand(startCommand);

            // Subcommand: stop
            var stopCommand = new Command("stop", "Stop the daemon");
            stopCommand.SetHandler(async (bool json) =>
            {
                await DaemonCommand.StopDaemonAsync(_cliClient, json);
            }, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            daemonCommand.AddCommand(stopCommand);

            // Subcommand: restart
            var restartCommand = new Command("restart", "Restart the daemon");
            restartCommand.SetHandler(async (bool json) =>
            {
                await DaemonCommand.RestartDaemonAsync(_cliClient, json);
            }, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            daemonCommand.AddCommand(restartCommand);

            // Subcommand: status
            var statusCommand = new Command("status", "Check daemon status");
            statusCommand.SetHandler(async (bool json) =>
            {
                await DaemonCommand.GetDaemonStatusAsync(_cliClient, json);
            }, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            daemonCommand.AddCommand(statusCommand);

            _rootCommand.AddCommand(daemonCommand);
        }

        private void AddAutomationCommand()
        {
            var automationCommand = new Command("automation", "Manage automation profiles and rules");

            // Note: For full automation functionality, use the GUI or run with --daemon mode
            // The basic CLI commands here work with the daemon when it's running

            // Subcommand: status
            var statusCommand = new Command("status", "Show automation status");
            statusCommand.SetHandler(async (bool json) =>
            {
                await AutomationCommand.GetStatusAsync(_cliClient, json);
            }, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            automationCommand.AddCommand(statusCommand);

            // Subcommand: start
            var startCommand = new Command("start", "Start automation service");
            startCommand.SetHandler(async (bool json) =>
            {
                await AutomationCommand.StartAsync(_cliClient, json);
            }, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            automationCommand.AddCommand(startCommand);

            // Subcommand: stop
            var stopCommand = new Command("stop", "Stop automation service");
            stopCommand.SetHandler(async (bool json) =>
            {
                await AutomationCommand.StopAsync(_cliClient, json);
            }, _rootCommand.Options.FirstOrDefault(o => o.Name == "json") as Option<bool>);
            automationCommand.AddCommand(stopCommand);

            _rootCommand.AddCommand(automationCommand);
        }
    }
}