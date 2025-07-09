using System.Reflection;
using System.Collections.Generic;
using MimeKit;
using Xunit;

namespace Mailozaurr.Tests;

public class SesClientTests
{
    [Fact]
    public void BuildMessage_WithValidData_ReturnsMimeMessage()
    {
        using var client = new SesClient
        {
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Subject = "subject",
            Text = "text"
        };
        MethodInfo? method = typeof(SesClient).GetMethod("BuildMessage", BindingFlags.NonPublic | BindingFlags.Instance);
        var msg = method!.Invoke(client, null) as MimeMessage;
        Assert.NotNull(msg);
        Assert.Equal("subject", msg!.Subject);
        Assert.Contains("to@example.com", msg.To.ToString());
    }

    [Fact]
    public void BuildMessage_WithHeaders_AddsHeaders()
    {
        using var client = new SesClient
        {
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Subject = "subject",
            Headers = new Dictionary<string, string> { ["X-Test"] = "123" }
        };
        MethodInfo? method = typeof(SesClient).GetMethod("BuildMessage", BindingFlags.NonPublic | BindingFlags.Instance);
        var message = method!.Invoke(client, null) as MimeMessage;
        Assert.NotNull(message);
        Assert.Equal("123", message!.Headers["X-Test"]);
    }
}
