using System.Collections.Generic;

namespace Mailozaurr.NonDeliveryReports;

/// <summary>Default implementation supplying common Non-Delivery Report subject patterns.</summary>
public class DefaultNonDeliveryReportSubjectPatternProvider : INonDeliveryReportSubjectPatternProvider {
    /// <inheritdoc />
    public ICollection<string> SubjectPatterns { get; } = new List<string> {
        "Undelivered Mail Returned to Sender",
        "Delivery Status Notification",
        "Mail delivery failed",
        "Mail Delivery Subsystem",
        "Failure Notice",
        "Delivery failure",
    };
}

