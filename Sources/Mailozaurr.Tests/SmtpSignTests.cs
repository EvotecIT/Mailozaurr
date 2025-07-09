using System;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpSignTests
{
    [Fact]
    public void Sign_MissingCertificate_ReturnsFailedResult()
    {
        var smtp = new Smtp();
        smtp.From = "a@b.com";
        smtp.To = new object[] { "c@d.com" };
        smtp.Subject = "test";
        smtp.TextBody = "body";
        smtp.CreateMessage();

        var result = smtp.Sign("0000000000000000000000000000000000000000");

        Assert.False(result.Status);
        Assert.Equal("Certificate not found in the store.", result.Error);
    }
}
