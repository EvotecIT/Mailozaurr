namespace Mailozaurr;

public static class LoggingConfigurator {
    public static ProtocolLogger? ConfigureLogging(string logPath, bool logConsole, bool logObject, bool logTimestamps, bool logSecrets, string? logTimestampsFormat = null, string? logServerPrefix = null, string? logClientPrefix = null, bool logOverwrite = false) {
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
                var stream = new MemoryStream();
                protocolLogger = new ProtocolLogger(stream);
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

        return protocolLogger;
    }
}
