using Mailozaurr;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Mailozaurr.Tests;

public class TemporarySmimeCertificateTests {
    [Fact]
    public void CreateSelfSigned_PreservesLegacyThreeParameterSignature() {
        MethodInfo? method = typeof(TemporarySmimeCertificate).GetMethod(
            nameof(TemporarySmimeCertificate.CreateSelfSigned),
            new[] { typeof(string), typeof(int), typeof(string) });

        Assert.NotNull(method);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;
        using X509Certificate2 certificate = TemporarySmimeCertificate.CreateSelfSigned(
            "CN=Legacy Mailozaurr Test",
            1,
            null);
        Assert.True(certificate.HasPrivateKey);
    }

    [Fact]
    public void CreateSelfSigned_RequiresAPasswordForPfxExport() {
        Assert.Throws<ArgumentException>(() => TemporarySmimeCertificate.CreateSelfSigned(outputPath: "certificate.pfx"));
    }

    [Fact]
    public void CreateSelfSigned_ExportsAPasswordProtectedPfxWithoutOverwriting() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;
        string directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "certificate.pfx");
        const string password = "correct horse battery staple";
        try {
            using X509Certificate2 certificate = TemporarySmimeCertificate.CreateSelfSigned(
                outputPath: path,
                outputPassword: password);
            using var loaded = new X509Certificate2(path, password);

            Assert.True(loaded.HasPrivateKey);
            Assert.Throws<IOException>(() => TemporarySmimeCertificate.CreateSelfSigned(
                outputPath: path,
                outputPassword: password));
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CreateSelfSigned_ReturnsUsableCertificate() {
        // Skip test on macOS due to certificate compatibility issues
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            return;
        }

        using X509Certificate2 cert = TemporarySmimeCertificate.CreateSelfSigned();
        Assert.True(cert.HasPrivateKey);
        Assert.NotNull(cert.Subject);
    }

    [Fact]
    public void Certificate_CanSignAndEncryptMessage() {
        // Skip test on macOS due to certificate compatibility issues
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
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

    [Fact]
    public void Certificate_CanVerifySmimeSignature() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
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
        Assert.True(MimeKitUtils.VerifySmimeSignature(smtp.Message, cert));
    }

    [Fact]
    public void Certificate_CanDecryptSmimeMessage() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            return;
        }

        using X509Certificate2 cert = TemporarySmimeCertificate.CreateSelfSigned();
        var smtp = new Smtp();
        smtp.From = "a@b.com";
        smtp.To = new object[] { "c@d.com" };
        smtp.Subject = "test";
        smtp.TextBody = "body";
        smtp.CreateMessage();

        var encryptResult = smtp.Encrypt(cert);
        var decrypted = MimeKitUtils.DecryptSmime(smtp.Message, cert);

        Assert.True(encryptResult.Status);
        Assert.Equal("body", decrypted.TextBody);
    }
}
