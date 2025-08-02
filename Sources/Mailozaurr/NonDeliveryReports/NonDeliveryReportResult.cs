using Mailozaurr;

namespace Mailozaurr.NonDeliveryReports;

public sealed class NonDeliveryReportResult {
    public NonDeliveryReportResult(NonDeliveryReport report, SentMessageRecord? sentMessage) {
        Report = report;
        SentMessage = sentMessage;
    }

    public NonDeliveryReport Report { get; }

    public SentMessageRecord? SentMessage { get; }
}
