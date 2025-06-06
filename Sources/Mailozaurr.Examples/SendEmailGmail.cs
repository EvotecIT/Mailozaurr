using Mailozaurr;
using MailKit.Security;
using System;

public static class SendEmailGmail {
    public static void Run() {
        // === CONFIGURATION ===
        string gmailAddress = "youraddress@gmail.com";
        string gmailAppPassword = "your_app_password"; // Use Gmail App Password
        string recipient = "recipient@example.com";

        // === EXAMPLE ===
        try {
            var smtp = new Smtp();
            smtp.From = gmailAddress;
            smtp.To = new[] { recipient };
            smtp.Subject = "Test Email via Gmail SMTP";
            smtp.HtmlBody = "<b>Hello from Mailozaurr via Gmail SMTP!</b>";
            var connectResult = smtp.Connect("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
            if (!connectResult.Status)
                throw new Exception($"SMTP Connect failed: {connectResult.Error}");
            var authResult = smtp.Authenticate(gmailAddress, gmailAppPassword, false);
            if (!authResult.Status)
                throw new Exception($"SMTP Auth failed: {authResult.Error}");
            var sendResult = smtp.Send();
            Console.WriteLine(sendResult.Status ? "Gmail SMTP: Email sent!" : $"Gmail SMTP: Failed: {sendResult.Error}");
            smtp.Disconnect();
        } catch (Exception ex) {
            Console.WriteLine($"Gmail SMTP Example Error: {ex.Message}");
        }
    }
}