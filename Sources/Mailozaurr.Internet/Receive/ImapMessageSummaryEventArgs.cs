using MailKit;
using System;

namespace Mailozaurr;

/// <summary>
/// Lightweight IMAP IDLE arrival evidence that does not require downloading a MIME message body.
/// </summary>
public sealed class ImapMessageSummaryEventArgs : EventArgs {
    /// <summary>Creates lightweight arrival evidence.</summary>
    public ImapMessageSummaryEventArgs(UniqueId uid, string? subject) {
        Uid = uid;
        Subject = subject;
    }

    /// <summary>IMAP unique identifier.</summary>
    public UniqueId Uid { get; }

    /// <summary>Message subject when included in the fetched envelope.</summary>
    public string? Subject { get; }
}
