using MailKit.Security;
using Mailozaurr;

namespace Mailozaurr.Tests;

public sealed class Pop3ConnectionRequestTests {
    [Fact]
    public void ConstructorSetsValidatedValues() {
        var request = new Pop3ConnectionRequest(
            "pop.example.test",
            995,
            SecureSocketOptions.SslOnConnect,
            timeout: 1234,
            skipCertificateRevocation: true,
            skipCertificateValidation: true,
            retryCount: 7,
            retryDelayMilliseconds: 150,
            retryDelayBackoff: 1.5);

        Assert.Equal("pop.example.test", request.Server);
        Assert.Equal(995, request.Port);
        Assert.Equal(SecureSocketOptions.SslOnConnect, request.Options);
        Assert.Equal(1234, request.Timeout);
        Assert.True(request.SkipCertificateRevocation);
        Assert.True(request.SkipCertificateValidation);
        Assert.Equal(7, request.RetryCount);
        Assert.Equal(150, request.RetryDelayMilliseconds);
        Assert.Equal(1.5, request.RetryDelayBackoff);
    }

    [Fact]
    public void ConstructorRejectsInvalidConnectionSettings() {
        Assert.Throws<ArgumentException>(() => new Pop3ConnectionRequest("", 995));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pop3ConnectionRequest("pop.example.test", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pop3ConnectionRequest("pop.example.test", 65536));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pop3ConnectionRequest("pop.example.test", timeout: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pop3ConnectionRequest("pop.example.test", retryCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pop3ConnectionRequest("pop.example.test", retryDelayMilliseconds: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pop3ConnectionRequest("pop.example.test", retryDelayBackoff: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pop3ConnectionRequest("pop.example.test", retryDelayBackoff: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pop3ConnectionRequest("pop.example.test", retryDelayBackoff: double.PositiveInfinity));
    }
}
