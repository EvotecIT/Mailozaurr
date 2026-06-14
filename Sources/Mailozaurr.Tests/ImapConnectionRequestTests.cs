using MailKit.Security;
using System;
using Xunit;

namespace Mailozaurr.Tests;

public class ImapConnectionRequestTests {
    [Fact]
    public void Constructor_SetsValues() {
        var request = new ImapConnectionRequest(
            "imap.example.test",
            993,
            SecureSocketOptions.StartTls,
            timeout: 1234,
            skipCertificateRevocation: true,
            skipCertificateValidation: true,
            retryCount: 7,
            retryDelayMilliseconds: 150,
            retryDelayBackoff: 1.5);

        Assert.Equal("imap.example.test", request.Server);
        Assert.Equal(993, request.Port);
        Assert.Equal(SecureSocketOptions.StartTls, request.Options);
        Assert.Equal(1234, request.Timeout);
        Assert.True(request.SkipCertificateRevocation);
        Assert.True(request.SkipCertificateValidation);
        Assert.Equal(7, request.RetryCount);
        Assert.Equal(150, request.RetryDelayMilliseconds);
        Assert.Equal(1.5, request.RetryDelayBackoff);
    }

    [Fact]
    public void Constructor_ThrowsForInvalidInput() {
        Assert.Throws<ArgumentException>(() => new ImapConnectionRequest("", 993));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImapConnectionRequest("imap.example.test", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImapConnectionRequest("imap.example.test", 993, timeout: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImapConnectionRequest("imap.example.test", 993, retryCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImapConnectionRequest("imap.example.test", 993, retryDelayMilliseconds: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImapConnectionRequest("imap.example.test", 993, retryDelayBackoff: 0));
    }
}