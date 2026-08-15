namespace Mailozaurr.Hosting;

/// <summary>
/// Identifies the mailbox or transport technology behind a profile.
/// </summary>
public enum MailProfileKind {
    /// <summary>Unknown or not yet configured.</summary>
    Unknown = 0,

    /// <summary>IMAP mailbox access.</summary>
    Imap,

    /// <summary>POP3 mailbox access.</summary>
    Pop3,

    /// <summary>Microsoft Graph mailbox access.</summary>
    Graph,

    /// <summary>Gmail API mailbox access.</summary>
    Gmail,

    /// <summary>SMTP transport.</summary>
    Smtp,

    /// <summary>SendGrid transport.</summary>
    SendGrid,

    /// <summary>Mailgun transport.</summary>
    Mailgun,

    /// <summary>Amazon SES transport.</summary>
    Ses,
}