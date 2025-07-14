using Mailozaurr;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Mailozaurr.Tests;

public class TemporarySmimeCertificateTests
{
    [Fact]
    public void CreateSelfSigned_ReturnsUsableCertificate()
    {
        // Skip test on macOS due to certificate compatibility issues
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        using X509Certificate2 cert = TemporarySmimeCertificate.CreateSelfSigned();
        Assert.True(cert.HasPrivateKey);
        Assert.NotNull(cert.Subject);
    }

    [Fact]
    public void Certificate_CanSignAndEncryptMessage()
    {
        // Skip test on macOS due to certificate compatibility issues
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        using X509Certificate2 cert = TemporarySmimeCertificate.CreateSelfSigned();
        var smtp = new Smtp();
        smtp.From = "a@b.com";
        smtp.To = new object[] { "c@d.com" };
        smtp.Subject = "test";
        smtp.TextBody = "body";
        smtp.CreateMessage();
        var signResult = smtp.Sign(cert);
        Assert.True(signResult.Status);
        smtp.CreateMessage();
        var encryptResult = smtp.Encrypt(cert);
        Assert.True(encryptResult.Status);
    }
}
