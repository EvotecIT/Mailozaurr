using System.Net;
using System.Net.Http;

namespace Mailozaurr.Tests;

public sealed class GmailListingFailureTests {
    [Theory]
    [InlineData((HttpStatusCode)429)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task ListPage_PropagatesProviderFailureInsteadOfEmptyMailbox(HttpStatusCode status) {
        using var http = new HttpClient(new FailedListingHandler(status));
        using var gmail = new GmailApiClient(http);
        await Assert.ThrowsAsync<HttpRequestException>(() => gmail.ListPageAsync("me"));
    }

    private sealed class FailedListingHandler(HttpStatusCode status) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{\"error\":{\"message\":\"listing unavailable\"}}") });
    }
}
