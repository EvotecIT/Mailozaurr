using System.Net;
using System.Net.Http;
using System.Reflection;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphApiClientTests {
    [Theory]
    [InlineData(HttpStatusCode.Accepted)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async System.Threading.Tasks.Task DirectMimeSendPostsOnceToSendMailAndPreservesAttachments(HttpStatusCode status) {
        using var handler = new RecordingHandler(new HttpResponseMessage(status));
        using var http = new HttpClient(handler);
        using var client = new GraphApiClient(http, credential: new OAuthCredential { AccessToken = "synthetic" });
        using var mime = new MimeKit.MimeMessage { Subject = "Weekly report" };
        mime.From.Add(MimeKit.MailboxAddress.Parse("sender@example.test"));
        mime.To.Add(MimeKit.MailboxAddress.Parse("recipient@example.test"));
        mime.Bcc.Add(MimeKit.MailboxAddress.Parse("audit@example.test"));
        var body = new MimeKit.BodyBuilder { TextBody = "Report", HtmlBody = "<p>Report</p>" };
        body.Attachments.Add("report.xlsx", new byte[] { 1, 2, 3 });
        mime.Body = body.ToMessageBody();

        Assert.Equal(status, await client.SendMimeMessageAsync(mime, "sender@example.test", CancellationToken.None));

        using HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/users/sender%40example.test/sendMail", request.RequestUri!.AbsoluteUri);
        Assert.Equal("text/plain", request.Content!.Headers.ContentType!.MediaType);
        using var payload = new MemoryStream(Convert.FromBase64String(await request.Content.ReadAsStringAsync()));
        using var received = MimeKit.MimeMessage.Load(payload);
        Assert.Equal("Report", received.TextBody);
        Assert.Equal("<p>Report</p>", received.HtmlBody);
        Assert.Equal("audit@example.test", Assert.Single(received.Bcc.Mailboxes).Address);
        Assert.Equal("report.xlsx", Assert.IsType<MimeKit.MimePart>(Assert.Single(received.Attachments)).FileName);
    }

    [Fact]
    public async System.Threading.Tasks.Task CreateSubscriptionAsync_NullRequest_Throws() {
        var client = new GraphApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.CreateSubscriptionAsync(null!));
    }

    [Theory]
    [InlineData("", "created", "https://example.com", "resource")]
    [InlineData("resource", "", "https://example.com", "changeType")]
    [InlineData("resource", "created", "", "notificationUrl")]
    public async System.Threading.Tasks.Task CreateSubscriptionAsync_MissingRequired_Throws(string resource, string changeType, string notificationUrl, string _) {
        var client = new GraphApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var req = new GraphApiClient.GraphCreateSubscriptionRequest {
            Resource = resource,
            ChangeType = changeType,
            NotificationUrl = notificationUrl,
            ExpirationDateTime = System.DateTimeOffset.UtcNow.AddHours(1)
        };
        await Assert.ThrowsAsync<ArgumentException>(() => client.CreateSubscriptionAsync(req));
    }

    [Fact]
    public async System.Threading.Tasks.Task CreateSubscriptionAsync_SendsPostAndParses() {
        var json = "{\"id\":\"s1\",\"resource\":\"me/messages\",\"changeType\":\"created\",\"notificationUrl\":\"https://example.com\",\"expirationDateTime\":\"2026-02-15T00:00:00Z\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GraphApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GraphApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new HttpClient(handler) { BaseAddress = new System.Uri("https://graph.microsoft.com/v1.0/") });

        var req = new GraphApiClient.GraphCreateSubscriptionRequest {
            Resource = "me/messages",
            ChangeType = "created",
            NotificationUrl = "https://example.com",
            ExpirationDateTime = System.DateTimeOffset.Parse("2026-02-15T00:00:00Z")
        };
        var sub = await client.CreateSubscriptionAsync(req);
        Assert.Equal("s1", sub.Id);
        Assert.Equal("me/messages", sub.Resource);

        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/subscriptions", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"resource\":\"me/messages\"", body);
        Assert.Contains("\"changeType\":\"created\"", body);
        Assert.Contains("\"notificationUrl\":\"https://example.com\"", body);
    }

    [Fact]
    public async System.Threading.Tasks.Task CreateSubscriptionAsync_RejectsMissingRemoteId() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new System.Net.Http.StringContent("{\"resource\":\"me/messages\"}")
        });
        var client = new GraphApiClient(
            new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GraphApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new HttpClient(handler) { BaseAddress = new System.Uri("https://graph.microsoft.com/v1.0/") });

        await Assert.ThrowsAsync<InvalidDataException>(() => client.CreateSubscriptionAsync(
            new GraphApiClient.GraphCreateSubscriptionRequest {
                Resource = "me/messages",
                ChangeType = "created",
                NotificationUrl = "https://example.com",
                ExpirationDateTime = System.DateTimeOffset.UtcNow.AddHours(1)
            }));
    }

    [Fact]
    public async System.Threading.Tasks.Task RenewSubscriptionAsync_EncodesSubscriptionId_UsesPatch() {
        var json = "{\"id\":\"a b\",\"resource\":\"me/messages\",\"changeType\":\"created\",\"notificationUrl\":\"https://example.com\",\"expirationDateTime\":\"2026-02-15T00:00:00Z\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GraphApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GraphApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new HttpClient(handler) { BaseAddress = new System.Uri("https://graph.microsoft.com/v1.0/") });

        await client.RenewSubscriptionAsync("a b", System.DateTimeOffset.Parse("2026-02-15T00:00:00Z"));
        Assert.Single(handler.Requests);
        Assert.Equal("PATCH", handler.Requests[0].Method.Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/subscriptions/a%20b", handler.Requests[0].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async System.Threading.Tasks.Task RenewSubscriptionAsync_RejectsMissingRemoteId() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new System.Net.Http.StringContent("{\"resource\":\"me/messages\"}")
        });
        var client = new GraphApiClient(
            new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GraphApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new HttpClient(handler) { BaseAddress = new System.Uri("https://graph.microsoft.com/v1.0/") });

        await Assert.ThrowsAsync<InvalidDataException>(() => client.RenewSubscriptionAsync(
            "sub-1",
            System.DateTimeOffset.UtcNow.AddHours(1)));
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteSubscriptionAsync_EncodesSubscriptionId() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("{}") });
        var client = new GraphApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GraphApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new HttpClient(handler) { BaseAddress = new System.Uri("https://graph.microsoft.com/v1.0/") });

        await client.DeleteSubscriptionAsync("a b");
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/subscriptions/a%20b", handler.Requests[0].RequestUri!.AbsoluteUri);
    }
}
