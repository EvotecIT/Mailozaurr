using MimeKit;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
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
        smtp.CreateMessage(CancellationToken.None);
        var message = smtp.Message;
        Assert.NotNull(message);
        var body = Assert.IsType<MultipartRelated>(message!.Body!);
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
        var message = graph.MessageContainer?.Message;
        Assert.NotNull(message);
        var attachment = Assert.Single(message!.Attachments!);
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
        var client = HtmlUtils.HttpClient;
        var handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic);
        var original = (HttpMessageHandler)handlerField!.GetValue(client)!;
        handlerField.SetValue(client, handler);
        try {
            var smtp = new Smtp { AutoEmbedRemoteImages = true };
            smtp.From = "a@b.com";
            smtp.To = new object[] { "c@d.com" };
            smtp.Subject = "test";
            smtp.HtmlBody = "<img src=\"https://example.com/img.png\">";
            smtp.CreateMessage(CancellationToken.None);
            var message = smtp.Message;
            Assert.NotNull(message);
            var body = Assert.IsType<MultipartRelated>(message!.Body!);
            var inline = body.OfType<MimePart>().FirstOrDefault(p => p.ContentDisposition?.Disposition == ContentDisposition.Inline);
            Assert.NotNull(inline);
            Assert.Contains("cid:img.png", smtp.HtmlBody);
            Assert.Single(handler.Requests);
        } finally {
            handlerField.SetValue(client, original);
        }
    }
}