using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
#if NETFRAMEWORK
using Xunit;
namespace Mailozaurr.Tests;
public class SmtpSmimeSignTests
{
    [Fact(Skip = "Self-signed certificate generation not supported on .NET Framework.")]
    public void Sign_ChangesMessageBodyAndReturnsSuccess() { }

    [Fact(Skip = "Self-signed certificate generation not supported on .NET Framework.")]
    public void Pkcs7Sign_ChangesMessageBodyAndReturnsSuccess() { }
}
#else
using MimeKit;
using MimeKit.Cryptography;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpSmimeSignTests
{
    private static X509Certificate2 CreateSelfSignedCert()
    {
        using RSA rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return new X509Certificate2(cert.Export(X509ContentType.Pfx));
    }

    [Fact]
    public void Sign_ChangesMessageBodyAndReturnsSuccess()
    {
        using var cert = CreateSelfSignedCert();
        var smtp = new Smtp();
        smtp.From = "a@b.com";
        smtp.To = new object[] { "c@d.com" };
        smtp.Subject = "test";
        smtp.TextBody = "body";
        smtp.CreateMessage();
        var originalType = smtp.Message.Body.GetType();

        var result = smtp.Sign(cert);

        Assert.True(result.Status);
        Assert.NotEqual(originalType, smtp.Message.Body.GetType());
        Assert.IsType<MultipartSigned>(smtp.Message.Body);
    }

    [Fact]
    public void Pkcs7Sign_ChangesMessageBodyAndReturnsSuccess()
    {
        using var cert = CreateSelfSignedCert();
        var smtp = new Smtp();
        smtp.From = "a@b.com";
        smtp.To = new object[] { "c@d.com" };
        smtp.Subject = "test";
        smtp.TextBody = "body";
        smtp.CreateMessage();
        var originalType = smtp.Message.Body.GetType();

        var result = smtp.Pkcs7Sign(cert);

        Assert.True(result.Status);
        Assert.NotEqual(originalType, smtp.Message.Body.GetType());
        Assert.IsType<ApplicationPkcs7Mime>(smtp.Message.Body);
    }
}
#endif
