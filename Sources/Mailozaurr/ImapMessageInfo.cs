using System;
using System.Linq;
using MailKit;

namespace Mailozaurr;

/// <summary>
/// Provides a user friendly view over an IMAP email message.
/// </summary>
public class ImapMessageInfo {
    /// <summary>
    /// Initializes a new instance of the <see cref="ImapMessageInfo"/> class.
    /// </summary>
    /// <param name="message">The underlying IMAP email message.</param>
    public ImapMessageInfo(ImapEmailMessage message) {
        Raw = message;
    }

    /// <summary>The wrapped <see cref="ImapEmailMessage"/>.</summary>
    public ImapEmailMessage Raw { get; }

    /// <summary>Unique identifier of the message.</summary>
    public UniqueId Uid => Raw.Uid;

    /// <summary>Sender addresses.</summary>
    public string From => string.Join(", ", Raw.Message.From.Mailboxes.Select(m => m.ToString()));

    /// <summary>Recipient addresses.</summary>
    public string To => string.Join(", ", Raw.Message.To.Mailboxes.Select(m => m.ToString()));

    /// <summary>Subject of the message.</summary>
    public string? Subject => Raw.Message.Subject;

    /// <summary>Date the message was sent.</summary>
    public DateTime Date => Raw.Message.Date.DateTime;

    /// <inheritdoc />
    public override string ToString() => Subject ?? base.ToString();
}
