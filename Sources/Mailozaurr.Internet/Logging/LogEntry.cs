namespace Mailozaurr;

/// <summary>
/// Represents a single log entry, including its message and type.
/// Used to collect logs in client classes and emit them in cmdlets.
/// </summary>
/// <remarks>
/// The <see cref="LogCollector"/> class stores collections of these
/// entries until they are written to output.
/// </remarks>
public class LogEntry {
    /// <summary>The log message.</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>The type of log (warning, error, etc.).</summary>
    public LogType Type { get; set; }
}