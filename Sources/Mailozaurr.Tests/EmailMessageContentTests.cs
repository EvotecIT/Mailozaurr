using Mailozaurr.Definitions;
using MimeKit;

namespace Mailozaurr.Tests;

public class EmailMessageContentTests {
    [Fact]
    public void FromRenderResult_MapsBodiesAndMimeResourcesWithoutRendererDependency() {
        var source = new FakeRenderResult {
            Html = "<p>Open tickets: <img src=\"cid:ticket-chart\"></p>",
            PlainText = "Open tickets",
            Subject = "Service report",
            InlineResources = new[] {
                new FakeInlineResource("ticket-chart", "image/png", new byte[] { 1, 2, 3 }, "tickets.png")
            },
            Attachments = new[] {
                new FakeAttachment("tickets.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", new byte[] { 4, 5 })
            },
            Headers = new Dictionary<string, string> { ["X-Workflow"] = "daily-report" }
        };

        var content = EmailMessageContent.FromRenderResult(source);

        Assert.Equal(source.Html, content.HtmlBody);
        Assert.Equal(source.PlainText, content.TextBody);
        Assert.Equal(source.Subject, content.Subject);
        var inline = Assert.IsType<ByteArrayAttachmentDescriptor>(Assert.Single(content.InlineAttachments));
        Assert.Equal("ticket-chart", inline.ContentId);
        Assert.Equal("tickets.png", inline.FileName);
        Assert.Equal("image/png", inline.ContentType);
        Assert.Equal(ContentDisposition.Inline, inline.ContentDisposition?.Disposition);
        var attachment = Assert.IsType<ByteArrayAttachmentDescriptor>(Assert.Single(content.Attachments));
        Assert.Equal("tickets.xlsx", attachment.FileName);
        Assert.Equal("daily-report", content.Headers["X-Workflow"]);
    }

    [Fact]
    public void GraphAttachment_FromDescriptor_PreservesInlineMetadataAndBytes() {
        var descriptor = new ByteArrayAttachmentDescriptor(new byte[] { 10, 20, 30 }, "chart.svg") {
            ContentType = "image/svg+xml",
            ContentId = "chart-1",
            ContentDisposition = new ContentDisposition(ContentDisposition.Inline)
        };

        var attachment = GraphAttachment.FromDescriptor(descriptor);

        Assert.True(attachment.IsInline);
        Assert.Equal("chart-1", attachment.ContentId);
        Assert.Equal("image/svg+xml", attachment.ContentType);
        Assert.Equal(Convert.ToBase64String(new byte[] { 10, 20, 30 }), attachment.ContentBytes);
    }

    [Fact]
    public void WithContent_AppliesTheSameRenderedContractToSmtpAndGraph() {
        var content = new EmailMessageContent {
            Subject = "Service report",
            HtmlBody = "<p>Open tickets</p>",
            TextBody = "Open tickets"
        };
        content.Headers["X-Workflow"] = "daily-report";
        content.Attachments.Add(new ByteArrayAttachmentDescriptor(new byte[] { 1 }, "report.xlsx"));
        content.InlineAttachments.Add(new ByteArrayAttachmentDescriptor(new byte[] { 2 }, "chart.png") {
            ContentType = "image/png",
            ContentId = "chart"
        });

        var smtp = new Smtp().WithContent(content);
        using var graph = new Graph().WithContent(content);

        Assert.Equal(content.Subject, smtp.Subject);
        Assert.Equal(content.HtmlBody, smtp.HtmlBody);
        Assert.Single(smtp.Attachments!);
        Assert.Single(smtp.InlineAttachments!);
        Assert.Equal("daily-report", smtp.Headers!["X-Workflow"]);

        Assert.Equal(content.Subject, graph.Subject);
        Assert.Equal(content.HtmlBody, graph.HTML);
        Assert.Equal("HTML", graph.ContentType);
        Assert.Equal(2, graph.Attachments!.Length);
        var inline = Assert.IsType<GraphAttachment>(graph.Attachments[1]);
        Assert.True(inline.IsInline);
        Assert.Equal("chart", inline.ContentId);
        Assert.Equal("daily-report", graph.Headers!["X-Workflow"]);
    }

    [Fact]
    public void WithContent_RoutesSendGridInlineResourcesWithoutMutatingTheSourceDescriptor() {
        var descriptor = new ByteArrayAttachmentDescriptor(new byte[] { 1, 2 }, "chart.png") {
            ContentType = "image/png",
            ContentId = "chart"
        };
        var content = new EmailMessageContent();
        content.InlineAttachments.Add(descriptor);

        using var sendGrid = new SendGridClient().WithContent(content);

        Assert.Null(sendGrid.Attachments);
        var mapped = Assert.Single(sendGrid.InlineAttachments!);
        Assert.Same(descriptor, mapped);
        Assert.Null(descriptor.ContentDisposition);
    }

    [Fact]
    public async Task WithContent_PreservesFileBackedInlineResourceForGraphUploadSession() {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[4_100_000]);
        var descriptor = new FileAttachmentDescriptor(path) {
            FileName = "dashboard.png",
            ContentType = "image/png",
            ContentId = "dashboard-image"
        };
        var content = new EmailMessageContent();
        content.InlineAttachments.Add(descriptor);
        using var graph = new Graph().WithContent(content);

        try {
            var preparedDescriptor = Assert.IsType<FileAttachmentDescriptor>(Assert.Single(graph.Attachments!));
            Assert.NotSame(descriptor, preparedDescriptor);
            Assert.Equal(ContentDisposition.Inline, preparedDescriptor.ContentDisposition?.Disposition);
            Assert.Null(descriptor.ContentDisposition);

            graph.CreateAttachments();
            await graph.PrepareAttachments();

            Assert.True(graph.IsLargerAttachment);
            var placeholder = Assert.Single(graph.AttachmentsPlaceHolders);
            Assert.Contains("\"isInline\":true", placeholder.Json, StringComparison.Ordinal);
            Assert.Contains("\"contentId\":\"dashboard-image\"", placeholder.Json, StringComparison.Ordinal);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void WithContent_ReplacesLegacyMailgunAndSesAttachmentPaths() {
        var content = new EmailMessageContent();
        content.Attachments.Add(new ByteArrayAttachmentDescriptor(new byte[] { 1 }, "current.txt"));
        content.InlineAttachments.Add(new ByteArrayAttachmentDescriptor(new byte[] { 2 }, "current.png"));

        using var mailgun = new MailgunClient {
            Attachment = new[] { "stale.txt" },
            InlineAttachment = new[] { "stale.png" }
        }.WithContent(content);
        using var ses = new SesClient {
            Attachment = new[] { "stale.txt" },
            InlineAttachment = new[] { "stale.png" }
        }.WithContent(content);

        Assert.Null(mailgun.Attachment);
        Assert.Null(mailgun.InlineAttachment);
        Assert.Single(mailgun.Attachments!);
        Assert.Single(mailgun.InlineAttachments!);
        Assert.Null(ses.Attachment);
        Assert.Null(ses.InlineAttachment);
        Assert.Single(ses.Attachments!);
        Assert.Single(ses.InlineAttachments!);
    }

    private sealed class FakeRenderResult {
        public string Html { get; init; } = string.Empty;
        public string PlainText { get; init; } = string.Empty;
        public string Subject { get; init; } = string.Empty;
        public FakeInlineResource[] InlineResources { get; init; } = Array.Empty<FakeInlineResource>();
        public FakeAttachment[] Attachments { get; init; } = Array.Empty<FakeAttachment>();
        public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    }

    private sealed record FakeInlineResource(string ContentId, string MimeType, byte[] Data, string? FileName);
    private sealed record FakeAttachment(string FileName, string MimeType, byte[] Data);
}
