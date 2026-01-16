using Mailozaurr.NonDeliveryReports;

namespace Mailozaurr;

/// <summary>
/// Represents a POP3 email message along with its index.
/// </summary>
/// <remarks>
/// Similar to <see cref="ImapEmailMessage"/> but used when retrieving
/// messages from a POP3 server.
/// </remarks>
public class Pop3EmailMessage {
    /// <summary>
    /// Creates a new instance of <see cref="Pop3EmailMessage"/>.
    /// </summary>
    /// <param name="index">Index of the message within the mailbox.</param>
    /// <param name="message">The actual MIME message.</param>
    /// <param name="nonDeliveryReports">Optional parsed NDRs associated with the message.</param>
    public Pop3EmailMessage(int index, MimeMessage message, IList<NonDeliveryReport>? nonDeliveryReports = null) {
        Index = index;
        Message = message;
        Encryption = MimeKitUtils.GetEncryption(message);
        NonDeliveryReports = nonDeliveryReports ?? MimeKitUtils.GetNonDeliveryReports(message);
    }

    /// <summary>Index of the message within the mailbox.</summary>
    public int Index { get; }

    /// <summary>The underlying <see cref="MimeMessage"/>.</summary>
    public MimeMessage Message { get; }

    /// <summary>Detected encryption or signature type.</summary>
    public EmailEncryption Encryption { get; }

    /// <summary>Parsed Non-Delivery Report details, if available.</summary>
    public IList<NonDeliveryReport> NonDeliveryReports { get; }

    /// <inheritdoc />
    public override string ToString() => Message.Subject ?? string.Empty;
}
