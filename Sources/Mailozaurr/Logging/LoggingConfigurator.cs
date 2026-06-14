using System;
using System.IO;

namespace Mailozaurr;

/// <summary>
/// Configures protocol logging for SMTP and other clients.
/// </summary>
/// <remarks>
/// Enables capturing protocol transcripts either in memory or on
/// disk so that troubleshooting information can be reviewed.
/// </remarks>
public class LoggingConfigurator : IDisposable {
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
    /// <summary>The path of the log file or <see langword="null"/> when not logging to a file.</summary>
    public string? LogPath { get; private set; }
    internal ProtocolLogger? ProtocolLogger { get; set; }
    private bool _disposed;

    /// <summary>
    /// Configures protocol logging.
    /// </summary>
    /// <param name="logPath">Path to the log file or <see langword="null"/> to disable file logging.</param>
    /// <param name="logConsole">Enable console logging.</param>
    /// <param name="logObject">Capture log entries in memory for later use.</param>
    /// <param name="logTimestamps">Include timestamps in the log.</param>
    /// <param name="logSecrets">Include secret values in the log.</param>
    /// <param name="logTimestampsFormat">Optional timestamp format.</param>
    /// <param name="logServerPrefix">Optional prefix for server messages.</param>
    /// <param name="logClientPrefix">Optional prefix for client messages.</param>
    /// <param name="logOverwrite">Overwrite existing log file if it exists.</param>
    public void ConfigureLogging(string? logPath, bool logConsole, bool logObject, bool logTimestamps, bool logSecrets, string? logTimestampsFormat = null, string? logServerPrefix = null, string? logClientPrefix = null, bool logOverwrite = false) {
        LogTimestamps = logTimestamps;
        LogSecrets = logSecrets;
        // Validate formats and prefixes
        if (!string.IsNullOrWhiteSpace(logTimestampsFormat)) {
            try {
                _ = DateTimeOffset.UtcNow.ToString(logTimestampsFormat);
                LogTimestampsFormat = logTimestampsFormat;
            } catch (FormatException) {
                LoggingMessages.Logger.WriteWarning("Invalid log timestamp format '{0}'. Falling back to default.", logTimestampsFormat!);
                LogTimestampsFormat = null;
            }
        } else {
            LogTimestampsFormat = null;
        }
        if (!string.IsNullOrEmpty(logServerPrefix) && (logServerPrefix.Contains('\n') || logServerPrefix.Contains('\r'))) {
            LoggingMessages.Logger.WriteWarning("Server log prefix contains new lines. Stripping them for safety.");
            logServerPrefix = logServerPrefix!.Replace("\r", string.Empty).Replace("\n", string.Empty);
        }
        if (!string.IsNullOrEmpty(logClientPrefix) && (logClientPrefix.Contains('\n') || logClientPrefix.Contains('\r'))) {
            LoggingMessages.Logger.WriteWarning("Client log prefix contains new lines. Stripping them for safety.");
            logClientPrefix = logClientPrefix!.Replace("\r", string.Empty).Replace("\n", string.Empty);
        }
        LogServerPrefix = logServerPrefix;
        LogClientPrefix = logClientPrefix;
        LogOverwrite = logOverwrite;
        LogObject = logObject;
        LogConsole = logConsole;
        LogPath = logPath;

        ProtocolLogger? protocolLogger = null;
        if (!string.IsNullOrWhiteSpace(logPath) || logConsole || logObject) {
            if (!string.IsNullOrWhiteSpace(logPath)) {
                var protocolLogPath = logPath!;
                try {
                    var directory = Path.GetDirectoryName(protocolLogPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
                        Directory.CreateDirectory(directory);
                    }
                } catch (Exception ex) {
                    LoggingMessages.Logger.WriteWarning("Couldn't create directory for protocol logs at '{0}': {1}. Using console output instead.", protocolLogPath, ex.Message);
                    protocolLogPath = string.Empty;
                }
                if (protocolLogPath.Length == 0) {
                    protocolLogger = new ProtocolLogger(Console.OpenStandardOutput());
                } else {
                    try {
                        protocolLogger = new ProtocolLogger(protocolLogPath, logOverwrite);
                    } catch (IOException ex) {
                        LoggingMessages.Logger.WriteWarning($"Couldn't create protocol logger with {protocolLogPath}: {ex.Message}. Using console output instead.");
                        protocolLogger = new ProtocolLogger(Console.OpenStandardOutput());
                    }
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
                protocolLogger.TimestampFormat = logTimestampsFormat!;
            }

            if (!string.IsNullOrWhiteSpace(logServerPrefix)) {
                protocolLogger.ServerPrefix = logServerPrefix!;
            }

            if (!string.IsNullOrWhiteSpace(logClientPrefix)) {
                protocolLogger.ClientPrefix = logClientPrefix!;
            }
        }
        ProtocolLogger = protocolLogger;
    }

    /// <summary>Finalizer that ensures unmanaged resources are released.</summary>
    ~LoggingConfigurator() => Dispose(false);

    /// <summary>Releases resources used by the logger.</summary>
    /// <param name="disposing">When true, disposes managed resources as well.</param>
    protected virtual void Dispose(bool disposing) {
        if (_disposed) {
            return;
        }

        if (disposing) {
            ProtocolLogger?.Dispose();
            ProtocolLogger = null;
            LogStream?.Dispose();
            LogStream = null;
        }

        _disposed = true;
    }

    /// <summary>Disposes the configurator and suppresses finalization.</summary>
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}