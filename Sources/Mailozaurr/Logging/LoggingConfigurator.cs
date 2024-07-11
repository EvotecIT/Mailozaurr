namespace Mailozaurr;

public class LoggingConfigurator {
    public MemoryStream? LogStream { get; private set; }
    public bool LogConsole { get; private set; }
    public bool LogObject { get; private set; }
    public bool LogTimestamps { get; private set; }
    public bool LogSecrets { get; private set; }
    public string? LogTimestampsFormat { get; private set; }
    public string? LogServerPrefix { get; private set; }
    public string? LogClientPrefix { get; private set; }
    public bool LogOverwrite { get; private set; }
    public string? LogPath { get; private set; }
    internal ProtocolLogger? ProtocolLogger { get; set; }

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
        if (!string.IsNullOrEmpty(logPath) || logConsole || logObject) {
            if (!string.IsNullOrEmpty(logPath)) {
                try {
                    protocolLogger = new ProtocolLogger(logPath, logOverwrite);
                } catch {
                    //Console.WriteLine($"Couldn't create protocol logger with {logPath}. Using console output instead.");
                    // TODO: add logging
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

            if (!string.IsNullOrEmpty(logTimestampsFormat)) {
                protocolLogger.TimestampFormat = logTimestampsFormat;
            }

            if (!string.IsNullOrEmpty(logServerPrefix)) {
                protocolLogger.ServerPrefix = logServerPrefix;
            }

            if (!string.IsNullOrEmpty(logClientPrefix)) {
                protocolLogger.ClientPrefix = logClientPrefix;
            }
        }
        ProtocolLogger = protocolLogger;
    }
}
