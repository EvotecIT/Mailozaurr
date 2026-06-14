using MimeKit;
using System;
using System.IO;
using Xunit;

namespace Mailozaurr.Tests;

/// <summary>
/// Tests creation of Graph API messages.
/// </summary>
public class GraphCreateMessageTests {
    [Fact]
    public void CreateMessage_WithValidData_BuildsJson() {
        using var graph = new Graph {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "subject",
            HTML = "body",
            ContentType = "HTML"
        };
        graph.CreateMessage();
        Assert.Contains("subject", graph.MessageJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("to@example.com", graph.MessageJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateMessage_WithHeaders_IncludesHeaders() {
        using var graph = new Graph {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "subject",
            HTML = "body",
            ContentType = "HTML",
            Headers = new System.Collections.Generic.Dictionary<string, string> { ["X-Test"] = "123" }
        };
        graph.CreateMessage();
        Assert.Contains("X-Test", graph.MessageJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GraphMimePreparation_IncludesOnlyCustomGraphHeaders() {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("from@example.com"));
        message.To.Add(MailboxAddress.Parse("to@example.com"));
        message.Subject = "subject";
        message.Body = new TextPart("plain") { Text = "body" };
        message.MessageId = "message@example.com";
        message.InReplyTo = "<previous@example.com>";
        message.References.Add("<root@example.com>");
        message.Headers.Add("X-Correlation-Id", "abc");
        message.Headers.Add("List-Unsubscribe", "<mailto:unsubscribe@example.com>");

        var graphMessage = GraphMimePreparation.ConvertToGraphMessage(
            message,
            idempotencyHeaderName: "X-Correlation-Id");

        Assert.NotNull(graphMessage.InternetMessageHeaders);
        var headers = graphMessage.InternetMessageHeaders!;
        var correlation = Assert.Single(headers, header => string.Equals(header.Name, "X-Correlation-Id", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("abc", correlation.Value);
        Assert.DoesNotContain(headers, header => string.Equals(header.Name, "Message-Id", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(headers, header => string.Equals(header.Name, "In-Reply-To", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(headers, header => string.Equals(header.Name, "References", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(headers, header => string.Equals(header.Name, "List-Unsubscribe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateMessage_WithoutExplicitContentType_DefaultsToHtml() {
        using var graph = new Graph {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "subject",
            HTML = "body"
        };

        graph.CreateMessage();

        Assert.Equal("HTML", graph.MessageContainer.Message.Body?.Type);
    }

    [Fact]
    public void ContentType_SetToInvalidValue_ThrowsArgumentException() {
        using var graph = new Graph();

        Assert.Throws<ArgumentException>(() => graph.ContentType = "Markdown");
    }

    [Fact]
    public void CreateAttachments_WithMissingFile_SkipsAttachment() {
        using var graph = new Graph {
            Attachments = new object[] { "missing.file" }
        };
        graph.CreateAttachments();

        Assert.Empty(graph.ConvertedAttachments);
    }

    [Fact]
    public void CreateMessage_WithLargeAttachment_DoesNotIncludeAttachment() {
        string tmp = Path.GetTempFileName();
        File.WriteAllBytes(tmp, new byte[4000001]);
        using var graph = new Graph {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "subject",
            HTML = "body",
            ContentType = "HTML",
            Attachments = new object[] { tmp }
        };
        graph.CreateAttachments();
        graph.CreateMessage();
        File.Delete(tmp);
        Assert.True(graph.IsLargerAttachment);
        Assert.Null(graph.MessageContainer.Message.Attachments);
    }

    [Fact]
    public void CreateMessage_WithoutFrom_ThrowsInvalidOperationException() {
        using var graph = new Graph {
            To = new object[] { "to@example.com" },
            Subject = "subject",
            HTML = "body",
            ContentType = "HTML"
        };

        Assert.Throws<InvalidOperationException>(() => graph.CreateMessage());
    }
}