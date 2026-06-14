using Mailozaurr.Definitions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using Xunit;

namespace Mailozaurr.Tests;

public class SendGridCreateMessageTests {
    [Fact]
    public void CreateMessage_WithValidData_BuildsJson() {
        var client = new SendGridClient {
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
    public void CreateMessage_InvalidAddress_ThrowsArgumentException() {
        var client = new SendGridClient {
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
    public void CreateMessage_WithHeaders_IncludesHeaders() {
        var client = new SendGridClient {
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
    public void CreateMessage_DuplicateAttachments_IncludedOnce() {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, "data");
        var client = new SendGridClient {
            From = "from@example.com",
            To = new List<object> { "to@example.com" },
            Subject = "subject",
            Text = "text",
            Credentials = new NetworkCredential("apikey", "test"),
            Attachments = new List<AttachmentDescriptor>
            {
                new FileAttachmentDescriptor(tmp),
                new FileAttachmentDescriptor(tmp)
            }
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

    [Theory]
    [InlineData("", "<b>body</b>", "text/html")]
    [InlineData("text", "", "text/plain")]
    public void CreateMessage_WithoutBody_OmitsCorrespondingContent(string text, string html, string expectedType) {
        var client = new SendGridClient {
            From = "from@example.com",
            To = new List<object> { "to@example.com" },
            Subject = "subject",
            Text = text,
            Html = html,
            Credentials = new NetworkCredential("apikey", "test")
        };
        client.CreateMessage();
        PropertyInfo? prop = typeof(SendGridClient).GetProperty("MessageJson", BindingFlags.NonPublic | BindingFlags.Instance);
        var json = prop?.GetValue(client) as string;
        Assert.NotNull(json);
        using var doc = System.Text.Json.JsonDocument.Parse(json!);
        var content = doc.RootElement.GetProperty("Content");
        Assert.Equal(1, content.GetArrayLength());
        Assert.Equal(expectedType, content[0].GetProperty("Type").GetString());
    }
}