using MailKit.Security;
using Mailozaurr;

public static class SendEmailSasl {
    public static void Run() {
        var smtp = new Smtp();
        smtp.From = "sender@example.com";
        smtp.To = new[] { "recipient@example.com" };
        smtp.Subject = "Test with CRAM-MD5";
        smtp.HtmlBody = "<b>Hello</b>";
        smtp.Connect("smtp.example.com", 587, SecureSocketOptions.StartTls);
        smtp.Authenticate("user", "pass", false, AuthenticationMechanism.CramMd5);
        smtp.Send();
        smtp.Disconnect();
    }
}
