using System;
using Mailozaurr;

/// <summary>
/// Example demonstrating how to sign and encrypt email using PGP.
/// </summary>
public static class SendEmailPgp {
    /// <summary>Runs the example.</summary>
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
