using System;
using Mailozaurr;

public static class SendEmailPgp {
    public static void Run() {
        var smtp = new Smtp();
        smtp.From = "mimekit@example.com";
        smtp.To = new[] { "mimekit@example.com" };
        smtp.Subject = "PGP Test";
        smtp.TextBody = "Hello";
        smtp.Connect("smtp.example.com", 25);
        smtp.CreateMessage();
        smtp.PgpSignAndEncrypt("Examples/PGPKeys/mimekit.gpg.pub", "Examples/PGPKeys/mimekit.gpg.sec", "no.secret", false);
        smtp.Send();
        smtp.Disconnect();
    }
}
