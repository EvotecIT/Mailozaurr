using System.Threading.Tasks;

class Program {
    static async Task Main(string[] args) {
        // Uncomment the example you want to run:

        // SendEmailGmail.Run();
        await SendEmailGraphClientSecret.RunAsync();
        // await SendEmailGraphCertificate.RunAsync();
        await SendEmailMailgun.RunAsync();
    }
}

// See individual example files for usage of Mailozaurr.
// Examples:
//   - SmtpGmailExample.cs
//   - GraphClientSecretExample.cs
//   - GraphCertificateExample.cs
