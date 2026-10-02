#if NET8_0_OR_GREATER
using System.Net;
using System.Net.Http;

namespace Mailozaurr.Tests;

public sealed class GmailSummaryConcurrencyTests {
    [Fact]
    public async Task MissingSummary_IsSkippedButProviderFailurePropagates() {
        var handler = new SummaryFailureHandler { Status = HttpStatusCode.NotFound };
        using var http = new HttpClient(handler);
        using var gmail = new GmailApiClient(http);
        var browser = new GmailMailboxBrowser(gmail);
        Assert.Empty((await browser.ListMessagesAsync("INBOX", 1, 0)).Messages);
        handler.Status = HttpStatusCode.ServiceUnavailable;
        var error = await Assert.ThrowsAsync<GmailApiException>(() => browser.ListMessagesAsync("INBOX", 1, 0));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
    }

    [Fact]
    public async Task SummaryCancellation_PropagatesToCaller() {
        using var cancellation = new CancellationTokenSource();
        var handler = new SummaryFailureHandler { Cancellation = cancellation };
        using var http = new HttpClient(handler);
        using var gmail = new GmailApiClient(http);
        var browser = new GmailMailboxBrowser(gmail);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => browser.ListMessagesAsync("INBOX", 1, 0, cancellationToken: cancellation.Token));
    }

    private sealed class SummaryFailureHandler : HttpMessageHandler {
        internal HttpStatusCode Status;
        internal CancellationTokenSource? Cancellation;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (request.RequestUri!.AbsolutePath.EndsWith("/messages", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"messages\":[{\"id\":\"missing\"}],\"resultSizeEstimate\":1}") });
            if (Cancellation != null) {
                Cancellation.Cancel();
                return Task.FromCanceled<HttpResponseMessage>(cancellationToken);
            }
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent("{\"error\":\"summary failed\"}") });
        }
    }

    [Fact]
    public async Task ListMessages_LoadsSummariesConcurrentlyWithinLimitAndPreservesOrder() {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var handler = new SummaryHandler();
        using var http = new HttpClient(handler);
        using var gmail = new GmailApiClient(http);
        var browser = new GmailMailboxBrowser(gmail) { SummaryDownloadConcurrency = 2 };
        var result = await browser.ListMessagesAsync("INBOX", limit: 3, offset: 0, cancellationToken: timeout.Token);
        Assert.Equal(new[] { "m0", "m1", "m2" }, result.Messages.Select(message => message.NativeId));
        Assert.Equal(2, handler.MaximumActive);
        Assert.Equal(3, handler.SummaryRequests);
    }

    private sealed class SummaryHandler : HttpMessageHandler {
        private readonly TaskCompletionSource<bool> secondStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int active;
        internal int MaximumActive;
        internal int SummaryRequests;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var id = request.RequestUri!.AbsolutePath.Split('/').Last();
            if (id == "messages") {
                return Response("{\"messages\":[{\"id\":\"m0\"},{\"id\":\"m1\"},{\"id\":\"m2\"}],\"resultSizeEstimate\":3}");
            }
            var count = Interlocked.Increment(ref active);
            Interlocked.Increment(ref SummaryRequests);
            MaximumActive = Math.Max(MaximumActive, count);
            try {
                if (id == "m0") await secondStarted.Task.WaitAsync(cancellationToken);
                if (id == "m1") secondStarted.TrySetResult(true);
                return Response($"{{\"id\":\"{id}\",\"internalDate\":\"1739577600000\",\"payload\":{{\"headers\":[]}}}}");
            } finally {
                Interlocked.Decrement(ref active);
            }
        }

        private static HttpResponseMessage Response(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };
    }
}
#endif
