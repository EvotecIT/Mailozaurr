using MailKit.Net.Smtp;

namespace Mailozaurr;

/// <summary>
/// Represents details about an SMTP server retrieved when testing connectivity.
/// </summary>
public sealed class SmtpConnectionInfo {
    /// <summary>Server that was tested.</summary>
    public string Server { get; }
    /// <summary>Port used during the test.</summary>
    public int Port { get; }
    /// <summary>Raw banner line returned by the server.</summary>
    public string? Banner { get; }
    /// <summary>Server software parsed from the banner if available.</summary>
    public string? Software { get; }
    /// <summary>Capabilities advertised by the server.</summary>
    public SmtpCapabilities Capabilities { get; }
    /// <summary>Indicates whether the server kept the connection open after a NOOP.</summary>
    public bool Persistent { get; }
    /// <summary>Optional recipient probe result when RCPT validation was requested.</summary>
    public SmtpRecipientProbeInfo? RecipientProbe { get; }
    /// <summary>Optional validation message result when a deliberate test send was requested.</summary>
    public SmtpValidationMessageInfo? ValidationMessage { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpConnectionInfo"/> class.
    /// </summary>
    public SmtpConnectionInfo(string server, int port, string? banner, string? software, SmtpCapabilities capabilities, bool persistent) {
        Server = server;
        Port = port;
        Banner = banner;
        Software = software;
        Capabilities = capabilities;
        Persistent = persistent;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpConnectionInfo"/> class.
    /// </summary>
    public SmtpConnectionInfo(string server, int port, string? banner, string? software, SmtpCapabilities capabilities, bool persistent, SmtpRecipientProbeInfo? recipientProbe) {
        Server = server;
        Port = port;
        Banner = banner;
        Software = software;
        Capabilities = capabilities;
        Persistent = persistent;
        RecipientProbe = recipientProbe;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpConnectionInfo"/> class.
    /// </summary>
    public SmtpConnectionInfo(string server, int port, string? banner, string? software, SmtpCapabilities capabilities, bool persistent, SmtpRecipientProbeInfo? recipientProbe, SmtpValidationMessageInfo? validationMessage) {
        Server = server;
        Port = port;
        Banner = banner;
        Software = software;
        Capabilities = capabilities;
        Persistent = persistent;
        RecipientProbe = recipientProbe;
        ValidationMessage = validationMessage;
    }
}
