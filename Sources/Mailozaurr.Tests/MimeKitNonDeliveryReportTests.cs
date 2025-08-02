using Mailozaurr;
using Mailozaurr.NonDeliveryReports;
using MimeKit;
using System.IO;
using System.Text;
using Xunit;

namespace Mailozaurr.Tests;

public class MimeKitNonDeliveryReportTests {
    [Fact]
    public void GetNonDeliveryReport_ParsesMessage() {
        const string raw = "Content-Type: multipart/report; report-type=delivery-status; boundary=\"XXX\"\n\n--XXX\nContent-Type: text/plain; charset=utf-8\n\ntext\n\n--XXX\nContent-Type: message/delivery-status\n\nOriginal-Recipient: rfc822; user@example.com\nFinal-Recipient: rfc822; user@example.com\nReporting-MTA: dns; mx.example.com\nDiagnostic-Code: smtp; 550 5.1.1 User unknown\nStatus: 5.1.1\nArrival-Date: Wed, 24 Jul 2024 10:00:00 +0000\n\n--XXX--";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        var message = MimeMessage.Load(stream);
        NonDeliveryReport? report = MimeKitUtils.GetNonDeliveryReport(message);
        Assert.NotNull(report);
        Assert.Equal(NonDeliveryReportType.UnknownRecipient, report!.Type);
    }
}
