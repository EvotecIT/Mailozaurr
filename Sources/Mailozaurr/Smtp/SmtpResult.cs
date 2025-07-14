namespace Mailozaurr;

/// <summary>
/// Class that holds the result of the SMTP operation
/// </summary>
/// <remarks>
/// Used by various send methods to provide feedback about the
/// outcome of sending an email.
/// </remarks>
public class SmtpResult {
    /// <summary>Whether the operation succeeded.</summary>
    public bool Status { get; set; }
    /// <summary>The type of email action that was performed.</summary>
    public EmailAction EmailAction { get; set; }
    /// <summary>The recipients the message was sent to.</summary>
    public string SentTo { get; set; }
    /// <summary>The sender address.</summary>
    public string SentFrom { get; set; }
    /// <summary>Optional message returned by the operation.</summary>
    public string? Message { get; set; }
    /// <summary>Time taken to perform the action.</summary>
    public TimeSpan TimeToExecute { get; set; }
    /// <summary>The server used to send the message.</summary>
    public string Server { get; set; }
    /// <summary>The port used to connect.</summary>
    public int Port { get; set; }
    /// <summary>Error information if the operation failed.</summary>
    public string? Error { get; set; }
    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpResult"/> class.
    /// </summary>
    /// <param name="status">if set to <c>true</c> [status].</param>
    /// <param name="emailAction">The email action.</param>
    /// <param name="sentTo">The sent to.</param>
    /// <param name="sentFrom">The sent from.</param>
    /// <param name="server">The server.</param>
    /// <param name="port">The port.</param>
    /// <param name="timeToExecute">The time to execute.</param>
    /// <param name="outputMessage">The output message.</param>
    public SmtpResult(bool status, EmailAction emailAction, string sentTo, string sentFrom, string server, int port, TimeSpan timeToExecute, string? outputMessage = null, string? error = null) {
        Status = status;
        SentTo = sentTo;
        SentFrom = sentFrom;
        Server = server;
        Port = port;
        TimeToExecute = timeToExecute;
        Message = outputMessage;
        EmailAction = emailAction;
        Error = error;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpResult"/> class.
    /// </summary>
    /// <param name="status">if set to <c>true</c> [status].</param>
    /// <param name="emailAction">The email action.</param>
    /// <param name="sentTo">The sent to.</param>
    /// <param name="sentFrom">The sent from.</param>
    /// <param name="server">The server.</param>
    /// <param name="port">The port.</param>
    /// <param name="timeToExecute">The time to execute.</param>
    /// <param name="loggingConfigurator">The logging configurator.</param>
    public SmtpResult(bool status, EmailAction emailAction, string sentTo, string sentFrom, string server, int port, TimeSpan timeToExecute, LoggingConfigurator? loggingConfigurator) {
        Status = status;
        SentTo = sentTo;
        SentFrom = sentFrom;
        Server = server;
        Port = port;
        TimeToExecute = timeToExecute;
        EmailAction = emailAction;

        if (loggingConfigurator?.ProtocolLogger != null && loggingConfigurator.LogObject) {
            Message = Encoding.ASCII.GetString(loggingConfigurator.LogStream?.ToArray() ?? Array.Empty<byte>());
        }
    }
}
