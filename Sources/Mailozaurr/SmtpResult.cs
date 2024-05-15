using System.Text;

namespace Mailozaurr;

/// <summary>
/// Class that holds the result of the SMTP operation
/// </summary>
public class SmtpResult {
    public bool Status { get; set; }
    public string SentTo { get; set; }
    public string SentFrom { get; set; }
    public string? Message { get; set; }
    public TimeSpan TimeToExecute { get; set; }
    public string Server { get; set; }
    public int Port { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpResult"/> class.
    /// </summary>
    /// <param name="status">if set to <c>true</c> [status].</param>
    /// <param name="sentTo">The sent to.</param>
    /// <param name="sentFrom">The sent from.</param>
    /// <param name="server">The server.</param>
    /// <param name="port">The port.</param>
    /// <param name="timeToExecute">The time to execute.</param>
    /// <param name="outputMessage">The output message.</param>
    public SmtpResult(bool status, string sentTo, string sentFrom, string server, int port, TimeSpan timeToExecute, string? outputMessage = null) {
        Status = status;
        SentTo = sentTo;
        SentFrom = sentFrom;
        Server = server;
        Port = port;
        TimeToExecute = timeToExecute;
        Message = outputMessage;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpResult"/> class.
    /// </summary>
    /// <param name="status">if set to <c>true</c> [status].</param>
    /// <param name="sentTo">The sent to.</param>
    /// <param name="sentFrom">The sent from.</param>
    /// <param name="server">The server.</param>
    /// <param name="port">The port.</param>
    /// <param name="timeToExecute">The time to execute.</param>
    /// <param name="loggingConfigurator">The logging configurator.</param>
    public SmtpResult(bool status, string sentTo, string sentFrom, string server, int port, TimeSpan timeToExecute, LoggingConfigurator? loggingConfigurator) {
        Status = status;
        SentTo = sentTo;
        SentFrom = sentFrom;
        Server = server;
        Port = port;
        TimeToExecute = timeToExecute;

        if (loggingConfigurator?.ProtocolLogger != null && loggingConfigurator.LogObject) {
            Message = Encoding.ASCII.GetString(loggingConfigurator.LogStream?.ToArray() ?? Array.Empty<byte>());
        }
    }
}
