namespace Mailozaurr.NonDeliveryReports;

/// <summary>
/// Describes common Non-Delivery Report types.
/// </summary>
public enum NonDeliveryReportType {
    /// <summary>Type of the failure couldn't be determined.</summary>
    Unknown,
    /// <summary>The message was permanently rejected.</summary>
    HardBounce,
    /// <summary>The message failed temporarily and may be retried.</summary>
    SoftBounce,
    /// <summary>The recipient's mailbox is full.</summary>
    MailboxFull,
    /// <summary>The recipient does not exist.</summary>
    UnknownRecipient
}
