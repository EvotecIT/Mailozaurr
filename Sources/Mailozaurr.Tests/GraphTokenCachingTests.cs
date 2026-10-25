using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace Mailozaurr.Tests;

public class GraphTokenCachingTests {
    [Fact]
    public async Task Connect_ThrottledTokenRequest_RecreatesContentForRetry() {
        HttpContent? previous = null;
        int requests = 0;
        using var handler = new TokenHandler(async (request, _) => {
            Assert.NotSame(previous, request.Content);
            Assert.Contains("client_secret=secret", await request.Content!.ReadAsStringAsync());
            previous = request.Content;
            if (++requests == 1) {
                // .NET Framework disposes sent content. Model that lifetime on every target.
                request.Content.Dispose();
                var throttled = new HttpResponseMessage((HttpStatusCode)429);
                throttled.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                return throttled;
            }
            return Token("retried-token");
        });
        using var graph = Session(handler);

        Assert.True((await graph.ConnectO365GraphAsync()).Status);
        Assert.Equal("retried-token", graph.AccessToken);
        Assert.Equal(2, requests);
    }

    [Fact]
    public async Task Connect_ValidCachedToken_ReusesTokenWithoutHttp() {
        using var handler = new TokenHandler((_, _) => Task.FromResult(Token("first")));
        using var graph = Session(handler);
        Assert.True((await graph.ConnectO365GraphAsync()).Status);
        Assert.True((await graph.ConnectO365GraphAsync()).Status);
        Assert.Equal("first", graph.AccessToken);
        Assert.Equal("Bearer", graph.TokenType);
        Assert.Equal(1, handler.Calls);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => graph.ConnectO365GraphAsync(canceled.Token));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(120, 300, 2)]
    [InlineData(120, 0, 1)]
    [InlineData(120, 60, 1)]
    [InlineData(0, 0, 2)]
    public async Task Connect_ExpiryAndConfiguredSkew_ControlRefresh(int lifetime, int skew, int expectedCalls) {
        using var handler = new TokenHandler((_, _) => Task.FromResult(Token("token", lifetime)));
        using var graph = Session(handler);
        Assert.Equal(TimeSpan.FromMinutes(5), graph.TokenRefreshSkew);
        graph.TokenRefreshSkew = TimeSpan.FromSeconds(skew);
        Assert.True((await graph.ConnectO365GraphAsync()).Status);
        Assert.True((await graph.ConnectO365GraphAsync()).Status);
        Assert.Equal(expectedCalls, handler.Calls);
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.TokenRefreshSkew = TimeSpan.FromSeconds(-1));
    }

    [Theory]
    [InlineData("{\"access_token\":\"token\",\"token_type\":\"Bearer\",\"expires_in\":\"3600\"}", 1)]
    [InlineData("{\"access_token\":\"token\",\"token_type\":\"Bearer\"}", 2)]
    [InlineData("{\"access_token\":\"token\",\"token_type\":\"Bearer\",\"expires_on\":1,\"expires_in\":3600}", 2)]
    public async Task Connect_ProviderExpiryFormats_AvoidsReusingUnknownOrExpiredTokens(string body, int expectedCalls) {
        using var handler = new TokenHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }));
        using var graph = Session(handler);
        Assert.True((await graph.ConnectO365GraphAsync()).Status);
        Assert.True((await graph.ConnectO365GraphAsync()).Status);
        Assert.Equal(expectedCalls, handler.Calls);
    }

    [Fact]
    public async Task Authenticate_SecretRotation_ClearsSessionAndUsesSeparateCacheIdentity() {
        using var handler = new TokenHandler(async (request, _) => {
            string body = await request.Content!.ReadAsStringAsync();
            Assert.Equal("login.microsoftonline.com", request.RequestUri!.Host);
            Assert.Equal("/tenant/oauth2/token", request.RequestUri.AbsolutePath);
            Assert.Contains("resource=https%3A%2F%2Fgraph.microsoft.com", body);
            return Token(body.Contains("client_secret=rotated") ? "rotated-token" : "first-token");
        });
        using var graph = Session(handler);
        Assert.True((await graph.ConnectO365GraphAsync()).Status);
        graph.Authenticate(new NetworkCredential("client@tenant", "rotated"));
        Assert.Empty(graph.AccessToken);
        Assert.Empty(graph.TokenType);
        Assert.True((await graph.ConnectO365GraphAsync()).Status);
        Assert.Equal("rotated-token", graph.AccessToken);
        graph.Authenticate(new NetworkCredential("client@tenant", "secret"));
        Assert.True((await graph.ConnectO365GraphAsync()).Status);
        Assert.Equal("first-token", graph.AccessToken);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Connect_InjectedSessions_DoNotShareTokens() {
        using var firstHandler = new TokenHandler((_, _) => Task.FromResult(Token("first")));
        using var secondHandler = new TokenHandler((_, _) => Task.FromResult(Token("second")));
        using var first = Session(firstHandler);
        using var second = Session(secondHandler);
        Assert.True((await first.ConnectO365GraphAsync()).Status);
        Assert.True((await second.ConnectO365GraphAsync()).Status);
        Assert.Equal("first", first.AccessToken);
        Assert.Equal("second", second.AccessToken);
        Assert.Equal(1, firstHandler.Calls);
        Assert.Equal(1, secondHandler.Calls);
    }

    [Fact]
    public async Task MemoryCache_AuthorityResourceClientAndSecret_AreDistinctWithoutPlaintextKeys() {
        using var handler = new TokenHandler((_, _) => Task.FromResult(Token("token")));
        using var client = new HttpClient(handler);
        var cache = new GraphTokenMemoryCache();
        var keys = new HashSet<string>();
        foreach (var scope in new[] {
            ("client", "tenant", "https://graph.microsoft.com", "synthetic-secret"),
            ("other", "tenant", "https://graph.microsoft.com", "synthetic-secret"),
            ("client", "other", "https://graph.microsoft.com", "synthetic-secret"),
            ("client", "tenant", "https://manage.office.com", "synthetic-secret"),
            ("client", "tenant", "https://graph.microsoft.com", "rotated-secret")
        }) {
            var credential = new GraphCredential { ClientId = scope.Item1, DirectoryId = scope.Item2, ClientSecret = scope.Item4 };
            string key = MicrosoftGraphUtils.BuildGraphTokenCacheKey(credential, scope.Item2, scope.Item3);
            Assert.DoesNotContain("secret", key);
            Assert.True(keys.Add(key));
            await cache.GetAsync(credential, scope.Item2, scope.Item3, client, TimeSpan.FromMinutes(5), CancellationToken.None);
            await cache.GetAsync(credential, scope.Item2, scope.Item3, client, TimeSpan.FromMinutes(5), CancellationToken.None);
        }
        Assert.Equal(5, handler.Calls);
    }

    [Fact]
    public async Task Connect_ConcurrentCallers_ShareOneAcquisition() {
        var started = Signal();
        var release = Signal();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var handler = new TokenHandler(async (_, token) => {
            started.TrySetResult(true);
            await AwaitSignal(release.Task, token);
            return Token("shared");
        });
        using var graph = Session(handler);
        Task<GraphSmtpResult>[] calls = Enumerable.Range(0, 20).Select(_ => graph.ConnectO365GraphAsync(timeout.Token)).ToArray();
        await AwaitSignal(started.Task, timeout.Token);
        Assert.Equal(1, handler.Calls);
        release.TrySetResult(true);
        Assert.All(await Task.WhenAll(calls), result => Assert.True(result.Status));
        Assert.Equal(1, handler.Calls);
        Assert.Equal("shared", graph.AccessToken);
    }

    [Fact]
    public async Task Connect_CanceledWaiter_DoesNotCancelAcquisition() {
        var started = Signal();
        var release = Signal();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var waiterCancellation = new CancellationTokenSource();
        using var handler = new TokenHandler(async (_, token) => {
            started.TrySetResult(true);
            await AwaitSignal(release.Task, token);
            return Token("leader");
        });
        using var graph = Session(handler);
        Task<GraphSmtpResult> leader = graph.ConnectO365GraphAsync(timeout.Token);
        await AwaitSignal(started.Task, timeout.Token);
        Task<GraphSmtpResult> waiter = graph.ConnectO365GraphAsync(waiterCancellation.Token);
        waiterCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        release.TrySetResult(true);
        Assert.True((await leader).Status);
        Assert.True((await graph.ConnectO365GraphAsync(timeout.Token)).Status);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Connect_CanceledLeader_FollowerCanAcquireAndCache() {
        var started = Signal();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var leaderCancellation = new CancellationTokenSource();
        int calls = 0;
        using var handler = new TokenHandler(async (_, token) => {
            if (Interlocked.Increment(ref calls) == 1) {
                started.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, token);
            }
            return Token("recovered");
        });
        using var graph = Session(handler);
        Task<GraphSmtpResult> leader = graph.ConnectO365GraphAsync(leaderCancellation.Token);
        await AwaitSignal(started.Task, timeout.Token);
        Task<GraphSmtpResult> follower = graph.ConnectO365GraphAsync(timeout.Token);
        leaderCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => leader);
        Assert.True((await follower).Status);
        Assert.True((await graph.ConnectO365GraphAsync(timeout.Token)).Status);
        Assert.Equal("recovered", graph.AccessToken);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Authenticate_ChangedDuringAcquisition_DoesNotApplyOldToken() {
        var started = Signal();
        var release = Signal();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var handler = new TokenHandler(async (_, token) => {
            started.TrySetResult(true);
            await AwaitSignal(release.Task, token);
            return Token("old-token");
        });
        using var graph = Session(handler);
        Task<GraphSmtpResult> old = graph.ConnectO365GraphAsync(timeout.Token);
        await AwaitSignal(started.Task, timeout.Token);
        graph.Authenticate(new NetworkCredential("new-client@tenant", "rotated"));
        release.TrySetResult(true);
        Assert.False((await old).Status);
        Assert.Empty(graph.AccessToken);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Connect_ProviderOrTransportFailure_ExposesOnlySafeDiagnostics(int failure) {
        const string marker = "synthetic-sensitive-marker";
        using var handler = new TokenHandler((_, _) => failure switch {
            0 => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(marker) }),
            1 => throw new HttpRequestException(marker),
            2 => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(marker) }),
            _ => throw new GraphApiException(HttpStatusCode.Unauthorized, marker, marker)
        });
        using var graph = Session(handler);
        GraphSmtpResult result = await graph.ConnectO365GraphAsync();
        Assert.False(result.Status);
        Assert.Equal("Microsoft Graph authentication failed.", result.Error);
        Assert.DoesNotContain(marker, JsonSerializer.Serialize(result));
        Assert.DoesNotContain(marker, string.Join(" ", graph.LogCollector.Logs.Select(log => log.Message)));
        graph.ErrorAction = ActionPreference.Stop;
        Exception error = await Assert.ThrowsAnyAsync<Exception>(() => graph.ConnectO365GraphAsync());
        Assert.DoesNotContain(marker, error.ToString());
        Assert.Null(error.InnerException);
        if (error is GraphApiException graphError) Assert.Empty(graphError.ResponseContent);
        Assert.All(graph.LogCollector.Logs, log => Assert.Equal("Microsoft Graph authentication failed.", log.Message));
    }

    [Fact]
    public async Task Connect_FailedRefresh_ClearsOldSessionTokenAndCanRecover() {
        int calls = 0;
        using var handler = new TokenHandler((_, _) => Task.FromResult(Interlocked.Increment(ref calls) == 2
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("private") }
            : Token("token", 120)));
        using var graph = Session(handler);
        Assert.True((await graph.ConnectO365GraphAsync()).Status);
        Assert.False((await graph.ConnectO365GraphAsync()).Status);
        Assert.Empty(graph.AccessToken);
        Assert.Empty(graph.TokenType);
        Assert.True((await graph.ConnectO365GraphAsync()).Status);
        Assert.Equal(3, handler.Calls);
    }

    private static Graph Session(HttpMessageHandler handler) {
        var graph = new Graph(handler);
        graph.Authenticate(new NetworkCredential("client@tenant", "secret"));
        return graph;
    }

    private static HttpResponseMessage Token(string value, int lifetime = 3600) => new(HttpStatusCode.OK) {
        Content = new StringContent($"{{\"access_token\":\"{value}\",\"token_type\":\"Bearer\",\"expires_in\":{lifetime}}}")
    };

    private static TaskCompletionSource<bool> Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task AwaitSignal(Task signal, CancellationToken token) {
        var canceled = Signal();
        using (token.Register(() => canceled.TrySetCanceled())) {
            await await Task.WhenAny(signal, canceled.Task);
        }
    }

    private sealed class TokenHandler : HttpMessageHandler {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;
        private int _calls;
        internal int Calls => Volatile.Read(ref _calls);
        internal TokenHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => _send = send;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Interlocked.Increment(ref _calls);
            return _send(request, cancellationToken);
        }
    }
}
