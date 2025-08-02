using Mailozaurr.NonDeliveryReports;
using System;
using System.Collections.Generic;
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
        var headers = new Dictionary<string, string> {
            ["Original-Recipient"] = "rfc822; orig@example.com",
            ["Final-Recipient"] = "rfc822; final@example.com",
            ["Reporting-MTA"] = "dns; mx.example.com",
            ["Diagnostic-Code"] = "smtp; 550 5.1.1 User unknown",
            ["Status"] = "5.1.1",
            ["Arrival-Date"] = DateTimeOffset.UtcNow.ToString("R")
        };
        var report = NonDeliveryReport.FromHeaders(headers);
        Assert.Equal("rfc822; orig@example.com", report.OriginalRecipient);
        Assert.Equal("rfc822; final@example.com", report.FinalRecipient);
        Assert.Equal("dns; mx.example.com", report.ReportingMta);
        Assert.NotNull(report.DiagnosticCode);
        Assert.NotNull(report.Status);
        Assert.Equal(NonDeliveryReportType.UnknownRecipient, report.Type);
    }
}
