using MailKit;

namespace Mailozaurr;

/// <summary>
/// Represents an IMAP email message along with its unique identifier.
/// </summary>
public class ImapEmailMessage {
    /// <summary>
    /// Creates a new instance of <see cref="ImapEmailMessage"/>.
    /// </summary>
    /// <param name="uid">Unique identifier of the message.</param>
    /// <param name="message">The actual MIME message.</param>
    public ImapEmailMessage(UniqueId uid, MimeMessage message) {
        Uid = uid;
        Message = message;
        Encryption = MimeKitUtils.GetEncryption(message);
    }

    /// <summary>Unique identifier of the message.</summary>
    public UniqueId Uid { get; }

    /// <summary>The underlying <see cref="MimeMessage"/>.</summary>
    public MimeMessage Message { get; }

    /// <summary>Detected encryption or signature type.</summary>
    public EmailEncryption Encryption { get; }

    /// <inheritdoc />
    public override string ToString() => Message.Subject ?? base.ToString();
}
