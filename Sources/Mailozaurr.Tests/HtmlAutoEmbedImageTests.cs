using System.IO;
using System.Linq;
using MimeKit;
using Xunit;

namespace Mailozaurr.Tests;

public class HtmlAutoEmbedImageTests {
    [Fact]
    public void Smtp_CreateMessage_AutoEmbedsImages() {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, "data");
        var smtp = new Smtp { AutoEmbedImages = true };
        smtp.From = "a@b.com";
        smtp.To = new object[] { "c@d.com" };
        smtp.Subject = "test";
        smtp.HtmlBody = $"<img src=\"{tmp}\">";
        smtp.CreateMessage();
        var body = (MultipartRelated)smtp.Message.Body;
        var inline = body.OfType<MimePart>().FirstOrDefault(p => p.ContentDisposition?.Disposition == ContentDisposition.Inline);
        File.Delete(tmp);
        Assert.NotNull(inline);
        Assert.Contains("cid:" + Path.GetFileName(tmp), smtp.HtmlBody);
    }

    [Fact]
    public void Graph_CreateMessage_AutoEmbedsImages() {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, "data");
        using var graph = new Graph {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "subject",
            HTML = $"<img src=\"{tmp}\">",
            ContentType = "HTML",
            AutoEmbedImages = true
        };
        graph.CreateMessage();
        File.Delete(tmp);
        var attachment = Assert.Single(graph.MessageContainer.Message.Attachments);
        Assert.True(attachment.IsInline);
        Assert.Contains("cid:" + attachment.ContentId, graph.HTML);
    }
}
