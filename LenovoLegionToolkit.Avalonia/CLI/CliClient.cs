using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.IPC;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.CLI
{
    public class CliClient
    {
        private const string SOCKET_PATH = "/tmp/legion-toolkit.sock";
        private readonly JsonSerializerOptions _jsonOptions;

        public CliClient()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
        }

        public async Task<IpcResponse?> SendCommandAsync(IpcMessage message)
        {
            try
            {
                using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);

                var endpoint = new UnixDomainSocketEndPoint(SOCKET_PATH);

                try
                {
                    await client.ConnectAsync(endpoint);
                }
                catch (SocketException)
                {
                    Logger.Warning("Cannot connect to daemon. Is it running?");
                    return new IpcResponse
                    {
                        Success = false,
                        Error = "Cannot connect to Legion Toolkit daemon. Please ensure it is running.",
                        Data = null
                    };
                }

                // Send message
                var json = JsonSerializer.Serialize(message, _jsonOptions);
                var data = Encoding.UTF8.GetBytes(json);
                var lengthBytes = BitConverter.GetBytes(data.Length);

                await client.SendAsync(lengthBytes, SocketFlags.None);
                await client.SendAsync(data, SocketFlags.None);

                // Receive response
                var lengthBuffer = new byte[4];
                await client.ReceiveAsync(lengthBuffer, SocketFlags.None);
                var responseLength = BitConverter.ToInt32(lengthBuffer, 0);

                var responseBuffer = new byte[responseLength];
                var totalReceived = 0;
                while (totalReceived < responseLength)
                {
                    var received = await client.ReceiveAsync(
                        responseBuffer.AsMemory(totalReceived, responseLength - totalReceived),
                        SocketFlags.None);
                    totalReceived += received;
                }

                var responseJson = Encoding.UTF8.GetString(responseBuffer);
                var response = JsonSerializer.Deserialize<IpcResponse>(responseJson, _jsonOptions);

                return response;
            }
            catch (Exception ex)
            {
                Logger.Error($"IPC communication error: {ex.Message}", ex);
                return new IpcResponse
                {
                    Success = false,
                    Error = $"Communication error: {ex.Message}",
                    Data = null
                };
            }
        }

        public async Task<T?> SendCommandAsync<T>(IpcCommand command, object? parameters = null)
        {
            var message = new IpcMessage
            {
                Command = command,
                Parameters = parameters != null ? JsonSerializer.Serialize(parameters, _jsonOptions) : null
            };

            var response = await SendCommandAsync(message);

            if (response == null || !response.Success)
            {
                return default;
            }

            if (response.Data == null)
            {
                return default;
            }

            try
            {
                if (response.Data is JsonElement element)
                {
                    return JsonSerializer.Deserialize<T>(element.GetRawText(), _jsonOptions);
                }

                return JsonSerializer.Deserialize<T>(response.Data.ToString()!, _jsonOptions);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to deserialize response: {ex.Message}", ex);
                return default;
            }
        }

        public async Task<bool> CheckDaemonStatusAsync()
        {
            try
            {
                using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                var endpoint = new UnixDomainSocketEndPoint(SOCKET_PATH);

                await client.ConnectAsync(endpoint);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsDaemonRunning()
        {
            try
            {
                using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                var endpoint = new UnixDomainSocketEndPoint(SOCKET_PATH);

                client.Connect(endpoint);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void PrintResponse(IpcResponse response, bool jsonOutput)
        {
            if (jsonOutput)
            {
                var json = JsonSerializer.Serialize(response, _jsonOptions);
                Console.WriteLine(json);
            }
            else
            {
                if (response.Success)
                {
                    if (response.Data != null)
                    {
                        if (response.Data is JsonElement element)
                        {
                            Console.WriteLine(element.ToString());
                        }
                        else
                        {
                            Console.WriteLine(response.Data.ToString());
                        }
                    }
                    else
                    {
                        Console.WriteLine("✓ Command executed successfully");
                    }
                }
                else
                {
                    Console.Error.WriteLine($"✗ Error: {response.Error}");
                }
            }
        }
    }
}