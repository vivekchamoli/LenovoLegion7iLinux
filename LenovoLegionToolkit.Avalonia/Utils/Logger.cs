using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Avalonia.Utils
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    public static class Logger
    {
        private static readonly object _lockObject = new();
        private static readonly string _logDirectory;
        private static readonly string _logFile;
        private static LogLevel _minLevel = LogLevel.Info;
        private static bool _consoleOutput = true;
        private static bool _initialized;

        static Logger()
        {
            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config",
                "LegionToolkit"
            );

            _logDirectory = Path.Combine(configDir, "logs");
            Directory.CreateDirectory(_logDirectory);

            var timestamp = DateTime.Now.ToString("yyyyMMdd");
            _logFile = Path.Combine(_logDirectory, $"legion-toolkit-{timestamp}.log");

            CleanOldLogs();
        }

        public static void Initialize(LogLevel minLevel = LogLevel.Info, bool enableConsole = true)
        {
            _minLevel = minLevel;
            _consoleOutput = enableConsole;
            _initialized = true;

            Log(LogLevel.Info, "Logger initialized", "Logger");
            Log(LogLevel.Info, $"Log file: {_logFile}", "Logger");
        }

        public static void Debug(string message, [CallerMemberName] string caller = "")
        {
            Log(LogLevel.Debug, message, caller);
        }

        public static void Info(string message, [CallerMemberName] string caller = "")
        {
            Log(LogLevel.Info, message, caller);
        }

        public static void Warning(string message, [CallerMemberName] string caller = "")
        {
            Log(LogLevel.Warning, message, caller);
        }

        public static void Error(string message, Exception? ex = null, [CallerMemberName] string caller = "")
        {
            var fullMessage = ex != null
                ? $"{message}\nException: {ex.GetType().Name}: {ex.Message}\nStackTrace: {ex.StackTrace}"
                : message;
            Log(LogLevel.Error, fullMessage, caller);
        }

        public static void Critical(string message, Exception? ex = null, [CallerMemberName] string caller = "")
        {
            var fullMessage = ex != null
                ? $"{message}\nException: {ex.GetType().Name}: {ex.Message}\nStackTrace: {ex.StackTrace}"
                : message;
            Log(LogLevel.Critical, fullMessage, caller);
        }

        private static void Log(LogLevel level, string message, string caller)
        {
            if (!_initialized || level < _minLevel)
                return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var threadId = Thread.CurrentThread.ManagedThreadId;
            var levelStr = level.ToString().PadRight(8);
            var formattedMessage = $"[{timestamp}] [{levelStr}] [{threadId,3}] [{caller}] {message}";

            lock (_lockObject)
            {
                if (_consoleOutput)
                {
                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = GetConsoleColor(level);
                    Console.WriteLine(formattedMessage);
                    Console.ForegroundColor = originalColor;
                }

                try
                {
                    File.AppendAllText(_logFile, formattedMessage + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    // Ignore file write errors
                }
            }
        }

        private static ConsoleColor GetConsoleColor(LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Info => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Critical => ConsoleColor.DarkRed,
                _ => ConsoleColor.White
            };
        }

        private static void CleanOldLogs()
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-7);
                var logFiles = Directory.GetFiles(_logDirectory, "legion-toolkit-*.log");

                foreach (var file in logFiles)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        public static async Task<string> GetRecentLogsAsync(int lines = 100)
        {
            try
            {
                if (!File.Exists(_logFile))
                    return "No log file found.";

                var allLines = await File.ReadAllLinesAsync(_logFile);
                var startIndex = Math.Max(0, allLines.Length - lines);
                var recentLines = new StringBuilder();

                for (int i = startIndex; i < allLines.Length; i++)
                {
                    recentLines.AppendLine(allLines[i]);
                }

                return recentLines.ToString();
            }
            catch (Exception ex)
            {
                return $"Error reading logs: {ex.Message}";
            }
        }
    }
}