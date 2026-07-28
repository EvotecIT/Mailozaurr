using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public sealed class SendGridSendEmailAsyncTests {
    [Fact]
    public async Task SendEmailAsync_PermanentFailureDoesNotRetry() {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.BadRequest) {
                Content = new StringContent("invalid")
            });
        using var client = CreateClient(handler);
        client.RetryCount = 3;

        var result = await client.SendEmailAsync();

        Assert.False(result.Status);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SendEmailAsync_ErrorActionStopThrowsProviderFailure() {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.Forbidden) {
                Content = new StringContent("forbidden")
            });
        using var client = CreateClient(handler);
        client.RetryCount = 3;
        client.ErrorAction = ActionPreference.Stop;

        var exception = await Record.ExceptionAsync(() => client.SendEmailAsync());

        var httpException = Assert.IsAssignableFrom<HttpRequestException>(exception);
        Assert.Contains("Forbidden", httpException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SendEmailAsync_ReturnsNativeMessageId() {
        var response = new HttpResponseMessage(HttpStatusCode.Accepted);
        response.Headers.Add("X-Message-Id", "sendgrid-123");
        var handler = new RecordingHandler(response);
        using var client = CreateClient(handler);

        var result = await client.SendEmailAsync();

        Assert.True(result.Status);
        Assert.Equal("sendgrid-123", result.MessageId);
    }

    [Fact]
    public async Task SendEmailAsync_SharedRequestDeadlineCancelsStalledRequest() {
        using var client = CreateClient(new BlockingHandler());
        client.RequestTimeout = TimeSpan.FromMilliseconds(50);

        var result = await client.SendEmailAsync();

        Assert.False(result.Status);
        Assert.True(client.Stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    private static SendGridClient CreateClient(HttpMessageHandler handler) {
        var client = new SendGridClient(handler) {
            From = "sender@example.com",
            To = new List<object> { "recipient@example.com" },
            Credentials = new NetworkCredential(string.Empty, "api-key"),
            Subject = "subject",
            Text = "body"
        };
        client.CreateMessage();
        return client;
    }

    private sealed class BlockingHandler : HttpMessageHandler {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }
}
