using MimeKit;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace Mailozaurr.Tests;

public class SendGridAttachmentTests {
    [Fact]
    public void Constructor_FromFilePath_ReadsFileAndSetsMetadata() {
        var path = Path.GetTempFileName();
        try {
            File.WriteAllText(path, "hello");
            var attachment = new SendGridAttachment(path);

            Assert.Equal(Path.GetFileName(path), attachment.Filename);
            Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("hello")), attachment.Content);
            Assert.Equal(MimeTypes.GetMimeType(path), attachment.Type);
            Assert.Equal("attachment", attachment.Disposition);
            Assert.Null(attachment.ContentId);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void Constructor_FromBytes_UsesProvidedMetadata() {
        var data = Encoding.UTF8.GetBytes("inline");
        var attachment = new SendGridAttachment("inline.txt", data, "text/plain", "inline", "cid123");

        Assert.Equal("inline.txt", attachment.Filename);
        Assert.Equal(Convert.ToBase64String(data), attachment.Content);
        Assert.Equal("text/plain", attachment.Type);
        Assert.Equal("inline", attachment.Disposition);
        Assert.Equal("cid123", attachment.ContentId);
    }
}