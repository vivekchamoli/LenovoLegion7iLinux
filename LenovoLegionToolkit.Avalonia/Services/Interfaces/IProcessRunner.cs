using System.Diagnostics;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Avalonia.Services.Interfaces
{
    public interface IProcessRunner
    {
        Task<ProcessResult> RunAsync(string fileName, string arguments, int timeoutMs = 5000);
        Task<ProcessResult> RunAsync(ProcessStartInfo startInfo, int timeoutMs = 5000);
        ProcessResult Run(string fileName, string arguments, int timeoutMs = 5000);
        ProcessResult Run(ProcessStartInfo startInfo, int timeoutMs = 5000);
    }

    public class ProcessResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public bool Success => ExitCode == 0;
        public bool TimedOut { get; set; }
    }
}