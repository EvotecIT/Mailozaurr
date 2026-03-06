using System.IO;
using System.Linq;
using System.Collections.Generic;
using MimeKit;
using Xunit;
using Mailozaurr.Definitions;

namespace Mailozaurr.Tests;

/// <summary>
/// Verifies handling of inline attachments for SMTP messages.
/// </summary>
public class SmtpInlineAttachmentTests
{
    [Fact]
    public void CreateMessage_WithInlineAttachment_AddsLinkedResource()
    {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, "data");
        var smtp = new Smtp();
        smtp.From = "a@b.com";
        smtp.To = new object[] { "c@d.com" };
        smtp.Subject = "test";
        smtp.HtmlBody = "<img src=\"cid:test\">";
        smtp.InlineAttachments = new List<AttachmentDescriptor> { new FileAttachmentDescriptor(tmp) };
        smtp.CreateMessage();
        var body = Assert.IsType<MultipartRelated>(smtp.Message.Body);
        var inlineCount = body.OfType<MimePart>().Count(p => p.ContentDisposition?.Disposition == ContentDisposition.Inline);
        File.Delete(tmp);
        Assert.Equal(1, inlineCount);
    }

    [Fact]
    public void CreateMessage_NullInlineAttachments_DoesNotThrow()
    {
        var smtp = new Smtp
        {
            From = "a@b.com",
            To = new object[] { "c@d.com" },
            Subject = "test",
            HtmlBody = "<b>body</b>",
            InlineAttachments = null
        };

        var ex = Record.Exception(() => smtp.CreateMessage());

        Assert.Null(ex);
    }

    [Fact]
    public void CreateMessage_MissingInlineAttachment_SkipsResource()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        if (File.Exists(path)) File.Delete(path);
        var smtp = new Smtp
        {
            From = "a@b.com",
            To = new object[] { "c@d.com" },
            Subject = "test",
            HtmlBody = "<img src=\"cid:test\">",
            InlineAttachments = new List<AttachmentDescriptor> { new FileAttachmentDescriptor(path) }
        };

        smtp.CreateMessage();

        Assert.IsType<TextPart>(smtp.Message.Body);
    }
}
