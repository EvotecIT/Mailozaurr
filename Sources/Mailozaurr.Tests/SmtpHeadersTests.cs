using System.Collections.Generic;
using Xunit;
using MimeKit;

namespace Mailozaurr.Tests;

public class SmtpHeadersTests
{
    [Fact]
    public void CreateMessage_WithHeaders_AddsHeaders()
    {
        var smtp = new Smtp();
        smtp.From = "a@b.com";
        smtp.To = new object[] { "c@d.com" };
        smtp.Subject = "test";
        smtp.TextBody = "body";
        smtp.Headers = new Dictionary<string, string> { ["X-Test"] = "123" };
        smtp.CreateMessage();
        Assert.Equal("123", smtp.Message.Headers["X-Test"]);
    }

    [Fact]
    public void CreateMessage_WithMultipleHeaders_AddsAllHeaders()
    {
        var smtp = new Smtp
        {
            From = "a@b.com",
            To = new object[] { "c@d.com" },
            Subject = "test",
            TextBody = "body",
            Headers = new Dictionary<string, string>
            {
                ["X-One"] = "1",
                ["X-Two"] = "2"
            }
        };

        smtp.CreateMessage();

        Assert.Equal("1", smtp.Message.Headers["X-One"]);
        Assert.Equal("2", smtp.Message.Headers["X-Two"]);
    }
}
