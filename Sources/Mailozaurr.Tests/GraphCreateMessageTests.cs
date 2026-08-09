using MimeKit;
using Mailozaurr.Definitions;
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
    public void GraphMimePreparation_MapsMimePriorityToImportance() {
        var message = new MimeMessage {
            Subject = "priority",
            Body = new TextPart("plain") { Text = "body" },
            Priority = MimeKit.MessagePriority.Urgent
        };
        message.From.Add(MailboxAddress.Parse("from@example.com"));
        message.To.Add(MailboxAddress.Parse("to@example.com"));

        var graphMessage = GraphMimePreparation.ConvertToGraphMessage(message);

        Assert.Equal("high", graphMessage.Importance);
    }

    [Fact]
    public void GraphMimePreparation_RoutesAttachmentWhenSerializedDraftWouldExceedLimit() {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("from@example.com"));
        message.To.Add(MailboxAddress.Parse("to@example.com"));
        message.Subject = "serialized routing";
        var builder = new BodyBuilder { TextBody = new string('x', 1_000_000) };
        builder.Attachments.Add("small.bin", new byte[2_300_000]);
        message.Body = builder.ToMessageBody();

        var prepared = GraphMimePreparation.PrepareMessage(message);
        try {
            Assert.Null(prepared.Message.Attachments);
            var attachment = Assert.Single(prepared.UploadAttachments);
            Assert.Equal(2_300_000, attachment.Length);
        } finally {
            foreach (var attachment in prepared.UploadAttachments) {
                attachment.Dispose();
            }
        }
    }

    [Fact]
    public void GraphMimePreparation_PreservesSmallLinkedInlineResource() {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("from@example.com"));
        message.To.Add(MailboxAddress.Parse("to@example.com"));
        var builder = new BodyBuilder { HtmlBody = "<img src=\"cid:logo-image\">" };
        var resource = builder.LinkedResources.Add("logo.png", new byte[] { 1, 2, 3, 4 });
        resource.ContentId = "logo-image";
        message.Body = builder.ToMessageBody();

        var prepared = GraphMimePreparation.PrepareMessage(message);

        var attachment = Assert.Single(prepared.Message.Attachments!);
        Assert.True(attachment.IsInline);
        Assert.Equal("logo-image", attachment.ContentId);
        Assert.Equal(Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }), attachment.ContentBytes);
        Assert.Empty(prepared.UploadAttachments);
    }

    [Fact]
    public void GraphMimePreparation_RoutesLargeLinkedInlineResourceWithCidMetadata() {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("from@example.com"));
        message.To.Add(MailboxAddress.Parse("to@example.com"));
        var builder = new BodyBuilder { HtmlBody = "<img src=\"cid:large-inline\">" };
        var resource = builder.LinkedResources.Add("large.png", new byte[3_200_000]);
        resource.ContentId = "large-inline";
        message.Body = builder.ToMessageBody();

        var prepared = GraphMimePreparation.PrepareMessage(message);
        try {
            Assert.Null(prepared.Message.Attachments);
            var attachment = Assert.Single(prepared.UploadAttachments);
            Assert.True(attachment.IsInline);
            Assert.Equal("large-inline", attachment.ContentId);
            Assert.Equal(3_200_000, attachment.Length);
        } finally {
            foreach (var attachment in prepared.UploadAttachments) {
                attachment.Dispose();
            }
        }
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
    public void ApplyBodyToSmtpFallback_PreservesPlainTextContentType() {
        using var graph = new Graph {
            HTML = "Use <literal> & text",
            ContentType = "Text"
        };
        var smtp = new Smtp();

        graph.ApplyBodyToSmtpFallback(smtp);

        Assert.Equal("Use <literal> & text", smtp.TextBody);
        Assert.Empty(smtp.HtmlBody);
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
    public void CreateAttachments_SmallFileDescriptor_PreservesMetadata() {
        string tmp = Path.GetTempFileName();
        File.WriteAllBytes(tmp, new byte[] { 1, 2, 3 });
        using var graph = new Graph {
            Attachments = new object[] {
                new FileAttachmentDescriptor(tmp) {
                    FileName = "renamed.bin",
                    ContentType = "application/x-workflow"
                }
            }
        };

        try {
            graph.CreateAttachments();
        } finally {
            File.Delete(tmp);
        }

        var attachment = Assert.Single(graph.ConvertedAttachments);
        Assert.Equal("renamed.bin", attachment.Name);
        Assert.Equal("application/x-workflow", attachment.ContentType);
    }

    [Fact]
    public void CreateSmtpFallbackAttachment_PreservesExplicitContentType() {
        var graphAttachment = new GraphAttachment {
            Name = "workflow",
            ContentType = "application/x-workflow",
            ContentBytes = Convert.ToBase64String(new byte[] { 1, 2, 3 })
        };

        var descriptor = Graph.CreateSmtpFallbackAttachment(graphAttachment);

        Assert.Equal("workflow", descriptor.FileName);
        Assert.Equal("application/x-workflow", descriptor.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, descriptor.GetContentBytes());
    }

    [Fact]
    public void AddFileAttachmentSourcesToSmtpFallback_PreservesInlineDisposition() {
        string tmp = Path.GetTempFileName();
        var inlineDescriptor = new FileAttachmentDescriptor(tmp) {
            ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
            ContentId = "workflow-image"
        };
        using var graph = new Graph {
            Attachments = new object[] { inlineDescriptor }
        };
        var smtp = new Smtp();

        try {
            graph.AddFileAttachmentSourcesToSmtpFallback(smtp);
        } finally {
            File.Delete(tmp);
        }

        Assert.Empty(smtp.Attachments!);
        Assert.Same(inlineDescriptor, Assert.Single(smtp.InlineAttachments!));
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
    public void CreateAttachments_RelativeAndAbsoluteAliases_AreIncludedOnce() {
        var fileName = $"mailozaurr-graph-{Guid.NewGuid():N}.tmp";
        var absolutePath = Path.Combine(Environment.CurrentDirectory, fileName);
        File.WriteAllBytes(absolutePath, new byte[] { 1, 2, 3 });
        using var graph = new Graph {
            Attachments = new object[] { fileName, absolutePath }
        };

        try {
            graph.CreateAttachments();
        } finally {
            File.Delete(absolutePath);
        }

        Assert.Single(graph.ConvertedAttachments);
        Assert.True(graph.TotalAttachmentSizeBytes > 3);
        Assert.False(graph.IsLargerAttachment);
    }

    [Fact]
    public void CreateAttachments_SameFileAcrossRegularAndInlineRoles_PreservesBoth() {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        var regular = new FileAttachmentDescriptor(path);
        var inline = new FileAttachmentDescriptor(path) {
            ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
            ContentId = "shared-inline"
        };
        using var graph = new Graph {
            Attachments = new object[] { regular, inline }
        };

        try {
            graph.CreateAttachments();
        } finally {
            File.Delete(path);
        }

        Assert.Equal(2, graph.ConvertedAttachments.Count);
        Assert.Contains(graph.ConvertedAttachments, attachment => !attachment.IsInline);
        Assert.Contains(graph.ConvertedAttachments, attachment => attachment.IsInline && attachment.ContentId == "shared-inline");
        Assert.True(graph.TotalAttachmentSizeBytes > 6);
        Assert.False(graph.IsLargerAttachment);
    }

    [Fact]
    public void CreateAttachments_RoutesBase64ExpandedFileThroughUploadSession() {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[3_100_000]);
        using var graph = new Graph { Attachments = new object[] { path } };

        try {
            graph.CreateAttachments();
        } finally {
            File.Delete(path);
        }

        Assert.True(graph.TotalAttachmentSizeBytes > 4_000_000);
        Assert.Equal(3_100_000, graph.RawAttachmentSizeBytes);
        Assert.True(graph.IsLargerAttachment);
        Assert.Empty(graph.ConvertedAttachments);
    }

    [Fact]
    public void CreateAttachments_RoutesBase64ExpandedInMemoryAttachmentAwayFromSimpleSend() {
        using var graph = new Graph {
            Attachments = new object[] {
                new GraphAttachment {
                    Name = "expanded.bin",
                    ContentBytes = Convert.ToBase64String(new byte[3_100_000])
                }
            }
        };

        graph.CreateAttachments();

        Assert.True(graph.TotalAttachmentSizeBytes > 4_000_000);
        Assert.Equal(3_100_000, graph.RawAttachmentSizeBytes);
        Assert.True(graph.IsLargerAttachment);
    }

    [Fact]
    public void CreateMessage_RoutesFileWhenCompleteSerializedRequestExceedsLimit() {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[2_300_000]);
        using var graph = new Graph {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "complete payload",
            HTML = new string('x', 1_000_000),
            ContentType = "HTML",
            Attachments = new object[] { path }
        };

        try {
            graph.CreateMessage();
        } finally {
            File.Delete(path);
        }

        Assert.True(graph.IsLargerAttachment);
        Assert.Null(graph.MessageContainer.Message.Attachments);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(graph.MessageJson) < 4_000_000);
    }

    [Fact]
    public void CreateMessage_RejectsCompleteSerializedRequestThatCannotUseFileUpload() {
        using var graph = new Graph {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "oversized body",
            HTML = new string('x', 4_000_000),
            ContentType = "HTML"
        };

        var exception = Assert.Throws<InvalidOperationException>(() => graph.CreateMessage());

        Assert.Contains("complete serialized Graph request", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddFileAttachmentSourcesToSmtpFallback_SameFileAcrossRoles_PreservesBoth() {
        var path = Path.GetTempFileName();
        var regular = new FileAttachmentDescriptor(path);
        var inline = new FileAttachmentDescriptor(path) {
            ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
            ContentId = "shared-inline"
        };
        using var graph = new Graph {
            Attachments = new object[] { regular, inline }
        };
        var smtp = new Smtp();

        try {
            graph.AddFileAttachmentSourcesToSmtpFallback(smtp);
        } finally {
            File.Delete(path);
        }

        Assert.Same(regular, Assert.Single(smtp.Attachments!));
        Assert.Same(inline, Assert.Single(smtp.InlineAttachments!));
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
