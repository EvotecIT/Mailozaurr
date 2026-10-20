namespace Mailozaurr;

public partial class Graph {
    /// <summary>Suppresses provider bodies, headers and exception details in send logs, failure results and thrown send errors.</summary>
    /// <remarks>Defaults to false to preserve detailed send diagnostics. Authentication failures are always sanitized.</remarks>
    public bool SuppressSensitiveDiagnostics { get; set; }

    private const string SafeSendFailure = "Microsoft Graph send failed.";

    private void LogGraphFailure(string context, Exception exception) {
        LogCollector.LogWarning(SuppressSensitiveDiagnostics ? context : $"{context}: {exception.Message}");
    }

    private Exception SafeSendException(Exception exception) => exception is GraphApiException graphException
        ? new GraphApiException(graphException.StatusCode, SafeSendFailure, string.Empty, graphException.RetryAfter)
        : exception is OperationCanceledException canceled
            ? new OperationCanceledException("Microsoft Graph send canceled.", canceled.CancellationToken)
            : new InvalidOperationException(SafeSendFailure);
}
