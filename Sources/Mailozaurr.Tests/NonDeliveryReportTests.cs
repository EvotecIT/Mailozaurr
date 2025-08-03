using Mailozaurr.NonDeliveryReports;
using System;
using System.Collections.Generic;
using MimeKit.Utils;
using Xunit;

namespace Mailozaurr.Tests;

public class NonDeliveryReportTests {
    [Theory]
    [InlineData("5.1.1", DsnStatusClass.PermanentFailure, NonDeliveryReportType.UnknownRecipient)]
    [InlineData("5.2.2", DsnStatusClass.PermanentFailure, NonDeliveryReportType.MailboxFull)]
    [InlineData("4.2.2", DsnStatusClass.PersistentTransientFailure, NonDeliveryReportType.MailboxFull)]
    [InlineData("4.1.0", DsnStatusClass.PersistentTransientFailure, NonDeliveryReportType.SoftBounce)]
    [InlineData("5.0.0", DsnStatusClass.PermanentFailure, NonDeliveryReportType.HardBounce)]
    public void ParseStatusAndMapType(string code, DsnStatusClass expectedClass, NonDeliveryReportType expectedType) {
        var status = DsnStatus.Parse(code);
        Assert.Equal(expectedClass, status.Class);
        var type = NonDeliveryReport.GetReportType(status);
        Assert.Equal(expectedType, type);
    }

    [Fact]
    public void ParseHeadersCreatesReport() {
        var lastAttempt = DateTimeOffset.UtcNow;
        var lastAttemptHeader = lastAttempt.ToString("R");
        var headers = new Dictionary<string, string> {
            ["Original-Recipient"] = "rfc822; orig@example.com",
            ["Final-Recipient"] = "rfc822; final@example.com",
            ["Reporting-MTA"] = "dns; mx.example.com",
            ["Action"] = "failed",
            ["Remote-MTA"] = "dns; remote.example.com",
            ["Last-Attempt-Date"] = lastAttemptHeader,
            ["Final-Log-ID"] = "ABC123",
            ["Diagnostic-Code"] = "smtp; 550 5.1.1 User unknown",
            ["Status"] = "5.1.1",
            ["Arrival-Date"] = DateTimeOffset.UtcNow.ToString("R")
        };
        var report = NonDeliveryReport.FromHeaders(headers);
        Assert.Equal("rfc822; orig@example.com", report.OriginalRecipient);
        Assert.Equal("rfc822; final@example.com", report.FinalRecipient);
        Assert.Equal("orig@example.com", report.OriginalRecipientAddress);
        Assert.Equal("final@example.com", report.FinalRecipientAddress);
        Assert.Equal("dns; mx.example.com", report.ReportingMta);
        Assert.Equal("failed", report.Action);
        Assert.Equal("dns; remote.example.com", report.RemoteMta);
        Assert.Equal(DateTimeOffset.Parse(lastAttemptHeader), report.LastAttemptDate);
        Assert.Equal("ABC123", report.FinalLogId);
        Assert.NotNull(report.DiagnosticCode);
        Assert.NotNull(report.Status);
        Assert.Equal(NonDeliveryReportType.UnknownRecipient, report.Type);
    }

    [Fact]
    public void ParsesOriginalMessageId() {
        var headers = new Dictionary<string, string> {
            ["Original-Message-ID"] = "<msg1@local>"
        };
        var report = NonDeliveryReport.FromHeaders(headers);
        Assert.Equal("<msg1@local>", report.OriginalMessageId);
    }

    [Theory]
    [InlineData("Fri, 21 Jun 2024 10:12:34 +0000")]
    [InlineData("Fri, 21 Jun 2024 10:12:34 +0000 (UTC)")]
    [InlineData("21 Jun 2024 10:12:34 -0700")]
    public void ParseTimestampHandlesVariousFormats(string dateHeader) {
        var headers = new Dictionary<string, string> {
            ["Arrival-Date"] = dateHeader,
            ["Last-Attempt-Date"] = dateHeader
        };
        var report = NonDeliveryReport.FromHeaders(headers);
        Assert.True(DateUtils.TryParse(dateHeader, out var expected));
        Assert.Equal(expected, report.Timestamp);
        Assert.Equal(expected, report.LastAttemptDate);
    }
}
