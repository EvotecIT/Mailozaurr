using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace Mailozaurr.Tests;

public class GraphSafeDiagnosticsTests {
    private const string Marker = "synthetic-secret-provider-diagnostic";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_ProviderError_SuppressesDiagnosticsOnlyWhenEnabled(bool suppress) {
        using var graph = Session(new FailureHandler(), suppress);
        GraphSmtpResult result = await graph.SendMessageAsync();
        Assert.False(result.Status);
        Assert.Equal(HttpStatusCode.BadRequest, result.GraphError!.StatusCode);
        string diagnostics = Diagnostics(graph, result);
        if (suppress) {
            Assert.DoesNotContain(Marker, diagnostics);
            Assert.Equal("Microsoft Graph send failed.", result.Error);
        } else {
            Assert.Contains(Marker, diagnostics);
            Assert.Contains(Marker, result.GraphError.Raw);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_TransportErrorOrTimeout_DoesNotEchoException(bool timeout) {
        using var graph = Session(new FailureHandler(timeout ? 2 : 1), true);
        GraphSmtpResult result = await graph.SendMessageAsync();
        Assert.False(result.Status);
        Assert.DoesNotContain(Marker, Diagnostics(graph, result));
    }

    [Fact]
    public async Task Send_StopPreference_ThrowsSafeExceptionWithoutProviderBodyOrInnerException() {
        using var graph = Session(new FailureHandler(), true);
        graph.ErrorAction = ActionPreference.Stop;
        GraphApiException error = await Assert.ThrowsAsync<GraphApiException>(() => graph.SendMessageAsync());
        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Empty(error.ResponseContent);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(Marker, error.ToString());
        Assert.DoesNotContain(Marker, Diagnostics(graph));
    }

    [Fact]
    public async Task Send_DraftCreationError_DoesNotEchoProviderBody() {
        using var graph = Session(new FailureHandler(), true);
        GraphSmtpResult result = await graph.SendMessageDraftAsync();
        Assert.False(result.Status);
        Assert.Equal(HttpStatusCode.BadRequest, result.GraphError!.StatusCode);
        Assert.DoesNotContain(Marker, Diagnostics(graph, result));
    }

    [Fact]
    public async Task Send_ExistingDraftError_DoesNotEchoProviderBody() {
        using var graph = Session(new FailureHandler(), true);
        graph.CreateMessage();
        GraphApiException error = await Assert.ThrowsAsync<GraphApiException>(() => graph.SendDraftMessage(new GraphMessage { Id = "draft" }));
        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Empty(error.ResponseContent);
        Assert.DoesNotContain(Marker, error.ToString());
        Assert.DoesNotContain(Marker, Diagnostics(graph));
    }

    [Fact]
    public async Task Send_RetryWarnings_DoNotEchoProviderBody() {
        using var handler = new FailureHandler();
        using var graph = Session(handler, true);
        graph.RetryAlways = true;
        graph.WithSendPolicy(new GraphSendPolicy { MaxConcurrency = 0, MaxRetries = 1, BaseDelayMs = 0, JitterMs = 0 });
        GraphSmtpResult result = await graph.SendMessageAsync();
        Assert.False(result.Status);
        Assert.Equal(2, handler.Calls);
        Assert.DoesNotContain(Marker, Diagnostics(graph, result));
    }

    [Fact]
    public async Task Send_SmtpFallbackFactoryFailure_DoesNotEchoException() {
        using var graph = Session(new FailureHandler(), true);
        graph.WithSendPolicy(new GraphSendPolicy { MaxConcurrency = 0, MaxRetries = 0, EnableSmtpFallback = true });
        graph.WithSmtpFallback(() => throw new InvalidOperationException(Marker));
        Exception error = await Assert.ThrowsAsync<InvalidOperationException>(() => graph.SendMessageAsync());
        Assert.DoesNotContain(Marker, error.ToString());
        Assert.DoesNotContain(Marker, Diagnostics(graph));
    }

    private static Graph Session(HttpMessageHandler handler, bool suppress) => new Graph(handler) {
        SuppressSensitiveDiagnostics = suppress,
        From = "sender@example.test",
        To = new object[] { "recipient@example.test" },
        Subject = "subject",
        HTML = "body",
        AccessToken = "synthetic-token",
        TokenType = "Bearer"
    }.WithSendPolicy(new GraphSendPolicy { MaxConcurrency = 0, MaxRetries = 0, BaseDelayMs = 0, JitterMs = 0 });

    [Fact]
    public async Task Send_ExistingDraftTransportFailure_DoesNotEchoException() {
        using var graph = Session(new FailureHandler(1), true);
        graph.CreateMessage();
        Exception error = await Assert.ThrowsAsync<InvalidOperationException>(() => graph.SendDraftMessage(new GraphMessage { Id = "draft" }));
        Assert.DoesNotContain(Marker, error.ToString());
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task Send_UploadChunkFailure_DoesNotLogResponseHeaders() {
        using var graph = Session(new FailureHandler(), true);
        using var content = new StreamContent(new MemoryStream(new byte[] { 1 }));
        await graph.SendAttachmentChunk("https://upload.example.test/session", content);
        Assert.NotEmpty(graph.LogCollector.Logs);
        Assert.DoesNotContain(Marker, Diagnostics(graph));
    }

    private static string Diagnostics(Graph graph, GraphSmtpResult? result = null) =>
        JsonSerializer.Serialize(result) + string.Join(" ", graph.LogCollector.Logs.Select(log => log.Message));

    private sealed class FailureHandler : HttpMessageHandler {
        private readonly int _mode;
        internal int Calls { get; private set; }
        internal FailureHandler(int mode = 0) => _mode = mode;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Calls++;
            if (_mode == 1) throw new HttpRequestException(Marker);
            if (_mode == 2) throw new TaskCanceledException(Marker);
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest) {
                Content = new StringContent($"{{\"error\":{{\"code\":\"BadRequest\",\"message\":\"{Marker}\",\"innerError\":{{\"request-id\":\"{Marker}\"}}}}}}")
            };
            response.Headers.TryAddWithoutValidation("X-Provider-Diagnostic", Marker);
            return Task.FromResult(response);
        }
    }
}
