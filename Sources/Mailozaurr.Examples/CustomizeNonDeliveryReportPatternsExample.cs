using Mailozaurr;
using Mailozaurr.NonDeliveryReports;
using MimeKit;
using System;
using System.IO;
using System.Text;

/// <summary>
/// Demonstrates customizing Non-Delivery Report subject patterns.
/// </summary>
public static class CustomizeNonDeliveryReportPatternsExample {
    /// <summary>Runs the example.</summary>
    public static void Run() {
        NonDeliveryReportSubjectPatternProvider.Current.SubjectPatterns.Add("CustomPattern");
        const string raw = "Subject: CustomPattern\n\ntext";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        var message = MimeMessage.Load(stream);
        var reports = MimeKitUtils.GetNonDeliveryReports(message);
        Console.WriteLine($"Detected {reports.Count} report(s).");
    }
}

