namespace Mailozaurr;

/// <summary>Graph send result with provider-specific structured error details.</summary>
public sealed class GraphSmtpResult : SmtpResult {
    /// <summary>Initializes a Graph send result.</summary>
    public GraphSmtpResult(bool status, EmailAction emailAction, string sentTo, string sentFrom,
        string server, int port, TimeSpan timeToExecute, string? outputMessage = null,
        string? error = null)
        : base(status, emailAction, sentTo, sentFrom, server, port, timeToExecute,
            outputMessage, error) { }

    /// <summary>Parsed Graph API error details, when available.</summary>
    public GraphApiErrorResponse? GraphError { get; set; }

    internal GraphSmtpResult(SmtpResult source, string? error)
        : base(source.Status, source.EmailAction, source.SentTo, source.SentFrom,
            source.Server, source.Port, source.TimeToExecute, source.Message, error) {
        MessageId = source.MessageId;
        Queued = source.Queued;
        GraphError = (source as GraphSmtpResult)?.GraphError;
    }
}
