using System.Collections.Concurrent;

namespace Mailozaurr;

/// <summary>
/// Collects log entries in a thread-safe queue for later emission by PowerShell cmdlets.
/// Intended for use in client classes (e.g., SendGridClient) to decouple logging from cmdlet pipeline output.
/// </summary>
/// <remarks>
/// Items remain in the queue until consumed by a cmdlet that flushes
/// the log collector's contents to the appropriate stream.
/// </remarks>
public class LogCollector {
    /// <summary>
    /// The thread-safe queue of log entries.
    /// </summary>
    public ConcurrentQueue<LogEntry> Logs { get; } = new();

    /// <summary>Adds a warning log entry.</summary>
    public void LogWarning(string message) => Logs.Enqueue(new LogEntry { Message = message, Type = LogType.Warning });
    /// <summary>Adds an error log entry.</summary>
    public void LogError(string message) => Logs.Enqueue(new LogEntry { Message = message, Type = LogType.Error });
    /// <summary>Adds a verbose log entry.</summary>
    public void LogVerbose(string message) => Logs.Enqueue(new LogEntry { Message = message, Type = LogType.Verbose });
    /// <summary>Adds an informational log entry.</summary>
    public void LogInformation(string message) => Logs.Enqueue(new LogEntry { Message = message, Type = LogType.Information });
}