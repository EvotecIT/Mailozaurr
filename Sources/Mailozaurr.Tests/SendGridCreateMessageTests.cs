using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using Xunit;

namespace Mailozaurr.Tests;

public class SendGridCreateMessageTests
{
    [Fact]
    public void CreateMessage_WithValidData_BuildsJson()
    {
        var client = new SendGridClient
        {
            From = "from@example.com",
            To = new List<object> { "to@example.com" },
            Subject = "subject",
            Text = "text",
            Html = "<b>body</b>",
            Credentials = new NetworkCredential("apikey", "test")
        };
        client.CreateMessage();
        PropertyInfo? prop = typeof(SendGridClient).GetProperty("MessageJson", BindingFlags.NonPublic | BindingFlags.Instance);
        var json = prop?.GetValue(client) as string;
        Assert.NotNull(json);
        Assert.Contains("subject", json!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("to@example.com", json!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateMessage_InvalidAddress_ThrowsArgumentException()
    {
        var client = new SendGridClient
        {
            From = "from@example.com",
            To = new List<object> { new Dictionary<string, object> { { "Name", "Test" } } },
            Subject = "subject",
            Text = "text",
            Html = "<b>body</b>",
            Credentials = new NetworkCredential("apikey", "test")
        };
        Assert.Throws<ArgumentException>(() => client.CreateMessage());
    }
}
