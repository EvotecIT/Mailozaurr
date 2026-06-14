using Mailozaurr.NonDeliveryReports;
using System;
using System.Collections.Generic;

/// <summary>
/// Demonstrates parsing of a Non-Delivery Report from DSN headers.
/// </summary>
public static class ParseNonDeliveryReportExample {
    /// <summary>Runs the example.</summary>
    public static void Run() {
        var headers = new Dictionary<string, string> {
            ["Original-Recipient"] = "rfc822; user@example.com",
            ["Final-Recipient"] = "rfc822; user@example.com",
            ["Reporting-MTA"] = "dns; mx.example.com",
            ["Diagnostic-Code"] = "smtp; 550 5.1.1 User unknown",
            ["Status"] = "5.1.1",
            ["Arrival-Date"] = DateTimeOffset.UtcNow.ToString("R"),
            ["Last-Attempt-Date"] = DateTimeOffset.UtcNow.ToString("R")
        };

        var ndr = NonDeliveryReport.FromHeaders(headers);
        Console.WriteLine($"NDR Type: {ndr.Type}, Last Attempt: {ndr.LastAttemptDate?.ToString() ?? "n/a"}, Status: {ndr.Status}");
    }
}