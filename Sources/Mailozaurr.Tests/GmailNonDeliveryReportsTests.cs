using Mailozaurr;
using MimeKit;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class GmailNonDeliveryReportsTests {
    private static MimeMessage CreateNdr(string recipient, string messageId, DateTimeOffset date) {
        string raw = $"Content-Type: multipart/report; report-type=delivery-status; boundary=\"XXX\"\r\n\r\n--XXX\r\nContent-Type: text/plain; charset=utf-8\r\n\r\ntext\r\n\r\n--XXX\r\nContent-Type: message/delivery-status\r\n\r\nOriginal-Recipient: rfc822; {recipient}\r\nFinal-Recipient: rfc822; {recipient}\r\nOriginal-Message-ID: {messageId}\r\nReporting-MTA: dns; mx.example.com\r\nDiagnostic-Code: smtp; 550 5.1.1 User unknown\r\nStatus: 5.1.1\r\nArrival-Date: {date:R}\r\n\r\n--XXX--";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        return MimeMessage.Load(stream);
    }

    private static string Encode(MimeMessage message) {
        using var ms = new MemoryStream();
        message.WriteTo(ms);
        return Convert.ToBase64String(ms.ToArray()).Replace('+', '-').Replace('/', '_').Replace("=", string.Empty);
    }

    [Fact]
    public async Task SearchNonDeliveryReportsAsync_GmailApi_ReturnsReports() {
        var now = DateTimeOffset.UtcNow;
        var ndr = CreateNdr("user@example.com", "<id1>", now);
        ndr.Subject = "Undeliverable: Delivery has failed";
        var normal = new MimeMessage();
        normal.Subject = "hello";
        var listJson = "{\"messages\":[{\"id\":\"1\"},{\"id\":\"2\"}]}";
        var msg1Json = $"{{\"raw\":\"{Encode(ndr)}\"}}";
        var msg2Json = $"{{\"raw\":\"{Encode(normal)}\"}}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(listJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(msg1Json) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(msg2Json) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new HttpClient(handler) { BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/") });
        var reports = await MailboxSearcher.SearchNonDeliveryReportsAsync(client, "me", parallelDownloadLimit: 1, cancellationToken: CancellationToken.None);
        Assert.NotEmpty(handler.Requests);
        var listRequest = handler.Requests[0];
        var uri = listRequest.RequestUri!;
        string? queryParam = null;
        var query = uri.Query.TrimStart('?').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in query) {
            var kvp = part.Split(new[] { '=' }, 2);
            if (kvp.Length == 2 && kvp[0] == "q") {
                queryParam = Uri.UnescapeDataString(kvp[1]);
                break;
            }
        }
        Assert.NotNull(queryParam);
        Assert.Contains("subject:\"Undeliverable:\"", queryParam, StringComparison.Ordinal);
        Assert.Single(reports);
        Assert.Equal("id1", reports[0].OriginalMessageId);
    }
}

