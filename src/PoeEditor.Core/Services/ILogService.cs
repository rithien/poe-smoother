namespace PoeEditor.Core.Services;

/// <summary>
/// Log levels for the application.
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// Simple logging service interface.
/// </summary>
public interface ILogService
{
    /// <summary>
    /// Log a message with specified level.
    /// </summary>
    void Log(LogLevel level, string message);

    /// <summary>
    /// Log a message with exception details.
    /// </summary>
    void Log(LogLevel level, string message, Exception? ex);

    /// <summary>
    /// Log debug message (detailed operations, disabled in release).
    /// </summary>
    void LogDebug(string message);

    /// <summary>
    /// Log info message (major operations).
    /// </summary>
    void LogInfo(string message);

    /// <summary>
    /// Log warning message (recoverable issues).
    /// </summary>
    void LogWarning(string message);

    /// <summary>
    /// Log error message with optional exception.
    /// </summary>
    void LogError(string message, Exception? ex = null);

    /// <summary>
    /// Get the current log file path.
    /// </summary>
    string GetLogFilePath();

    /// <summary>
    /// Get recent log entries.
    /// </summary>
    /// <param name="lines">Number of lines to retrieve.</param>
    Task<string> GetRecentLogsAsync(int lines = 100);

    /// <summary>
    /// Minimum log level to write. Messages below this level are ignored.
    /// </summary>
    LogLevel MinimumLevel { get; set; }
}
