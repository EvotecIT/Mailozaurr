namespace Mailozaurr;

/// <summary>
/// Represents the result of an SMTP envelope recipient probe that stops before DATA.
/// </summary>
public sealed class SmtpRecipientProbeInfo {
    /// <summary>SMTP server that was tested.</summary>
    public string Server { get; }
    /// <summary>TCP port used during the probe.</summary>
    public int Port { get; }
    /// <summary>EHLO/HELO name sent by the probe.</summary>
    public string HeloHost { get; }
    /// <summary>Envelope sender used in the MAIL FROM command.</summary>
    public string Sender { get; }
    /// <summary>Recipient tested with the RCPT TO command.</summary>
    public string Recipient { get; }
    /// <summary>Initial SMTP banner returned by the server.</summary>
    public string? Banner { get; }
    /// <summary>Indicates whether STARTTLS was negotiated before the envelope probe.</summary>
    public bool StartTlsUsed { get; }
    /// <summary>Status code returned for MAIL FROM.</summary>
    public int? MailFromStatusCode { get; }
    /// <summary>Response returned for MAIL FROM.</summary>
    public string? MailFromResponse { get; }
    /// <summary>Status code returned for RCPT TO.</summary>
    public int? RecipientStatusCode { get; }
    /// <summary>Response returned for RCPT TO.</summary>
    public string? RecipientResponse { get; }
    /// <summary>Indicates whether the server accepted the tested recipient at SMTP envelope stage.</summary>
    public bool Accepted { get; }
    /// <summary>Error captured when the probe could not complete.</summary>
    public string? Error { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpRecipientProbeInfo"/> class.
    /// </summary>
    public SmtpRecipientProbeInfo(
        string server,
        int port,
        string heloHost,
        string sender,
        string recipient,
        string? banner,
        bool startTlsUsed,
        int? mailFromStatusCode,
        string? mailFromResponse,
        int? recipientStatusCode,
        string? recipientResponse,
        bool accepted,
        string? error) {
        Server = server;
        Port = port;
        HeloHost = heloHost;
        Sender = sender;
        Recipient = recipient;
        Banner = banner;
        StartTlsUsed = startTlsUsed;
        MailFromStatusCode = mailFromStatusCode;
        MailFromResponse = mailFromResponse;
        RecipientStatusCode = recipientStatusCode;
        RecipientResponse = recipientResponse;
        Accepted = accepted;
        Error = error;
    }
}
