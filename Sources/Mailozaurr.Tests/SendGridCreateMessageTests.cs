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
    public void CreateMessage_WithHighPriority_IncludesProviderPriorityHeaders() {
        using var client = new SendGridClient {
            From = "from@example.com",
            To = new List<object> { "to@example.com" },
            Subject = "subject",
            Text = "text",
            Credentials = new NetworkCredential("apikey", "test"),
            Priority = MessagePriority.High
        };

        client.CreateMessage();

        PropertyInfo? prop = typeof(SendGridClient).GetProperty("MessageJson", BindingFlags.NonPublic | BindingFlags.Instance);
        var json = Assert.IsType<string>(prop?.GetValue(client));
        Assert.Contains("\"X-Priority\":\"1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Importance\":\"high\"", json, StringComparison.Ordinal);
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

    [Fact]
    public void CreateMessage_RelativeAndAbsoluteAttachmentPaths_IncludedOnce() {
        var fileName = $"mailozaurr-sendgrid-{Guid.NewGuid():N}.tmp";
        var absolutePath = Path.Combine(Environment.CurrentDirectory, fileName);
        File.WriteAllText(absolutePath, "data");
        using var client = new SendGridClient {
            From = "from@example.com",
            To = new List<object> { "to@example.com" },
            Subject = "subject",
            Text = "text",
            Credentials = new NetworkCredential("apikey", "test"),
            Attachments = new List<AttachmentDescriptor> {
                new FileAttachmentDescriptor(fileName),
                new FileAttachmentDescriptor(absolutePath),
            },
        };

        try {
            client.CreateMessage();
            PropertyInfo? property = typeof(SendGridClient).GetProperty("MessageJson", BindingFlags.NonPublic | BindingFlags.Instance);
            var json = Assert.IsType<string>(property?.GetValue(client));
            using var document = System.Text.Json.JsonDocument.Parse(json);

            Assert.Single(document.RootElement.GetProperty("Attachments").EnumerateArray());
        } finally {
            File.Delete(absolutePath);
        }
    }

    [Fact]
    public void CreateMessage_InlineFileWithoutContentId_UsesFileName() {
        var tmp = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        File.WriteAllBytes(tmp, new byte[] { 1, 2, 3 });
        var descriptor = new FileAttachmentDescriptor(tmp);
        using var client = new SendGridClient {
            From = "from@example.com",
            To = new List<object> { "to@example.com" },
            Subject = "subject",
            Html = $"<img src=\"cid:{Path.GetFileName(tmp)}\">",
            Credentials = new NetworkCredential("apikey", "test"),
            InlineAttachments = new List<AttachmentDescriptor> { descriptor }
        };

        try {
            client.CreateMessage();
            PropertyInfo? prop = typeof(SendGridClient).GetProperty("MessageJson", BindingFlags.NonPublic | BindingFlags.Instance);
            var json = Assert.IsType<string>(prop?.GetValue(client));
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var attachment = Assert.Single(doc.RootElement.GetProperty("Attachments").EnumerateArray());
            Assert.Equal("inline", attachment.GetProperty("Disposition").GetString());
            Assert.Equal(Path.GetFileName(tmp), attachment.GetProperty("ContentId").GetString());
            Assert.Null(descriptor.ContentDisposition);

            using var regularClient = new SendGridClient {
                From = "from@example.com",
                To = new List<object> { "to@example.com" },
                Subject = "subject",
                Text = "body",
                Credentials = new NetworkCredential("apikey", "test"),
                Attachments = new List<AttachmentDescriptor> { descriptor }
            };
            regularClient.CreateMessage();
            var regularJson = Assert.IsType<string>(prop?.GetValue(regularClient));
            using var regularDoc = System.Text.Json.JsonDocument.Parse(regularJson);
            var regularAttachment = Assert.Single(regularDoc.RootElement.GetProperty("Attachments").EnumerateArray());
            Assert.Equal("attachment", regularAttachment.GetProperty("Disposition").GetString());
        } finally {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void CreateMessage_SameFileInRegularAndInlineRoles_PreservesBothDispositions() {
        var tmp = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        File.WriteAllBytes(tmp, new byte[] { 1, 2, 3 });
        using var client = new SendGridClient {
            From = "from@example.com",
            To = new List<object> { "to@example.com" },
            Subject = "subject",
            Html = $"<img src=\"cid:{Path.GetFileName(tmp)}\">",
            Credentials = new NetworkCredential("apikey", "test"),
            Attachments = new List<AttachmentDescriptor> { new FileAttachmentDescriptor(tmp) },
            InlineAttachments = new List<AttachmentDescriptor> { new FileAttachmentDescriptor(tmp) }
        };

        try {
            client.CreateMessage();
            PropertyInfo? prop = typeof(SendGridClient).GetProperty("MessageJson", BindingFlags.NonPublic | BindingFlags.Instance);
            var json = Assert.IsType<string>(prop?.GetValue(client));
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var attachments = doc.RootElement.GetProperty("Attachments");

            Assert.Equal(2, attachments.GetArrayLength());
            Assert.Equal("attachment", attachments[0].GetProperty("Disposition").GetString());
            Assert.Equal("inline", attachments[1].GetProperty("Disposition").GetString());
            Assert.Equal(Path.GetFileName(tmp), attachments[1].GetProperty("ContentId").GetString());
        } finally {
            File.Delete(tmp);
        }
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
