using System;

namespace Mailozaurr;

// Keep service diagnostics independent of caller log sinks, including callback failures.
internal sealed class SmtpDiagnosticScope : IDisposable {
    private readonly Smtp _smtp;
    private readonly bool _previousSuppression;
    private readonly InternalLogger _previousLogger;

    internal SmtpDiagnosticScope(Smtp smtp) {
        _smtp = smtp;
        _previousSuppression = smtp.SuppressDiagnostics;
        _previousLogger = LoggingMessages.Logger;
        smtp.SuppressDiagnostics = true;
        LoggingMessages.Logger = new InternalLogger();
    }

    public void Dispose() {
        _smtp.SuppressDiagnostics = _previousSuppression;
        LoggingMessages.Logger = _previousLogger;
    }
}
