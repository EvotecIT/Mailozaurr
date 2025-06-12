using System.IO;
using System.Linq;
using System.Collections.Generic;
using MimeKit;
using Xunit;

namespace Mailozaurr.Tests;

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
        smtp.InlineAttachments = new List<object> { tmp };
        smtp.CreateMessage();
        var body = (MultipartRelated)smtp.Message.Body;
        var inlineCount = body.OfType<MimePart>().Count(p => p.ContentDisposition?.Disposition == ContentDisposition.Inline);
        File.Delete(tmp);
        Assert.Equal(1, inlineCount);
    }
}
