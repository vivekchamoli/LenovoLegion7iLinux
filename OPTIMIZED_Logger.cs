using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Avalonia.Utils
{
    /// <summary>
    /// OPTIMIZED Logger with async file writing, structured logging, and performance monitoring
    /// Fixes: File I/O blocking, log rotation, structured logging, performance metrics
    /// </summary>
    public static class Logger
    {
        private static readonly ConcurrentQueue<LogEntry> _logQueue = new();
        private static readonly Timer _flushTimer;
        private static readonly SemaphoreSlim _flushSemaphore = new(1, 1);
        private static readonly string _logDirectory;
        private static readonly string _logFilePath;

        private static LogLevel _currentLogLevel = LogLevel.Info;
        private static bool _fileLoggingEnabled = true;
        private static bool _consoleLoggingEnabled = true;
        private static bool _isInitialized = false;
        private static long _logFileSize = 0;
        private static int _logFileRotationNumber = 0;

        // Performance metrics
        private static readonly ConcurrentDictionary<string, PerformanceMetric> _performanceMetrics = new();

        private const long MAX_LOG_FILE_SIZE = 10 * 1024 * 1024; // 10MB
        private const int MAX_LOG_FILES = 5;
        private const int FLUSH_INTERVAL_MS = 1000; // Flush every 1 second
        private const int MAX_QUEUE_SIZE = 10000; // Prevent memory issues

        static Logger()
        {
            // Setup log directory
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(configHome))
            {
                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                configHome = Path.Combine(homeDir, ".config");
            }

            _logDirectory = Path.Combine(configHome, "legion-toolkit", "logs");
            _logFilePath = Path.Combine(_logDirectory, "legion-toolkit.log");

            // Ensure log directory exists
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
            }
            catch
            {
                // Fallback to temp directory
                _logDirectory = Path.GetTempPath();
                _logFilePath = Path.Combine(_logDirectory, "legion-toolkit.log");
            }

            // Start flush timer
            _flushTimer = new Timer(FlushCallback, null, FLUSH_INTERVAL_MS, FLUSH_INTERVAL_MS);

            // Handle application exit
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        public static void Initialize(LogLevel logLevel = LogLevel.Info, bool enableFileLogging = true)
        {
            _currentLogLevel = logLevel;
            _fileLoggingEnabled = enableFileLogging;
            _isInitialized = true;

            Info($"Logger initialized - Level: {logLevel}, File logging: {enableFileLogging}");
            Info($"Log directory: {_logDirectory}");
        }

        public static void SetLogLevel(LogLevel level)
        {
            _currentLogLevel = level;
            Info($"Log level changed to: {level}");
        }

        public static void EnableFileLogging(bool enabled)
        {
            _fileLoggingEnabled = enabled;
            Info($"File logging {(enabled ? "enabled" : "disabled")}");
        }

        public static void EnableConsoleLogging(bool enabled)
        {
            _consoleLoggingEnabled = enabled;
        }

        public static void Critical(string message, Exception? exception = null, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            Log(LogLevel.Critical, message, exception, memberName, filePath, lineNumber);
        }

        public static void Error(string message, Exception? exception = null, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            Log(LogLevel.Error, message, exception, memberName, filePath, lineNumber);
        }

        public static void Warning(string message, Exception? exception = null, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            Log(LogLevel.Warning, message, exception, memberName, filePath, lineNumber);
        }

        public static void Info(string message, Exception? exception = null, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            Log(LogLevel.Info, message, exception, memberName, filePath, lineNumber);
        }

        public static void Debug(string message, Exception? exception = null, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            Log(LogLevel.Debug, message, exception, memberName, filePath, lineNumber);
        }

        public static void Trace(string message, Exception? exception = null, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            Log(LogLevel.Trace, message, exception, memberName, filePath, lineNumber);
        }

        // Performance monitoring methods
        public static IDisposable BeginScope(string operationName)
        {
            return new PerformanceScope(operationName);
        }

        public static void LogPerformance(string operationName, TimeSpan duration, bool isSuccess = true)
        {
            _performanceMetrics.AddOrUpdate(operationName,
                new PerformanceMetric(operationName, duration, isSuccess),
                (key, existing) => existing.Update(duration, isSuccess));

            if (duration.TotalMilliseconds > 1000) // Log slow operations
            {
                Warning($"Slow operation detected: {operationName} took {duration.TotalMilliseconds:F1}ms");
            }
        }

        public static void LogMemoryUsage(string context = "")
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var workingSet = process.WorkingSet64 / (1024 * 1024); // MB
                var privateMemory = process.PrivateMemorySize64 / (1024 * 1024); // MB

                Info($"Memory usage{(string.IsNullOrEmpty(context) ? "" : $" ({context})")}: Working Set: {workingSet}MB, Private: {privateMemory}MB");
            }
            catch (Exception ex)
            {
                Debug($"Failed to log memory usage: {ex.Message}");
            }
        }

        public static PerformanceReport GetPerformanceReport()
        {
            var metrics = new Dictionary<string, PerformanceMetric>();
            foreach (var kvp in _performanceMetrics)
            {
                metrics[kvp.Key] = kvp.Value.Clone();
            }

            return new PerformanceReport(metrics);
        }

        private static void Log(LogLevel level, string message, Exception? exception, string memberName, string filePath, int lineNumber)
        {
            if (!_isInitialized || level < _currentLogLevel)
                return;

            try
            {
                var entry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    Message = message,
                    Exception = exception,
                    MemberName = memberName,
                    FileName = Path.GetFileName(filePath),
                    LineNumber = lineNumber,
                    ThreadId = Thread.CurrentThread.ManagedThreadId
                };

                // Console logging (immediate)
                if (_consoleLoggingEnabled)
                {
                    WriteToConsole(entry);
                }

                // Queue for file logging (async)
                if (_fileLoggingEnabled)
                {
                    // Prevent memory issues with large queue
                    if (_logQueue.Count < MAX_QUEUE_SIZE)
                    {
                        _logQueue.Enqueue(entry);
                    }
                    else
                    {
                        // Drop oldest entries if queue is full
                        if (_logQueue.TryDequeue(out _))
                        {
                            _logQueue.Enqueue(entry);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback logging to console if logging system fails
                try
                {
                    Console.WriteLine($"[LOGGER ERROR] {DateTime.Now:HH:mm:ss.fff} Failed to log message: {ex.Message}");
                    Console.WriteLine($"[FALLBACK] {DateTime.Now:HH:mm:ss.fff} [{level}] {message}");
                }
                catch
                {
                    // If even console fails, we can't do much
                }
            }
        }

        private static void WriteToConsole(LogEntry entry)
        {
            try
            {
                var color = GetConsoleColor(entry.Level);
                var originalColor = Console.ForegroundColor;

                Console.ForegroundColor = color;
                Console.WriteLine(FormatLogEntry(entry, false));

                if (entry.Exception != null)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"    Exception: {entry.Exception}");
                }

                Console.ForegroundColor = originalColor;
            }
            catch
            {
                // Fallback to plain console output
                Console.WriteLine(FormatLogEntry(entry, false));
            }
        }

        private static ConsoleColor GetConsoleColor(LogLevel level)
        {
            return level switch
            {
                LogLevel.Critical => ConsoleColor.Magenta,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Info => ConsoleColor.White,
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Trace => ConsoleColor.DarkGray,
                _ => ConsoleColor.White
            };
        }

        private static string FormatLogEntry(LogEntry entry, bool includeExtendedInfo = true)
        {
            var sb = new StringBuilder();

            // Basic format: [Timestamp] [Level] [Thread] Message
            sb.Append('[');
            sb.Append(entry.Timestamp.ToString("HH:mm:ss.fff"));
            sb.Append("] [");
            sb.Append(entry.Level.ToString().ToUpper().PadRight(8));
            sb.Append("] [");
            sb.Append(entry.ThreadId.ToString().PadLeft(2));
            sb.Append("] ");
            sb.Append(entry.Message);

            if (includeExtendedInfo)
            {
                // Add source information for file logging
                sb.Append(" (");
                sb.Append(entry.FileName);
                sb.Append(':');
                sb.Append(entry.MemberName);
                sb.Append(':');
                sb.Append(entry.LineNumber);
                sb.Append(')');
            }

            return sb.ToString();
        }

        private static async void FlushCallback(object? state)
        {
            try
            {
                await FlushLogsAsync();
            }
            catch (Exception ex)
            {
                // Avoid recursive logging errors
                try
                {
                    Console.WriteLine($"[LOGGER ERROR] {DateTime.Now:HH:mm:ss.fff} Failed to flush logs: {ex.Message}");
                }
                catch
                {
                    // If console also fails, we can't do anything
                }
            }
        }

        private static async Task FlushLogsAsync()
        {
            if (!_fileLoggingEnabled || _logQueue.IsEmpty)
                return;

            if (!await _flushSemaphore.WaitAsync(100))
                return;

            try
            {
                var entriesToWrite = new List<LogEntry>();

                // Dequeue all pending entries
                while (_logQueue.TryDequeue(out var entry) && entriesToWrite.Count < 1000)
                {
                    entriesToWrite.Add(entry);
                }

                if (entriesToWrite.Count == 0)
                    return;

                // Check if log rotation is needed
                await CheckLogRotationAsync();

                // Write entries to file
                using var writer = new StreamWriter(_logFilePath, append: true, Encoding.UTF8);
                foreach (var entry in entriesToWrite)
                {
                    var logLine = FormatLogEntry(entry, true);
                    await writer.WriteLineAsync(logLine);

                    if (entry.Exception != null)
                    {
                        await writer.WriteLineAsync($"    Exception: {entry.Exception}");
                    }

                    _logFileSize += Encoding.UTF8.GetByteCount(logLine) + Environment.NewLine.Length;
                }

                await writer.FlushAsync();
            }
            finally
            {
                _flushSemaphore.Release();
            }
        }

        private static async Task CheckLogRotationAsync()
        {
            try
            {
                if (!File.Exists(_logFilePath))
                {
                    _logFileSize = 0;
                    return;
                }

                var fileInfo = new FileInfo(_logFilePath);
                _logFileSize = fileInfo.Length;

                if (_logFileSize > MAX_LOG_FILE_SIZE)
                {
                    await RotateLogFileAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGGER ERROR] Failed to check log rotation: {ex.Message}");
            }
        }

        private static async Task RotateLogFileAsync()
        {
            try
            {
                // Move current log file to backup
                var backupPath = Path.Combine(_logDirectory, $"legion-toolkit.{++_logFileRotationNumber}.log");
                File.Move(_logFilePath, backupPath);

                // Clean up old log files
                await CleanupOldLogFilesAsync();

                _logFileSize = 0;

                // Log rotation event to new file
                var rotationEntry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = LogLevel.Info,
                    Message = $"Log file rotated. Previous log saved as: {Path.GetFileName(backupPath)}",
                    ThreadId = Thread.CurrentThread.ManagedThreadId,
                    FileName = "Logger.cs",
                    MemberName = "RotateLogFileAsync",
                    LineNumber = 0
                };

                _logQueue.Enqueue(rotationEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGGER ERROR] Failed to rotate log file: {ex.Message}");
            }
        }

        private static async Task CleanupOldLogFilesAsync()
        {
            try
            {
                var logFiles = Directory.GetFiles(_logDirectory, "legion-toolkit.*.log");
                if (logFiles.Length <= MAX_LOG_FILES)
                    return;

                Array.Sort(logFiles, (x, y) => File.GetCreationTime(x).CompareTo(File.GetCreationTime(y)));

                var filesToDelete = logFiles.Length - MAX_LOG_FILES;
                for (int i = 0; i < filesToDelete; i++)
                {
                    File.Delete(logFiles[i]);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGGER ERROR] Failed to cleanup old log files: {ex.Message}");
            }
        }

        private static void OnProcessExit(object? sender, EventArgs e)
        {
            try
            {
                // Flush remaining logs before exit
                FlushLogsAsync().Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Don't throw during process exit
            }
        }

        private class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public LogLevel Level { get; set; }
            public string Message { get; set; } = string.Empty;
            public Exception? Exception { get; set; }
            public string MemberName { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public int LineNumber { get; set; }
            public int ThreadId { get; set; }
        }

        private class PerformanceScope : IDisposable
        {
            private readonly string _operationName;
            private readonly Stopwatch _stopwatch;
            private bool _disposed;

            public PerformanceScope(string operationName)
            {
                _operationName = operationName;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                if (_disposed) return;

                _stopwatch.Stop();
                LogPerformance(_operationName, _stopwatch.Elapsed);
                _disposed = true;
            }
        }

        private class PerformanceMetric
        {
            private readonly object _lock = new object();

            public string OperationName { get; }
            public int TotalCalls { get; private set; }
            public int SuccessfulCalls { get; private set; }
            public TimeSpan TotalDuration { get; private set; }
            public TimeSpan MinDuration { get; private set; } = TimeSpan.MaxValue;
            public TimeSpan MaxDuration { get; private set; }
            public DateTime LastCall { get; private set; }

            public double AverageDurationMs => TotalCalls > 0 ? TotalDuration.TotalMilliseconds / TotalCalls : 0;
            public double SuccessRate => TotalCalls > 0 ? (double)SuccessfulCalls / TotalCalls * 100 : 0;

            public PerformanceMetric(string operationName, TimeSpan duration, bool isSuccess)
            {
                OperationName = operationName;
                Update(duration, isSuccess);
            }

            public PerformanceMetric Update(TimeSpan duration, bool isSuccess)
            {
                lock (_lock)
                {
                    TotalCalls++;
                    if (isSuccess) SuccessfulCalls++;
                    TotalDuration = TotalDuration.Add(duration);
                    LastCall = DateTime.Now;

                    if (duration < MinDuration) MinDuration = duration;
                    if (duration > MaxDuration) MaxDuration = duration;
                }

                return this;
            }

            public PerformanceMetric Clone()
            {
                lock (_lock)
                {
                    return new PerformanceMetric(OperationName, TimeSpan.Zero, true)
                    {
                        TotalCalls = TotalCalls,
                        SuccessfulCalls = SuccessfulCalls,
                        TotalDuration = TotalDuration,
                        MinDuration = MinDuration,
                        MaxDuration = MaxDuration,
                        LastCall = LastCall
                    };
                }
            }
        }

        public class PerformanceReport
        {
            public Dictionary<string, PerformanceMetric> Metrics { get; }
            public DateTime GeneratedAt { get; }

            public PerformanceReport(Dictionary<string, PerformanceMetric> metrics)
            {
                Metrics = metrics;
                GeneratedAt = DateTime.Now;
            }
        }
    }

    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4,
        Critical = 5
    }
}