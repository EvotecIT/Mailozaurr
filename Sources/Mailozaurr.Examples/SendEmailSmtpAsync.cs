using MailKit.Security;
using Mailozaurr;
using System;
using System.Net;
using System.Threading.Tasks;

/// <summary>
/// Example demonstrating asynchronous SMTP usage.
/// </summary>
public static class SendEmailSmtpAsync {
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync() {
        var smtp = new Smtp();
        smtp.From = "sender@example.com";
        smtp.To = new[] { "recipient@example.com" };
        smtp.Subject = "Async SMTP";
        smtp.TextBody = "Hello from async SMTP";
        var connectResult = await smtp.ConnectAsync("smtp.example.com", 25);
        if (!connectResult.Status)
            throw new Exception($"SMTP Connect failed: {connectResult.Error}");
        var authResult = await smtp.AuthenticateAsync(new NetworkCredential("user", "pass"));
        if (!authResult.Status)
            throw new Exception($"SMTP Auth failed: {authResult.Error}");
        var sendResult = await smtp.SendAsync();
        Console.WriteLine(sendResult.Status ? "SMTP async: Email sent!" : $"SMTP async: Failed: {sendResult.Error}");
        smtp.Disconnect();
    }
}
