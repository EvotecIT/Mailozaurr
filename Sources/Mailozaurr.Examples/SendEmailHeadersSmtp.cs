using Mailozaurr;
using System;
using System.Collections.Generic;

public static class SendEmailHeadersSmtp {
    public static void Run() {
        var smtp = new Smtp();
        smtp.From = "sender@example.com";
        smtp.To = new[] { "recipient@example.com" };
        smtp.Subject = "SMTP Headers";
        smtp.TextBody = "Hello";
        smtp.Headers = new Dictionary<string, string> {
            ["X-Tracking-ID"] = "abc123",
            ["X-Source"] = "Mailozaurr"
        };
        smtp.Connect("smtp.example.com", 25);
        smtp.Send();
        smtp.Disconnect();
    }
}