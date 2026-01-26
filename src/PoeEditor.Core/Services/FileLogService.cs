namespace PoeEditor.Core.Services;

/// <summary>
/// File-based logging service.
/// Logs are stored in the application directory under 'logs' folder.
/// Daily log files: patcher_YYYY-MM-DD.log
/// Thread-safe implementation.
/// </summary>
public class FileLogService : ILogService
{
    private readonly string _logDirectory;
    private string _logFilePath;
    private readonly object _lock = new();
    private DateTime _currentLogDate;

    /// <summary>
    /// Singleton instance for global access.
    /// </summary>
    public static FileLogService Instance { get; } = new();

    public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    public FileLogService()
    {
        // Logs in exe directory
        _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(_logDirectory);

        _currentLogDate = DateTime.Now.Date;
        _logFilePath = GetLogFilePathForDate(_currentLogDate);

        // Log startup
        LogInfo($"=== POE Editor Patcher Started ===");
        LogInfo($"Log directory: {_logDirectory}");
    }

    public void Log(LogLevel level, string message)
    {
        if (level < MinimumLevel)
            return;

        var now = DateTime.Now;

        // Check if we need to rotate to a new day's log file
        if (now.Date != _currentLogDate)
        {
            lock (_lock)
            {
                if (now.Date != _currentLogDate)
                {
                    _currentLogDate = now.Date;
                    _logFilePath = GetLogFilePathForDate(_currentLogDate);
                }
            }
        }

        var timestamp = now.ToString("HH:mm:ss.fff");
        var levelStr = level.ToString().ToUpper().PadRight(7);
        var logLine = $"[{timestamp}] [{levelStr}] {message}";

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logFilePath, logLine + Environment.NewLine);
            }
            catch
            {
                // Ignore write errors - don't crash app due to logging
            }
        }

        // Also output to Debug console
        System.Diagnostics.Debug.WriteLine(logLine);
    }

    public void Log(LogLevel level, string message, Exception? ex)
    {
        if (ex != null)
        {
            var exMessage = $"{message}\n  Exception: {ex.GetType().Name}: {ex.Message}";
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                // Only first few lines of stack trace
                var stackLines = ex.StackTrace.Split('\n').Take(5);
                exMessage += $"\n  StackTrace:\n    {string.Join("\n    ", stackLines)}";
            }
            Log(level, exMessage);
        }
        else
        {
            Log(level, message);
        }
    }

    public void LogDebug(string message) => Log(LogLevel.Debug, message);
    public void LogInfo(string message) => Log(LogLevel.Info, message);
    public void LogWarning(string message) => Log(LogLevel.Warning, message);
    public void LogError(string message, Exception? ex = null) => Log(LogLevel.Error, message, ex);

    public string GetLogFilePath() => _logFilePath;

    public async Task<string> GetRecentLogsAsync(int lines = 100)
    {
        if (!File.Exists(_logFilePath))
            return string.Empty;

        var allLines = await File.ReadAllLinesAsync(_logFilePath);
        var recentLines = allLines.TakeLast(lines);
        return string.Join(Environment.NewLine, recentLines);
    }

    private string GetLogFilePathForDate(DateTime date)
    {
        return Path.Combine(_logDirectory, $"patcher_{date:yyyy-MM-dd}.log");
    }

    /// <summary>
    /// Clean up old log files (older than specified days).
    /// </summary>
    public void CleanupOldLogs(int keepDays = 7)
    {
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-keepDays);
            var logFiles = Directory.GetFiles(_logDirectory, "patcher_*.log");

            foreach (var logFile in logFiles)
            {
                var fileInfo = new FileInfo(logFile);
                if (fileInfo.CreationTime < cutoffDate)
                {
                    try
                    {
                        File.Delete(logFile);
                        LogDebug($"Deleted old log file: {Path.GetFileName(logFile)}");
                    }
                    catch
                    {
                        // Ignore deletion errors
                    }
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
