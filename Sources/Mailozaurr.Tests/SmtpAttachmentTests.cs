using System.Collections.Generic;
using System.IO;
using MimeKit;
using Xunit;

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
            Attachments = new List<object> { path }
        };

        smtp.CreateMessage();

        Assert.IsNotType<Multipart>(smtp.Message.Body);
    }
}
