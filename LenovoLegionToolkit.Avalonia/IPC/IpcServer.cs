using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Settings;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.IPC
{
    public class IpcServer
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ISettingsService _settingsService;
        private readonly IPowerModeService _powerModeService;
        private readonly IBatteryService _batteryService;
        private readonly IThermalService _thermalService;
        private readonly IHardwareService _hardwareService;

        private Socket? _socket;
        private Thread? _listenerThread;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly string _socketPath;
        private bool _isRunning;

        public bool IsRunning => _isRunning;

        public IpcServer(
            IServiceProvider serviceProvider,
            ISettingsService settingsService,
            IPowerModeService powerModeService,
            IBatteryService batteryService,
            IThermalService thermalService,
            IHardwareService hardwareService)
        {
            _serviceProvider = serviceProvider;
            _settingsService = settingsService;
            _powerModeService = powerModeService;
            _batteryService = batteryService;
            _thermalService = thermalService;
            _hardwareService = hardwareService;

            _socketPath = _settingsService.Settings.Advanced.IpcSocketPath;
        }

        public async Task StartAsync()
        {
            if (_isRunning)
            {
                Logger.Warning("IPC server is already running");
                return;
            }

            try
            {
                // Remove existing socket file if it exists
                if (File.Exists(_socketPath))
                {
                    File.Delete(_socketPath);
                }

                // Create Unix domain socket
                _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                _socket.Bind(new UnixDomainSocketEndPoint(_socketPath));
                _socket.Listen(5);

                // Set permissions for the socket
                await SetSocketPermissionsAsync();

                _cancellationTokenSource = new CancellationTokenSource();
                _isRunning = true;

                // Start listener thread
                _listenerThread = new Thread(ListenForClients)
                {
                    IsBackground = true,
                    Name = "IPC Listener"
                };
                _listenerThread.Start();

                Logger.Info($"IPC server started on {_socketPath}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to start IPC server", ex);
                _isRunning = false;
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (!_isRunning)
                return;

            try
            {
                Logger.Info("Stopping IPC server...");

                _isRunning = false;
                _cancellationTokenSource?.Cancel();

                _socket?.Close();
                _socket?.Dispose();

                _listenerThread?.Join(1000);

                // Clean up socket file
                if (File.Exists(_socketPath))
                {
                    File.Delete(_socketPath);
                }

                Logger.Info("IPC server stopped");
            }
            catch (Exception ex)
            {
                Logger.Error("Error stopping IPC server", ex);
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _socket = null;
                _listenerThread = null;
            }

            await Task.CompletedTask;
        }

        private void ListenForClients()
        {
            while (_isRunning && !_cancellationTokenSource?.Token.IsCancellationRequested == true)
            {
                try
                {
                    // Accept client connection
                    var client = _socket?.Accept();
                    if (client != null)
                    {
                        // Handle client in a separate task
                        Task.Run(() => HandleClient(client));
                    }
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Logger.Error("Error accepting client connection", ex);
                    }
                }
            }
        }

        private async void HandleClient(Socket client)
        {
            try
            {
                using (client)
                {
                    // Read message
                    var buffer = new byte[4096];
                    var bytesReceived = client.Receive(buffer);

                    if (bytesReceived > 0)
                    {
                        var json = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                        var message = IpcSerializer.Deserialize<IpcMessage>(json);

                        if (message != null)
                        {
                            Logger.Debug($"IPC command received: {message.Command}");

                            // Process command
                            var response = await ProcessCommandAsync(message);

                            // Send response
                            var responseJson = IpcSerializer.Serialize(response);
                            var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                            client.Send(responseBytes);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error handling IPC client", ex);
            }
        }

        private Dictionary<string, object>? ParseParameters(string? parametersJson)
        {
            if (string.IsNullOrEmpty(parametersJson))
                return null;

            try
            {
                var doc = JsonDocument.Parse(parametersJson);
                var dict = new Dictionary<string, object>();

                foreach (var element in doc.RootElement.EnumerateObject())
                {
                    dict[element.Name] = IpcSerializer.DeserializeParameter(element.Value) ?? element.Value.ToString();
                }

                return dict;
            }
            catch
            {
                return null;
            }
        }

        private async Task<IpcResponse> ProcessCommandAsync(IpcMessage message)
        {
            try
            {
                object? result = null;
                var parameters = ParseParameters(message.Parameters);

                switch (message.Command)
                {
                    case IpcCommand.Ping:
                        result = "pong";
                        break;

                    case IpcCommand.GetStatus:
                        result = new
                        {
                            Running = true,
                            Version = "3.0.0",
                            Platform = "Linux",
                            Uptime = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime
                        };
                        break;

                    case IpcCommand.GetPowerMode:
                        result = await _powerModeService.GetCurrentPowerModeAsync();
                        break;

                    case IpcCommand.SetPowerMode:
                        if (parameters?.TryGetValue("mode", out var modeObj) == true)
                        {
                            if (Enum.TryParse<PowerMode>(modeObj?.ToString(), true, out var mode))
                            {
                                result = await _powerModeService.SetPowerModeAsync(mode);
                            }
                            else
                            {
                                return IpcMessageBuilder.CreateErrorResponse(message.Id, "Invalid power mode");
                            }
                        }
                        else
                        {
                            return IpcMessageBuilder.CreateErrorResponse(message.Id, "Power mode parameter required");
                        }
                        break;

                    case IpcCommand.GetAvailablePowerModes:
                        result = await _powerModeService.GetAvailablePowerModesAsync();
                        break;

                    case IpcCommand.GetBatteryInfo:
                        result = await _batteryService.GetBatteryInfoAsync();
                        break;

                    case IpcCommand.GetBatteryMode:
                        result = await _batteryService.GetBatteryModeAsync();
                        break;

                    case IpcCommand.SetConservationMode:
                        if (parameters?.TryGetValue("enabled", out var enabledObj) == true)
                        {
                            if (bool.TryParse(enabledObj?.ToString(), out var enabled))
                            {
                                result = await _batteryService.SetConservationModeAsync(enabled);
                            }
                            else
                            {
                                return IpcMessageBuilder.CreateErrorResponse(message.Id, "Invalid enabled value");
                            }
                        }
                        else
                        {
                            return IpcMessageBuilder.CreateErrorResponse(message.Id, "Enabled parameter required");
                        }
                        break;

                    case IpcCommand.SetRapidChargeMode:
                        if (parameters?.TryGetValue("enabled", out var rapidObj) == true)
                        {
                            if (bool.TryParse(rapidObj?.ToString(), out var enabled))
                            {
                                result = await _batteryService.SetRapidChargeModeAsync(enabled);
                            }
                            else
                            {
                                return IpcMessageBuilder.CreateErrorResponse(message.Id, "Invalid enabled value");
                            }
                        }
                        else
                        {
                            return IpcMessageBuilder.CreateErrorResponse(message.Id, "Enabled parameter required");
                        }
                        break;

                    case IpcCommand.GetThermalInfo:
                        result = await _thermalService.GetThermalInfoAsync();
                        break;

                    case IpcCommand.GetCpuTemperature:
                        result = await _thermalService.GetCpuTemperatureAsync();
                        break;

                    case IpcCommand.GetGpuTemperature:
                        result = await _thermalService.GetGpuTemperatureAsync();
                        break;

                    case IpcCommand.GetFanSpeed:
                        if (parameters?.TryGetValue("fanId", out var fanIdObj) == true)
                        {
                            if (int.TryParse(fanIdObj?.ToString(), out var fanId))
                            {
                                var fans = await _thermalService.GetFansInfoAsync();
                                result = fans.FirstOrDefault(f => f.Id == fanId);
                            }
                            else
                            {
                                result = await _thermalService.GetFansInfoAsync();
                            }
                        }
                        else
                        {
                            result = await _thermalService.GetFansInfoAsync();
                        }
                        break;

                    case IpcCommand.SetFanSpeed:
                        if (parameters?.TryGetValue("fanId", out var fanIdSetObj) == true &&
                            parameters?.TryGetValue("speed", out var speedObj) == true)
                        {
                            if (int.TryParse(fanIdSetObj?.ToString(), out var fanId) &&
                                int.TryParse(speedObj?.ToString(), out var speed))
                            {
                                result = await _thermalService.SetFanSpeedAsync(fanId, speed);
                            }
                            else
                            {
                                return IpcMessageBuilder.CreateErrorResponse(message.Id, "Invalid fanId or speed value");
                            }
                        }
                        else
                        {
                            return IpcMessageBuilder.CreateErrorResponse(message.Id, "FanId and speed parameters required");
                        }
                        break;

                    case IpcCommand.SetFanMode:
                        if (parameters?.TryGetValue("fanId", out var fanIdModeObj) == true &&
                            parameters?.TryGetValue("mode", out var fanModeObj) == true)
                        {
                            if (int.TryParse(fanIdModeObj?.ToString(), out var fanId) &&
                                Enum.TryParse<FanMode>(fanModeObj?.ToString(), true, out var mode))
                            {
                                result = await _thermalService.SetFanModeAsync(fanId, mode);
                            }
                            else
                            {
                                return IpcMessageBuilder.CreateErrorResponse(message.Id, "Invalid fanId or mode value");
                            }
                        }
                        else
                        {
                            return IpcMessageBuilder.CreateErrorResponse(message.Id, "FanId and mode parameters required");
                        }
                        break;

                    case IpcCommand.GetHardwareInfo:
                        result = await _hardwareService.GetHardwareInfoAsync();
                        break;

                    case IpcCommand.GetCapabilities:
                        result = await _hardwareService.DetectCapabilitiesAsync();
                        break;

                    case IpcCommand.CheckFeatureSupport:
                        if (parameters?.TryGetValue("feature", out var featureObj) == true)
                        {
                            if (Enum.TryParse<HardwareFeature>(featureObj?.ToString(), true, out var feature))
                            {
                                result = await _hardwareService.IsFeatureSupportedAsync(feature);
                            }
                            else
                            {
                                return IpcMessageBuilder.CreateErrorResponse(message.Id, "Invalid feature");
                            }
                        }
                        else
                        {
                            return IpcMessageBuilder.CreateErrorResponse(message.Id, "Feature parameter required");
                        }
                        break;

                    case IpcCommand.GetSettings:
                        result = _settingsService.Settings;
                        break;

                    case IpcCommand.ResetSettings:
                        _settingsService.ResetSettings();
                        result = true;
                        break;

                    case IpcCommand.GetLogs:
                        if (parameters?.TryGetValue("lines", out var linesObj) == true &&
                            int.TryParse(linesObj?.ToString(), out var lines))
                        {
                            result = await Logger.GetRecentLogsAsync(lines);
                        }
                        else
                        {
                            result = await Logger.GetRecentLogsAsync();
                        }
                        break;

                    default:
                        return IpcMessageBuilder.CreateErrorResponse(message.Id, $"Unknown command: {message.Command}");
                }

                return IpcMessageBuilder.CreateSuccessResponse(message.Id, result);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing IPC command {message.Command}", ex);
                return IpcMessageBuilder.CreateErrorResponse(message.Id, $"Error: {ex.Message}");
            }
        }

        private async Task SetSocketPermissionsAsync()
        {
            try
            {
                // Set permissions to allow access for the user's group
                var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"660 {_socketPath}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process != null)
                {
                    await process.WaitForExitAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to set socket permissions: {ex.Message}");
            }
        }
    }
}