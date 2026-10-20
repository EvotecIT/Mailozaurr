using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using MailKit;
using MimeKit;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public class GraphSafeRetryRegressionTests {
    private const string Marker = "synthetic-private-provider-failure";

    [Fact]
    public async Task ConnectWithRetry_TransportFailure_RetriesWithoutExposingDetails() {
        int calls = 0;
        using var handler = new Handler((_, _) => {
            if (++calls < 3) throw new HttpRequestException(Marker);
            return Task.FromResult(Token());
        });
        using var http = new StaticHttpScope(handler);
        var messages = new List<string>();
        InternalLogger logger = LoggingMessages.Logger;
        EventHandler<LogEventArgs> listener = (_, args) => messages.Add(args.Message);
        logger.OnWarningMessage += listener;
        try {
            string token = await MicrosoftGraphUtils.ConnectO365GraphWithRetryAsync(Credential(), "tenant", 2, 0, 1);
            Assert.Equal("Bearer synthetic-token", token);
            Assert.Equal(3, calls);
            Assert.Equal(2, messages.Count);
            Assert.DoesNotContain(Marker, string.Join(" ", messages));
        } finally {
            logger.OnWarningMessage -= listener;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendDraft_TransientTransportFailure_HonorsMaxRetries(bool exhaust) {
        int sends = 0;
        using var handler = new Handler((request, _) => {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/send", StringComparison.Ordinal)) {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"id\":\"draft\"}") });
            }
            if (++sends < 3 || exhaust) throw new HttpRequestException(Marker);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        });
        using var graph = Session(handler, true);
        graph.WithSendPolicy(new GraphSendPolicy { MaxConcurrency = 0, MaxRetries = 2, BaseDelayMs = 0, JitterMs = 0 });
        GraphSmtpResult result = await graph.SendMessageDraftAsync();
        Assert.Equal(!exhaust, result.Status);
        Assert.Equal(3, sends);
        Assert.DoesNotContain(Marker, Diagnostics(graph, result));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SendBatch_TopLevelFailure_SanitizesOnlyWhenEnabled(bool suppress, bool transport) {
        using var handler = new Handler((request, _) => {
            Assert.EndsWith("/$batch", request.RequestUri!.AbsolutePath);
            if (transport) throw new HttpRequestException(Marker);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(Marker) });
        });
        using var http = new StaticHttpScope(handler);
        using var graph = Session(new Handler((_, _) => throw new InvalidOperationException("Unexpected session HTTP request.")), suppress);
        graph.Authenticate(new NetworkCredential("client@tenant", "synthetic-secret"));
        http.SeedToken(Credential());
        Exception error = await Assert.ThrowsAnyAsync<Exception>(() => graph.SendMessageBatchAsync());
        if (suppress) {
            Assert.DoesNotContain(Marker, error.ToString());
            Assert.Null(error.InnerException);
            if (error is GraphApiException api) {
                Assert.Equal(HttpStatusCode.BadRequest, api.StatusCode);
                Assert.Empty(api.ResponseContent);
            }
        } else {
            Assert.Contains(Marker, error.ToString());
            Assert.Contains(Marker, Assert.IsType<GraphApiException>(error).ResponseContent);
        }
        Assert.DoesNotContain(Marker, Diagnostics(graph));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_SmtpFallbackSendFailure_ProtectsLogsAndWebhookOnlyWhenEnabled(bool suppress) {
        var client = new FailingSmtpClient();
        var factory = Smtp.ClientFactory;
        Smtp.ClientFactory = _ => client;
        Smtp smtp;
        try {
            smtp = new Smtp { RetryCount = 0, LogCollector = new LogCollector() };
        } finally {
            Smtp.ClientFactory = factory;
        }
        string? webhookPayload = null;
        using var webhookHandler = new Handler(async (request, _) => {
            webhookPayload = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var webhookClient = new HttpClient(webhookHandler);
        FieldInfo sharedField = typeof(Helpers).GetField("s_sharedHttpClient", BindingFlags.Static | BindingFlags.NonPublic)!;
        var originalClient = (HttpClient)sharedField.GetValue(null)!;
        sharedField.SetValue(null, webhookClient);
        InternalLogger logger = LoggingMessages.Logger;
        var warnings = new List<string>();
        EventHandler<LogEventArgs> listener = (_, args) => warnings.Add(args.Message);
        logger.OnWarningMessage += listener;
        try {
            using var graph = Session(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("graph failed") })), suppress);
            graph.WebhookUrl = "https://webhook.example.test/result";
            graph.WithSendPolicy(new GraphSendPolicy { MaxConcurrency = 0, MaxRetries = 0, EnableSmtpFallback = true });
            graph.WithSmtpFallback(() => smtp);
            GraphSmtpResult result = await graph.SendMessageAsync();
            Assert.False(result.Status);
            Assert.Equal(1, client.Sends);
            Assert.Same(logger, LoggingMessages.Logger);
            Assert.False(smtp.SuppressDiagnostics);
            Assert.Equal(graph.WebhookUrl, smtp.WebhookUrl);
            string diagnostics = Diagnostics(graph, result) + string.Join(" ", smtp.LogCollector!.Logs.Select(log => log.Message)) + string.Join(" ", warnings);
            Assert.NotNull(webhookPayload);
            if (suppress) {
                Assert.DoesNotContain(Marker, diagnostics);
                Assert.DoesNotContain(Marker, webhookPayload);
            } else {
                Assert.Contains(Marker, diagnostics);
                Assert.Contains(Marker, webhookPayload);
            }
        } finally {
            logger.OnWarningMessage -= listener;
            sharedField.SetValue(null, originalClient);
            smtp.Dispose();
        }
    }

    private static GraphCredential Credential() => new() { ClientId = "client", ClientSecret = "synthetic-secret", DirectoryId = "tenant" };
    private static HttpResponseMessage Token() => new(HttpStatusCode.OK) {
        Content = new StringContent("{\"access_token\":\"synthetic-token\",\"token_type\":\"Bearer\",\"expires_in\":3600}")
    };
    private static Graph Session(HttpMessageHandler handler, bool suppress) => new Graph(handler) {
        SuppressSensitiveDiagnostics = suppress, From = "sender@example.test", To = new object[] { "recipient@example.test" },
        Subject = "subject", HTML = "body", AccessToken = "synthetic-token", TokenType = "Bearer"
    }.WithSendPolicy(new GraphSendPolicy { MaxConcurrency = 0, MaxRetries = 0, BaseDelayMs = 0, JitterMs = 0 });
    private static string Diagnostics(Graph graph, GraphSmtpResult? result = null) =>
        JsonSerializer.Serialize(result) + string.Join(" ", graph.LogCollector.Logs.Select(log => log.Message));

    private sealed class Handler : HttpMessageHandler {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;
        internal Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => _send = send;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => _send(request, cancellationToken);
    }

    private sealed class FailingSmtpClient : ClientSmtp {
        internal int Sends;
        public override Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null) {
            Sends++;
            throw new InvalidOperationException(Marker);
        }
    }

    private sealed class StaticHttpScope : IDisposable {
        private readonly HttpClient _client;
        private readonly FieldInfo _handlerField;
        private readonly object _original;
        private readonly ConcurrentDictionary<string, GraphAuthorization> _cache;
        private readonly KeyValuePair<string, GraphAuthorization>[] _previousTokens;
        internal StaticHttpScope(HttpMessageHandler handler) {
            _client = (HttpClient)typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            _handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic)!;
            _original = _handlerField.GetValue(_client)!;
            _handlerField.SetValue(_client, handler);
            _cache = (ConcurrentDictionary<string, GraphAuthorization>)typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            _previousTokens = _cache.ToArray();
            _cache.Clear();
            OAuthCacheTestHelper.ResetOAuthTokenCache();
            OAuthCacheTestHelper.DeleteOAuthCacheFile();
        }
        internal void SeedToken(GraphCredential credential) => _cache[MicrosoftGraphUtils.BuildGraphTokenCacheKey(credential, "tenant", "https://graph.microsoft.com")] =
            new GraphAuthorization { AccessToken = "synthetic-token", TokenType = "Bearer", ExpiresOn = DateTimeOffset.UtcNow.AddHours(1) };
        public void Dispose() {
            _handlerField.SetValue(_client, _original);
            _cache.Clear();
            foreach (var entry in _previousTokens) _cache[entry.Key] = entry.Value;
            OAuthCacheTestHelper.ResetOAuthTokenCache();
            OAuthCacheTestHelper.DeleteOAuthCacheFile();
        }
    }
}
