using Mailozaurr;
using MimeKit;
using System;
using System.IO;
using System.Text;

/// <summary>
/// Demonstrates detecting a Non-Delivery Report from a MIME message.
/// </summary>
public static class DetectNonDeliveryReportExample {
    /// <summary>Runs the example.</summary>
    public static void Run() {
        const string raw = "Content-Type: multipart/report; report-type=delivery-status; boundary=\"XXXX\"\n\n--XXXX\nContent-Type: text/plain; charset=utf-8\n\nThis is the mail system at example.com\n\n--XXXX\nContent-Type: message/delivery-status\n\nOriginal-Recipient: rfc822; orig@example.com\nFinal-Recipient: rfc822; final@example.com\nReporting-MTA: dns; mx.example.com\nDiagnostic-Code: smtp; 550 5.1.1 User unknown\nStatus: 5.1.1\nArrival-Date: Wed, 24 Jul 2024 10:00:00 +0000\n\n--XXXX--";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        var message = MimeMessage.Load(stream);
        var report = MimeKitUtils.GetNonDeliveryReport(message);
        if (report != null) {
            Console.WriteLine($"Detected NDR: {report.Type} with status {report.Status}");
        } else {
            Console.WriteLine("No NDR detected.");
        }
    }
}