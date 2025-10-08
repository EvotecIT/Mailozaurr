using System;
using System.Linq;
using MimeKit;

namespace Mailozaurr;

/// <summary>
/// Provides a user friendly view over a POP3 email message.
/// </summary>
/// <remarks>
/// Exposes common message properties to simplify scripting scenarios
/// when dealing with POP3 servers.
/// </remarks>
public class Pop3MessageInfo {
    /// <summary>
    /// Initializes a new instance of the <see cref="Pop3MessageInfo"/> class.
    /// </summary>
    /// <param name="message">The underlying POP3 email message.</param>
    public Pop3MessageInfo(Pop3EmailMessage message) {
        Raw = message;
    }

    /// <summary>The wrapped <see cref="Pop3EmailMessage"/>.</summary>
    public Pop3EmailMessage Raw { get; }

    /// <summary>Index of the message in the mailbox.</summary>
    public int Index => Raw.Index;

    /// <summary>Sender addresses.</summary>
    public string From => string.Join(", ", Raw.Message.From.Mailboxes.Select(m => m.ToString()));

    /// <summary>Recipient addresses.</summary>
    public string To => string.Join(", ", Raw.Message.To.Mailboxes.Select(m => m.ToString()));

    /// <summary>Subject of the message.</summary>
    public string? Subject => Raw.Message.Subject;

    /// <summary>Date the message was sent.</summary>
    public DateTime Date => Raw.Message.Date.DateTime;

    /// <summary>Plain text body.</summary>
    public string? TextBody => Raw.Message.TextBody;

    /// <summary>HTML body.</summary>
    public string? HtmlBody => Raw.Message.HtmlBody;

    /// <summary>Message priority.</summary>
    public MessagePriority Priority => Raw.Message.Priority switch {
        MimeKit.MessagePriority.Urgent => MessagePriority.High,
        MimeKit.MessagePriority.NonUrgent => MessagePriority.Low,
        _ => MessagePriority.Normal,
    };

    /// <summary>Whether the message has any attachments.</summary>
    public bool HasAttachments => Raw.Message.Attachments.Any();

    /// <summary>Encryption or signature detected.</summary>
    public EmailEncryption Encryption => Raw.Encryption;

    /// <inheritdoc />
    public override string ToString() => Subject ?? string.Empty;
}
