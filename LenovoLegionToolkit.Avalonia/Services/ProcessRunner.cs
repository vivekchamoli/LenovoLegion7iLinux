using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Utils;

namespace LenovoLegionToolkit.Avalonia.Services
{
    public class ProcessRunner : IProcessRunner
    {
        public async Task<ProcessResult> RunAsync(string fileName, string arguments, int timeoutMs = 5000)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            return await RunAsync(startInfo, timeoutMs);
        }

        public async Task<ProcessResult> RunAsync(ProcessStartInfo startInfo, int timeoutMs = 5000)
        {
            return await Task.Run(() => Run(startInfo, timeoutMs));
        }

        public ProcessResult Run(string fileName, string arguments, int timeoutMs = 5000)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            return Run(startInfo, timeoutMs);
        }

        public ProcessResult Run(ProcessStartInfo startInfo, int timeoutMs = 5000)
        {
            var result = new ProcessResult();
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            try
            {
                using var process = new Process();
                process.StartInfo = startInfo;

                if (startInfo.RedirectStandardOutput)
                {
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            outputBuilder.AppendLine(e.Data);
                    };
                }

                if (startInfo.RedirectStandardError)
                {
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            errorBuilder.AppendLine(e.Data);
                    };
                }

                process.Start();

                if (startInfo.RedirectStandardOutput)
                    process.BeginOutputReadLine();

                if (startInfo.RedirectStandardError)
                    process.BeginErrorReadLine();

                if (process.WaitForExit(timeoutMs))
                {
                    // Ensure all output is read
                    process.WaitForExit();
                    result.ExitCode = process.ExitCode;
                }
                else
                {
                    result.TimedOut = true;
                    result.ExitCode = -1;

                    try
                    {
                        process.Kill();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Failed to kill timed out process: {ex.Message}");
                    }
                }

                result.Output = outputBuilder.ToString().TrimEnd();
                result.Error = errorBuilder.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                Logger.Error($"Process execution failed: {ex.Message}", ex);
                result.ExitCode = -1;
                result.Error = ex.Message;
            }

            return result;
        }
    }
}