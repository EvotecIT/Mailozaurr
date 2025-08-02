using Mailozaurr;
using Mailozaurr.NonDeliveryReports;
using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Mailozaurr.Tests;

public class SearchNonDeliveryReportsTests {
    private static MimeMessage CreateNdr(string recipient, string messageId, DateTimeOffset date) {
        string raw = $"Content-Type: multipart/report; report-type=delivery-status; boundary=\"XXX\"\r\n\r\n--XXX\r\nContent-Type: text/plain; charset=utf-8\r\n\r\ntext\r\n\r\n--XXX\r\nContent-Type: message/delivery-status\r\n\r\nOriginal-Recipient: rfc822; {recipient}\r\nFinal-Recipient: rfc822; {recipient}\r\nOriginal-Message-ID: {messageId}\r\nReporting-MTA: dns; mx.example.com\r\nDiagnostic-Code: smtp; 550 5.1.1 User unknown\r\nStatus: 5.1.1\r\nArrival-Date: {date:R}\r\n\r\n--XXX--";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        return MimeMessage.Load(stream);
    }

    [Fact]
    public void FilterNonDeliveryReports_FiltersByRecipientAndMessageId() {
        var now = DateTimeOffset.UtcNow;
        var msg1 = CreateNdr("user@example.com", "<id1>", now);
        var msg2 = CreateNdr("other@example.com", "<id2>", now);
        var list = new List<MimeMessage> { msg1, msg2 };
        var reports = MailboxSearcher.FilterNonDeliveryReports(
            list,
            since: now.AddMinutes(-5).DateTime,
            before: now.AddMinutes(5).DateTime,
            recipientContains: "user@example.com",
            messageId: "<id1>");
        Assert.Single(reports);
        Assert.Equal("<id1>", reports[0].OriginalMessageId);
    }
}
