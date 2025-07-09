using System.Threading.Tasks;

class Program {
    static async Task Main(string[] args) {
        // Uncomment the example you want to run:

        // SendEmailGmail.Run();
        // await AcquireGoogleTokenInteractive.RunAsync();
        // await AcquireO365TokenInteractive.RunAsync();
        await SendEmailGraphClientSecret.RunAsync();
        // await SendEmailGraphCertificate.RunAsync();
        await SendEmailMailgun.RunAsync();
        // await SendEmailSmtpAsync.RunAsync();
        // await FetchImapMessages.RunAsync();
        // await FetchPopMessages.RunAsync();
    }
}

// See individual example files for usage of Mailozaurr.
// Examples:
//   - SmtpGmailExample.cs
//   - GraphClientSecretExample.cs
//   - GraphCertificateExample.cs
//   - FetchImapMessages.cs
//   - FetchPopMessages.cs
//   - SendEmailGraphClientSecret.cs
//   - SendEmailGraphCertificate.cs
//   - AcquireGoogleTokenInteractive.cs
//   - AcquireO365TokenInteractive.cs
//   - SendEmailMailgun.cs
//   - SendEmailSmtpAsync.cs
