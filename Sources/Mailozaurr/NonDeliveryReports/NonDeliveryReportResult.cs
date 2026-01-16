using Mailozaurr;

namespace Mailozaurr.NonDeliveryReports;

/// <summary>
/// Represents a non-delivery report paired with an optional sent message record.
/// </summary>
public sealed class NonDeliveryReportResult {
    /// <summary>
    /// Initializes a new instance of the <see cref="NonDeliveryReportResult"/> class.
    /// </summary>
    /// <param name="report">Parsed non-delivery report details.</param>
    /// <param name="sentMessage">Associated sent message record, if available.</param>
    public NonDeliveryReportResult(NonDeliveryReport report, SentMessageRecord? sentMessage) {
        Report = report;
        SentMessage = sentMessage;
    }

    /// <summary>Parsed non-delivery report details.</summary>
    public NonDeliveryReport Report { get; }

    /// <summary>Sent message record corresponding to the report if resolved; otherwise <c>null</c>.</summary>
    public SentMessageRecord? SentMessage { get; }
}
