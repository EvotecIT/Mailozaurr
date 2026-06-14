using Mailozaurr;
using Mailozaurr.NonDeliveryReports;
using MimeKit;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace Mailozaurr.Tests;

public class MimeKitNonDeliveryReportTests {
    [Fact]
    public void GetNonDeliveryReports_ParsesSingleMessage() {
        const string raw = "Content-Type: multipart/report; report-type=delivery-status; boundary=\"XXX\"\n\n--XXX\nContent-Type: text/plain; charset=utf-8\n\ntext\n\n--XXX\nContent-Type: message/delivery-status\n\nOriginal-Recipient: rfc822; user@example.com\nFinal-Recipient: rfc822; user@example.com\nReporting-MTA: dns; mx.example.com\nDiagnostic-Code: smtp; 550 5.1.1 User unknown\nStatus: 5.1.1\nArrival-Date: Wed, 24 Jul 2024 10:00:00 +0000\n\n--XXX--";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        var message = MimeMessage.Load(stream);
        var reports = MimeKitUtils.GetNonDeliveryReports(message);
        Assert.Single(reports);
        Assert.Equal(NonDeliveryReportType.UnknownRecipient, reports[0].Type);
    }

    [Fact]
    public void GetNonDeliveryReports_ParsesMultipleReports() {
        const string raw = "Content-Type: multipart/report; report-type=delivery-status; boundary=\"XXX\"\n\n--XXX\nContent-Type: text/plain; charset=utf-8\n\ntext\n\n--XXX\nContent-Type: message/delivery-status\n\nOriginal-Recipient: rfc822; user1@example.com\nFinal-Recipient: rfc822; user1@example.com\nReporting-MTA: dns; mx.example.com\nDiagnostic-Code: smtp; 550 5.1.1 User unknown\nStatus: 5.1.1\nArrival-Date: Wed, 24 Jul 2024 10:00:00 +0000\n\nOriginal-Recipient: rfc822; user2@example.com\nFinal-Recipient: rfc822; user2@example.com\nReporting-MTA: dns; mx.example.com\nDiagnostic-Code: smtp; 550 5.2.2 Mailbox full\nStatus: 5.2.2\nArrival-Date: Wed, 24 Jul 2024 10:00:00 +0000\n\n--XXX--";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        var message = MimeMessage.Load(stream);
        var reports = MimeKitUtils.GetNonDeliveryReports(message);
        Assert.Equal(2, reports.Count);
        Assert.EndsWith("user1@example.com", reports[0].FinalRecipient);
        Assert.EndsWith("user2@example.com", reports[1].FinalRecipient);
    }

    [Fact]
    public void GetNonDeliveryReports_DetectsReportViaSubject() {
        const string raw = "Subject: Mail Delivery Subsystem\n\ntext";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        var message = MimeMessage.Load(stream);
        var reports = MimeKitUtils.GetNonDeliveryReports(message);
        Assert.Single(reports);
    }

    [Fact]
    public void GetNonDeliveryReports_SubjectReportUsesMessageDate() {
        var date = new DateTimeOffset(2024, 7, 24, 10, 0, 0, TimeSpan.Zero);
        string raw = $"Date: {date:R}\r\nSubject: Mail Delivery Subsystem\r\n\r\ntext";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        var message = MimeMessage.Load(stream);
        var reports = MimeKitUtils.GetNonDeliveryReports(message);
        Assert.Single(reports);
        Assert.Equal(date, reports[0].Timestamp);
    }

    [Fact]
    public void GetNonDeliveryReports_IgnoresDuplicateHeaders() {
        const string raw = "Content-Type: multipart/report; report-type=delivery-status; boundary=\"XXX\"\n\n--XXX\nContent-Type: text/plain; charset=utf-8\n\ntext\n\n--XXX\nContent-Type: message/delivery-status\n\nFinal-Recipient: rfc822; user@example.com\nFinal-Recipient: rfc822; other@example.com\nStatus: 5.1.1\n\n--XXX--";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        var message = MimeMessage.Load(stream);
        var reports = MimeKitUtils.GetNonDeliveryReports(message);
        Assert.Single(reports);
        Assert.EndsWith("user@example.com", reports[0].FinalRecipient);
        Assert.DoesNotContain("other@example.com", reports[0].FinalRecipient, StringComparison.OrdinalIgnoreCase);
    }
}