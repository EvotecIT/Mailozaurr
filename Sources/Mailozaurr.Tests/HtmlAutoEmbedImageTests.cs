using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
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

    [Fact]
    public void Smtp_CreateMessage_EmbedsRemoteImages() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) {
                Headers = { ContentType = new MediaTypeHeaderValue("image/png") }
            }
        });
        var property = typeof(HtmlUtils).GetProperty("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = new HttpClient(handler);
        var original = (HttpClient)property.GetValue(null)!;
        property.SetValue(null, client);
        try {
            var smtp = new Smtp { AutoEmbedRemoteImages = true };
            smtp.From = "a@b.com";
            smtp.To = new object[] { "c@d.com" };
            smtp.Subject = "test";
            smtp.HtmlBody = "<img src=\"https://example.com/img.png\">";
            smtp.CreateMessage();
            var body = (MultipartRelated)smtp.Message.Body!;
            var inline = body.OfType<MimePart>().FirstOrDefault(p => p.ContentDisposition?.Disposition == ContentDisposition.Inline);
            Assert.NotNull(inline);
            Assert.Contains("cid:img.png", smtp.HtmlBody);
            Assert.Single(handler.Requests);
        } finally {
            property.SetValue(null, original);
        }
    }
}
