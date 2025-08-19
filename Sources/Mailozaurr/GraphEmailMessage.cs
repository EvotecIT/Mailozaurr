namespace Mailozaurr;

using Mailozaurr.NonDeliveryReports;
using MimeKit;

/// <summary>
/// Represents a MIME message retrieved via Microsoft Graph along with its identifier.
/// </summary>
public class GraphEmailMessage {
    /// <summary>
    /// Creates a new instance of <see cref="GraphEmailMessage"/>.
    /// </summary>
    /// <param name="id">Unique identifier of the message.</param>
    /// <param name="message">The MIME message.</param>
    /// <param name="nonDeliveryReports">Optional pre-parsed non delivery reports.</param>
    public GraphEmailMessage(string id, MimeMessage message, IList<NonDeliveryReport>? nonDeliveryReports = null) {
        Id = id;
        Message = message;
        Encryption = MimeKitUtils.GetEncryption(message);
        NonDeliveryReports = nonDeliveryReports ?? MimeKitUtils.GetNonDeliveryReports(message);
    }

    /// <summary>Unique identifier of the message.</summary>
    public string Id { get; }

    /// <summary>The underlying <see cref="MimeMessage"/>.</summary>
    public MimeMessage Message { get; }

    /// <summary>Detected encryption or signature type.</summary>
    public EmailEncryption Encryption { get; }

    /// <summary>Parsed Non-Delivery Report details, if available.</summary>
    public IList<NonDeliveryReport> NonDeliveryReports { get; }

    /// <inheritdoc />
    public override string ToString() => Message.Subject ?? base.ToString()!;
}
