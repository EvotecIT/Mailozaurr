using System;
using System.Collections.Generic;
using System.IO;
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

    [Fact]
    public void CreateMessage_WithHeaders_IncludesHeaders()
    {
        var client = new SendGridClient
        {
            From = "from@example.com",
            To = new List<object> { "to@example.com" },
            Subject = "subject",
            Text = "text",
            Credentials = new NetworkCredential("apikey", "test"),
            Headers = new Dictionary<string, string> { ["X-Test"] = "123" }
        };
        client.CreateMessage();
        PropertyInfo? prop = typeof(SendGridClient).GetProperty("MessageJson", BindingFlags.NonPublic | BindingFlags.Instance);
        var json = prop?.GetValue(client) as string;
        Assert.NotNull(json);
        Assert.Contains("X-Test", json!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateMessage_DuplicateAttachments_IncludedOnce()
    {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, "data");
        var client = new SendGridClient
        {
            From = "from@example.com",
            To = new List<object> { "to@example.com" },
            Subject = "subject",
            Text = "text",
            Credentials = new NetworkCredential("apikey", "test"),
            Attachment = new object[] { tmp, tmp }
        };
        client.CreateMessage();
        PropertyInfo? prop = typeof(SendGridClient).GetProperty("MessageJson", BindingFlags.NonPublic | BindingFlags.Instance);
        var json = prop?.GetValue(client) as string;
        File.Delete(tmp);
        Assert.NotNull(json);
        using var doc = System.Text.Json.JsonDocument.Parse(json!);
        var count = doc.RootElement.GetProperty("Attachments").GetArrayLength();
        Assert.Equal(1, count);
    }

    [Fact]
    public void CreateMessage_SeparateTo_CreatesPersonalizationsPerRecipient()
    {
        var client = new SendGridClient
        {
            From = "from@example.com",
            To = new List<object> { "to1@example.com", "to2@example.com", "to1@example.com" },
            Cc = new List<object> { "cc@example.com" },
            Bcc = new List<object> { "bcc@example.com" },
            Subject = "subject",
            Text = "text",
            SeparateTo = true,
            Credentials = new NetworkCredential("apikey", "test"),
        };

        client.CreateMessage();
        PropertyInfo? prop = typeof(SendGridClient).GetProperty("MessageJson", BindingFlags.NonPublic | BindingFlags.Instance);
        var json = prop?.GetValue(client) as string;
        Assert.NotNull(json);
        using var doc = System.Text.Json.JsonDocument.Parse(json!);
        var pers = doc.RootElement.GetProperty("Personalizations");
        Assert.Equal(2, pers.GetArrayLength());
        foreach (var p in pers.EnumerateArray())
        {
            Assert.Equal(1, p.GetProperty("To").GetArrayLength());
            Assert.Equal(1, p.GetProperty("Cc").GetArrayLength());
            Assert.Equal(1, p.GetProperty("Bcc").GetArrayLength());
        }
    }
}
