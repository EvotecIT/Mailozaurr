namespace Mailozaurr.Logging;

/// <summary>
/// Represents the type of a log entry for use in cmdlet and client logging.
/// </summary>
public enum LogType {
    /// <summary>A warning message.</summary>
    Warning,
    /// <summary>An error message.</summary>
    Error,
    /// <summary>A verbose message.</summary>
    Verbose,
    /// <summary>An informational message.</summary>
    Information
}

/// <summary>
/// Represents a single log entry, including its message and type.
/// Used to collect logs in client classes and emit them in cmdlets.
/// </summary>
public class LogEntry {
    /// <summary>The log message.</summary>
    public string Message { get; set; }
    /// <summary>The type of log (warning, error, etc.).</summary>
    public LogType Type { get; set; }
}