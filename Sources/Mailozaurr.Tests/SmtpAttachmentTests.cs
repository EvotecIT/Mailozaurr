using System.Collections.Generic;
using System.IO;
using System.Text;
using MimeKit;
using Xunit;
using Mailozaurr.Definitions;

namespace Mailozaurr.Tests;

public class SmtpAttachmentTests
{
    [Fact]
    public void CreateMessage_MissingAttachment_SkipsAttachment()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        if (File.Exists(path)) File.Delete(path);
        var smtp = new Smtp
        {
            From = "a@b.com",
            To = new object[] { "c@d.com" },
            Subject = "test",
            TextBody = "body",
            Attachments = new List<AttachmentDescriptor> { new FileAttachmentDescriptor(path) }
        };

        smtp.CreateMessage();

        Assert.IsNotType<Multipart>(smtp.Message.Body);
}

    [Fact]
    public void CreateMessage_StreamAttachmentDescriptor_AddsAttachment()
    {
        var data = Encoding.UTF8.GetBytes("hello world");
        using var source = new MemoryStream(data);
        var descriptor = new StreamAttachmentDescriptor(source, "greeting.txt")
        {
            ContentType = "text/plain",
            Headers = new Dictionary<string, string> { { "X-Test", "Stream" } },
        };

        var smtp = new Smtp
        {
            From = "a@b.com",
            To = new object[] { "c@d.com" },
            Subject = "test",
            TextBody = "body",
            Attachments = new List<AttachmentDescriptor> { descriptor },
        };

        smtp.CreateMessage();

        var multipart = Assert.IsType<Multipart>(smtp.Message.Body);
        var part = Assert.Single(multipart.OfType<MimePart>(), p => p.IsAttachment);

        Assert.Equal("greeting.txt", part.FileName);
        Assert.Equal("text/plain", part.ContentType.MimeType);
        Assert.Equal("Stream", part.Headers["X-Test"]);

        using var extracted = new MemoryStream();
        var content = Assert.IsAssignableFrom<IMimeContent>(part.Content);
        content.DecodeTo(extracted);
        Assert.Equal(data, extracted.ToArray());

        Assert.True(source.CanRead);
    }

    [Fact]
    public void CreateMessage_ByteArrayAttachmentDescriptor_AddsAttachment()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var descriptor = new ByteArrayAttachmentDescriptor(data, "data.bin")
        {
            ContentType = "application/octet-stream",
        };

        var smtp = new Smtp
        {
            From = "a@b.com",
            To = new object[] { "c@d.com" },
            Subject = "test",
            TextBody = "body",
            Attachments = new List<AttachmentDescriptor> { descriptor },
        };

        smtp.CreateMessage();

        var multipart = Assert.IsType<Multipart>(smtp.Message.Body);
        var part = Assert.Single(multipart.OfType<MimePart>(), p => p.IsAttachment);

        Assert.Equal("data.bin", part.FileName);
        Assert.Equal("application/octet-stream", part.ContentType.MimeType);

        using var extracted = new MemoryStream();
        var content = Assert.IsAssignableFrom<IMimeContent>(part.Content);
        content.DecodeTo(extracted);
        Assert.Equal(data, extracted.ToArray());
    }
}
