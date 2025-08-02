using MailKit;
using Mailozaurr;
using Mailozaurr.NonDeliveryReports;
using MimeKit;
using System.IO;
using System.Text;
using Xunit;

namespace Mailozaurr.Tests;

public class NonDeliveryReportDetectionTests {
    private static MimeMessage CreateNdrMessage() {
        const string raw = "Content-Type: multipart/report; report-type=delivery-status; boundary=\"XXX\"\n\n--XXX\nContent-Type: text/plain; charset=utf-8\n\ntext\n\n--XXX\nContent-Type: message/delivery-status\n\nOriginal-Recipient: rfc822; orig@example.com\nFinal-Recipient: rfc822; final@example.com\nReporting-MTA: dns; mx.example.com\nDiagnostic-Code: smtp; 550 5.1.1 User unknown\nStatus: 5.1.1\nArrival-Date: Wed, 24 Jul 2024 10:00:00 +0000\n\n--XXX--";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        return MimeMessage.Load(stream);
    }

    [Fact]
    public void ImapEmailMessageDetectsNdr() {
        var msg = CreateNdrMessage();
        var imap = new ImapEmailMessage(new UniqueId(1), msg);
        Assert.NotNull(imap.NonDeliveryReport);
        Assert.Equal(NonDeliveryReportType.UnknownRecipient, imap.NonDeliveryReport!.Type);
    }

    [Fact]
    public void Pop3EmailMessageDetectsNdr() {
        var msg = CreateNdrMessage();
        var pop3 = new Pop3EmailMessage(0, msg);
        Assert.NotNull(pop3.NonDeliveryReport);
        Assert.Equal(NonDeliveryReportType.UnknownRecipient, pop3.NonDeliveryReport!.Type);
    }

    [Fact]
    public void GraphEmailMessageDetectsNdr() {
        var msg = CreateNdrMessage();
        var graph = new GraphEmailMessage("id", msg);
        Assert.NotNull(graph.NonDeliveryReport);
        Assert.Equal(NonDeliveryReportType.UnknownRecipient, graph.NonDeliveryReport!.Type);
    }
}