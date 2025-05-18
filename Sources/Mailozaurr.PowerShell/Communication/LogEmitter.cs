using Mailozaurr.Logging;

namespace Mailozaurr.PowerShell;

/// <summary>
/// LogEmitter is a static class that emits logs to the PowerShell cmdlet.
/// It's used as a way to decouple the logging from the cmdlet.
/// - `LoggingMessages.Logger.WriteXXX` static method should be used for AsyncPSCmdlet.
/// - `LogCollector.LogWarning($"Send-EmailMessage - Error during sending using SendGrid: {ex.Message}")` should be used for native PSCmdlet.
/// </summary>
public static class LogEmitter {
    /// <summary>
    /// Emits logs to the PowerShell cmdlet.
    /// </summary>
    /// <param name="collector">The log collector.</param>
    /// <param name="cmdlet">The PowerShell cmdlet.</param>
    public static void EmitLogs(LogCollector collector, PSCmdlet cmdlet) {
        while (collector.Logs.TryDequeue(out var log)) {
            switch (log.Type) {
                case Mailozaurr.Logging.LogType.Warning:
                    cmdlet.WriteWarning(log.Message);
                    break;
                case Mailozaurr.Logging.LogType.Error:
                    cmdlet.WriteError(new ErrorRecord(new Exception(log.Message), "SendGridError", ErrorCategory.NotSpecified, null));
                    break;
                case Mailozaurr.Logging.LogType.Verbose:
                    cmdlet.WriteVerbose(log.Message);
                    break;
                case Mailozaurr.Logging.LogType.Information:
                    cmdlet.WriteInformation(log.Message, new string[0]);
                    break;
            }
        }
    }
}