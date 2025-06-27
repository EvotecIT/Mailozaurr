using System.IO;

namespace Mailozaurr;

/// <summary>
/// Configures protocol logging for SMTP and other clients.
/// </summary>
public class LoggingConfigurator {
    /// <summary>Gets the in-memory log stream when logging to an object.</summary>
    public MemoryStream? LogStream { get; private set; }
    /// <summary>True when logging to the console is enabled.</summary>
    public bool LogConsole { get; private set; }
    /// <summary>True when log entries should be captured for later emission.</summary>
    public bool LogObject { get; private set; }
    /// <summary>True when timestamps should be included.</summary>
    public bool LogTimestamps { get; private set; }
    /// <summary>True when secrets should appear in logs.</summary>
    public bool LogSecrets { get; private set; }
    /// <summary>Optional format string for timestamps.</summary>
    public string? LogTimestampsFormat { get; private set; }
    /// <summary>Optional prefix for server messages.</summary>
    public string? LogServerPrefix { get; private set; }
    /// <summary>Optional prefix for client messages.</summary>
    public string? LogClientPrefix { get; private set; }
    /// <summary>Indicates whether existing log files should be overwritten.</summary>
    public bool LogOverwrite { get; private set; }
    /// <summary>The path of the log file.</summary>
    public string? LogPath { get; private set; }
    internal ProtocolLogger? ProtocolLogger { get; set; }

    /// <summary>
    /// Configures protocol logging.
    /// </summary>
    /// <param name="logPath">Path to the log file or <c>null</c> to disable file logging.</param>
    /// <param name="logConsole">Enable console logging.</param>
    /// <param name="logObject">Capture log entries in memory for later use.</param>
    /// <param name="logTimestamps">Include timestamps in the log.</param>
    /// <param name="logSecrets">Include secret values in the log.</param>
    /// <param name="logTimestampsFormat">Optional timestamp format.</param>
    /// <param name="logServerPrefix">Optional prefix for server messages.</param>
    /// <param name="logClientPrefix">Optional prefix for client messages.</param>
    /// <param name="logOverwrite">Overwrite existing log file if it exists.</param>
    public void ConfigureLogging(string logPath, bool logConsole, bool logObject, bool logTimestamps, bool logSecrets, string? logTimestampsFormat = null, string? logServerPrefix = null, string? logClientPrefix = null, bool logOverwrite = false) {
        LogTimestamps = logTimestamps;
        LogSecrets = logSecrets;
        LogTimestampsFormat = logTimestampsFormat;
        LogServerPrefix = logServerPrefix;
        LogClientPrefix = logClientPrefix;
        LogOverwrite = logOverwrite;
        LogObject = logObject;
        LogConsole = logConsole;
        LogPath = logPath;

        ProtocolLogger? protocolLogger = null;
        if (!string.IsNullOrWhiteSpace(logPath) || logConsole || logObject) {
            if (!string.IsNullOrWhiteSpace(logPath)) {
                try {
                    protocolLogger = new ProtocolLogger(logPath, logOverwrite);
                } catch (IOException ex) {
                    LoggingMessages.Logger.WriteWarning($"Couldn't create protocol logger with {logPath}: {ex.Message}. Using console output instead.");
                    protocolLogger = new ProtocolLogger(Console.OpenStandardOutput());
                }
            } else if (logConsole) {
                protocolLogger = new ProtocolLogger(Console.OpenStandardOutput());
            } else {
                LogStream = new MemoryStream();
                protocolLogger = new ProtocolLogger(LogStream);
            }

            protocolLogger.LogTimestamps = logTimestamps;
            protocolLogger.RedactSecrets = !logSecrets;

            if (!string.IsNullOrWhiteSpace(logTimestampsFormat)) {
                protocolLogger.TimestampFormat = logTimestampsFormat;
            }

            if (!string.IsNullOrWhiteSpace(logServerPrefix)) {
                protocolLogger.ServerPrefix = logServerPrefix;
            }

            if (!string.IsNullOrWhiteSpace(logClientPrefix)) {
                protocolLogger.ClientPrefix = logClientPrefix;
            }
        }
        ProtocolLogger = protocolLogger;
    }
}
