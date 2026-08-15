namespace Mailozaurr;

/// <summary>
/// Represents the outcome of a deliberate SMTP validation message send.
/// </summary>
public sealed class SmtpValidationMessageInfo {
    /// <summary>SMTP server used for the validation send.</summary>
    public string Server { get; }
    /// <summary>TCP port used for the validation send.</summary>
    public int Port { get; }
    /// <summary>Generated or supplied validation identifier.</summary>
    public string TestId { get; }
    /// <summary>Envelope and header sender used for the validation message.</summary>
    public string Sender { get; }
    /// <summary>Recipient used for the validation message.</summary>
    public string Recipient { get; }
    /// <summary>EHLO/HELO name used by the SMTP client.</summary>
    public string HeloHost { get; }
    /// <summary>Subject used for the validation message.</summary>
    public string Subject { get; }
    /// <summary>Message-ID used for the validation message.</summary>
    public string? MessageId { get; }
    /// <summary>Underlying SMTP send result.</summary>
    public SmtpResult Result { get; }
    /// <summary>Indicates whether the validation message was accepted by the SMTP server.</summary>
    public bool Sent => Result.Status;
    /// <summary>Error captured when the validation send failed.</summary>
    public string? Error => Result.Error;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpValidationMessageInfo"/> class.
    /// </summary>
    public SmtpValidationMessageInfo(
        string server,
        int port,
        string testId,
        string sender,
        string recipient,
        string heloHost,
        string subject,
        string? messageId,
        SmtpResult result) {
        Server = server;
        Port = port;
        TestId = testId;
        Sender = sender;
        Recipient = recipient;
        HeloHost = heloHost;
        Subject = subject;
        MessageId = messageId;
        Result = result;
    }
}
