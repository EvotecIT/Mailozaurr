using MailKit;
using Mailozaurr.NonDeliveryReports;

namespace Mailozaurr;

/// <summary>
/// Represents an IMAP email message along with its unique identifier.
/// </summary>
/// <remarks>
/// Provides a small wrapper over <see cref="MimeMessage"/> so that
/// metadata such as encryption state can be carried with the message.
/// </remarks>
public class ImapEmailMessage {
    /// <summary>
    /// Creates a new instance of <see cref="ImapEmailMessage"/>.
    /// </summary>
    /// <param name="uid">Unique identifier of the message.</param>
    /// <param name="message">The actual MIME message.</param>
    /// <param name="nonDeliveryReports">Optional pre-parsed non delivery reports.</param>
    public ImapEmailMessage(UniqueId uid, MimeMessage message, IList<NonDeliveryReport>? nonDeliveryReports = null) {
        Uid = uid;
        Message = message;
        Encryption = MimeKitUtils.GetEncryption(message);
        NonDeliveryReports = nonDeliveryReports ?? MimeKitUtils.GetNonDeliveryReports(message);
    }

    /// <summary>Unique identifier of the message.</summary>
    public UniqueId Uid { get; }

    /// <summary>The underlying <see cref="MimeMessage"/>.</summary>
    public MimeMessage Message { get; }

    /// <summary>Detected encryption or signature type.</summary>
    public EmailEncryption Encryption { get; }

    /// <summary>Parsed Non-Delivery Report details, if available.</summary>
    public IList<NonDeliveryReport> NonDeliveryReports { get; }

    /// <inheritdoc />
    public override string ToString() => Message.Subject ?? base.ToString()!;
}
