using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using MimeKit;

namespace Mailozaurr.Tests;

// Regression coverage for account isolation, provider acceptance, and safe deletion.
[Collection("GraphCollection")]
public sealed class TransportReliabilityTests {
    private sealed class CountingSmtpClient : ClientSmtp {
        public int Sends;
        public CancellationTokenSource? CancelSource;
        public override Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null) {
            Sends++;
            if (CancelSource != null) {
                CancelSource.Cancel();
                return Task.FromCanceled<string>(cancellationToken);
            }
            return Task.FromResult("accepted");
        }
    }

    private sealed class FailingSentRepository : ISentMessageRepository {
        public Task SaveAsync(SentMessageRecord record, CancellationToken cancellationToken = default) =>
            Task.FromException(new IOException("sent-log disk unavailable"));
        public Task<SentMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SentMessageRecord?>(null);
    }

    private static async Task<Smtp> CreateSmtpAsync(ClientSmtp client) {
        var smtp = new Smtp();
        typeof(Smtp).GetField("<Client>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(smtp, client);
        smtp.From = "sender@example.com";
        smtp.To = new object[] { "recipient@example.com" };
        smtp.Subject = "audit";
        smtp.TextBody = "body";
        await smtp.CreateMessageAsync();
        return smtp;
    }

    [Fact]
    public async Task SentLogFailure_PreservesAcceptanceWithoutResending() {
        var client = new CountingSmtpClient();
        var smtp = await CreateSmtpAsync(client);
        smtp.SentMessageRepository = new FailingSentRepository();
        smtp.RetryCount = 1;
        smtp.RetryAlways = true;
        var result = await smtp.SendAsync();
        Assert.Equal(1, client.Sends);
        Assert.True(result.Status);
        Assert.True(result.DeliveryAccepted);
        Assert.Contains("sent-log", Assert.Single(result.PostSendErrors));
        smtp.Dispose();
    }

    [Fact]
    public async Task CallerCancellation_PropagatesWithoutQueueing() {
        using var cancellation = new CancellationTokenSource();
        var client = new CountingSmtpClient { CancelSource = cancellation };
        var smtp = await CreateSmtpAsync(client);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => smtp.SendAsync(cancellation.Token));
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(1, client.Sends);
        smtp.Dispose();
    }

    private sealed class RecordingHandler : HttpMessageHandler {
        public readonly List<string?> Tokens = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Tokens.Add(request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"messages\":[]}") });
        }
    }

    [Fact]
    public async Task SharedGmailHttpClient_IsolatesAccountAuthorization() {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        using var first = new GmailApiClient(http, credential: new OAuthCredential { AccessToken = "account-A" });
        using var second = new GmailApiClient(http, credential: new OAuthCredential { AccessToken = "account-B" });
        await second.ListPageAsync("me");
        Assert.Equal("account-B", Assert.Single(handler.Tokens));
    }

    private sealed class RecordingImapClient : ImapClient {
        public string? Password;
        public override Task AuthenticateAsync(Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default) {
            Password = ((NetworkCredential)credentials).Password;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ImapBasicAuth_PreservesWhitespaceInPassword() {
        using var client = new RecordingImapClient();
        await ProtocolAuth.AuthenticateImapAsync(client, "user@example.test", " secret ", ProtocolAuthMode.Basic);
        Assert.Equal(" secret ", client.Password);
    }

    private sealed class PoolClient : ClientSmtp {
        public int Connects;
        private bool connected;
        public override bool IsConnected => connected;
        public override void Connect(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) { Connects++; connected = true; }
        public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Connect(host, port, options, cancellationToken);
            return Task.CompletedTask;
        }
        public override void Disconnect(bool quit, CancellationToken cancellationToken = default) { connected = false; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PoolRent_PreservesCurrentMessagesConfiguration(bool asynchronous) {
        SmtpConnectionPool.ClearConnectionPool();
        var originalFactory = Smtp.ClientFactory;
        try {
            Smtp.ClientFactory = _ => new PoolClient();
            var first = new Smtp { UseConnectionPool = true, From = "sender@example.com", To = new object[] { "old@example.com" }, Subject = "old", TextBody = "old body" };
            if (asynchronous) await first.ConnectAsync("audit.invalid", 587, SecureSocketOptions.StartTls);
            else first.Connect("audit.invalid", 587, SecureSocketOptions.StartTls);
            first.CreateMessage();
            first.Dispose();
            var second = new Smtp { UseConnectionPool = true, From = "sender@example.com", To = new object[] { "new@example.com" }, Subject = "new", TextBody = "new body" };
            second.Headers = new Dictionary<string, string> { ["X-Current-Message"] = "new" };
            if (asynchronous) await second.ConnectAsync("audit.invalid", 587, SecureSocketOptions.StartTls);
            else second.Connect("audit.invalid", 587, SecureSocketOptions.StartTls);
            second.CreateMessage();
            Assert.Equal("new@example.com", Assert.Single(second.Message.To.Mailboxes).Address);
            Assert.Equal("new", second.Message.Subject);
            Assert.Equal("new body", second.Message.TextBody);
            Assert.Equal("new", second.Message.Headers["X-Current-Message"]);
            second.Dispose();
        } finally {
            Smtp.ClientFactory = originalFactory;
            SmtpConnectionPool.ClearConnectionPool();
        }
    }

    private sealed class DeletionFolder : ImapDeleteOperations.IImapDeleteFolder, IImapUidExpungeFolder {
        public string FullName => "INBOX";
        public readonly HashSet<uint> Flagged = new() { 10 };
        public readonly List<uint> Removed = new();
        public bool SupportsUidExpunge = true;
        public Task AddFlagsAsync(IReadOnlyCollection<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) {
            foreach (var uid in uids) Flagged.Add(uid.Id);
            return Task.CompletedTask;
        }
        public Task RemoveFlagsAsync(IReadOnlyCollection<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) { Flagged.Add(uid.Id); return Task.CompletedTask; }
        public Task RemoveFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ExpungeAsync(CancellationToken cancellationToken = default) {
            throw new InvalidOperationException("A targeted delete must never issue folder-wide EXPUNGE.");
        }
        public Task<bool> ExpungeAsync(IReadOnlyCollection<UniqueId> uids, CancellationToken cancellationToken = default) {
            if (!SupportsUidExpunge) return Task.FromResult(false);
            foreach (var uid in uids) {
                if (Flagged.Remove(uid.Id)) Removed.Add(uid.Id);
            }
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task TargetedImapDelete_PreservesUnselectedDeletedMessages() {
        var folder = new DeletionFolder();
        var result = await ImapDeleteOperations.DeleteAsync(folder, new[] { new UniqueId(20) }, expunge: true);
        Assert.Equal(1, result.Deleted);
        Assert.DoesNotContain((uint)10, folder.Removed);
        Assert.Contains((uint)20, folder.Removed);
    }

    [Fact]
    public async Task TargetedImapDelete_WithoutUidExpungeLeavesSelectedMessagesFlagged() {
        var folder = new DeletionFolder { SupportsUidExpunge = false };
        var result = await ImapDeleteOperations.DeleteAsync(folder, new[] { new UniqueId(20) }, expunge: true);
        Assert.Equal(1, result.Deleted);
        Assert.False(result.Expunged);
        Assert.Empty(folder.Removed);
        Assert.Equal(new uint[] { 10, 20 }, folder.Flagged.OrderBy(uid => uid));
    }

    [Fact]
    public async Task GmailTokenRefresh_DoesNotChangeOtherClientsAuthorization() {
        var handler = new RefreshHandler();
        using var http = new HttpClient(handler);
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "default-token");
        using var first = new GmailApiClient(http, _ => Task.FromResult("refreshed-A"), new OAuthCredential { AccessToken = "A" });
        using var second = new GmailApiClient(http, credential: new OAuthCredential { AccessToken = "B" });
        await Assert.ThrowsAsync<GmailAuthenticationException>(() => first.ListPageAsync("me"));
        await second.ListPageAsync("me");
        await first.ListPageAsync("me");
        Assert.Equal(new[] { "A", "B", "refreshed-A" }, handler.Tokens);
        Assert.Equal("default-token", http.DefaultRequestHeaders.Authorization.Parameter);
    }

    private sealed class RefreshHandler : HttpMessageHandler {
        internal readonly List<string?> Tokens = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Tokens.Add(request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(Tokens.Count == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK) {
                Content = new StringContent("{\"messages\":[]}")
            });
        }
    }

    private sealed class UploadLimitHandler : HttpMessageHandler {
        public readonly List<long> UploadedBytes = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (request.Method == HttpMethod.Post) {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(request.RequestUri!.AbsolutePath.EndsWith("createUploadSession", StringComparison.Ordinal) ?
                        "{\"uploadUrl\":\"https://upload.example.test/session\"}" : "{\"id\":\"draft\"}")
                });
            }
            var length = request.Content!.Headers.ContentLength!.Value;
            UploadedBytes.Add(length);
            Assert.Equal(length, request.Content.Headers.ContentRange!.To - request.Content.Headers.ContentRange.From + 1);
            return Task.FromResult(new HttpResponseMessage(length <= Graph.MaxChunkSize ? HttpStatusCode.Accepted : HttpStatusCode.RequestEntityTooLarge) {
                Content = new StringContent("{}")
            });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GraphAttachmentUpload_UsesCompliantRangesAndUploadsAllBytes(bool mailboxImport) {
        using var part = new MimePart("application", "octet-stream") {
            FileName = "audit.bin",
            Content = new MimeContent(new MemoryStream(new byte[12 * 1024 * 1024]))
        };
        using var attachment = DecodedMimeAttachment.DecodeToTempFile(part);
        var handler = new UploadLimitHandler();
        using var http = new HttpClient(handler);
        if (mailboxImport) {
            using var graph = new GraphApiClient(http, credential: new OAuthCredential { AccessToken = "audit-token" });
            using var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse("sender@example.test"));
            message.To.Add(MailboxAddress.Parse("recipient@example.test"));
            part.Content!.Stream!.Position = 0;
            message.Body = new Multipart("mixed") { new TextPart("plain") { Text = "body" }, part };
            var imported = await new GraphMailboxBrowser(graph).ImportMessageAsync(message, maxInlineAttachmentBytes: 0);
            Assert.Equal("draft", imported.NativeId);
        } else {
            var error = await GraphLargeAttachmentUploader.UploadAsync(http, "audit-token", "draft", new[] { attachment });
            Assert.Null(error);
        }
        Assert.All(handler.UploadedBytes, size => Assert.InRange(size, 1, 3_999_999));
        Assert.Equal(12 * 1024 * 1024, handler.UploadedBytes.Sum());
    }

    [Fact]
    public void PoolIsolatesStrictCertificateValidationFromPermissiveConnections() {
        SmtpConnectionPool.ClearConnectionPool();
        var originalFactory = Smtp.ClientFactory;
        try {
            Smtp.ClientFactory = _ => new PoolClient();
            var permissive = new Smtp { UseConnectionPool = true, SkipCertificateValidation = true };
            permissive.Connect("audit.invalid", 587, SecureSocketOptions.StartTls);
            permissive.Dispose();
            var strict = new Smtp { UseConnectionPool = true, SkipCertificateValidation = false };
            strict.Connect("audit.invalid", 587, SecureSocketOptions.StartTls);
            Assert.Null(strict.Client.ServerCertificateValidationCallback);
            strict.Dispose();
        } finally {
            Smtp.ClientFactory = originalFactory;
            SmtpConnectionPool.ClearConnectionPool();
        }
    }

#if NET8_0_OR_GREATER
    private sealed class ConcurrentSendBarrier {
        public readonly TaskCompletionSource<bool> Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Sends;
        public async Task<string> SendAsync() {
            Interlocked.Increment(ref Sends);
            Started.TrySetResult(true);
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return "accepted";
        }
    }

    private sealed class BarrierSmtpClient(ConcurrentSendBarrier barrier) : ClientSmtp {
        public override Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null) => barrier.SendAsync();
    }

    [Fact]
    public async Task SmtpConvenienceQueueProcessor_TwoWorkersSendFileQueueRecordOnce() {
        var taskDirectory = Path.Combine(Path.GetTempPath(), "Mailozaurr-transport-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(taskDirectory);
        var repository = new FilePendingMessageRepository(Path.Combine(taskDirectory, Guid.NewGuid().ToString("N") + ".jsonl"));
        using var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.com"));
        message.To.Add(MailboxAddress.Parse("recipient@example.com"));
        message.Subject = "queued audit";
        message.Body = new TextPart("plain") { Text = "body" };
        message.MessageId = "audit@example.com";
        using var serialized = new MemoryStream();
        await message.WriteToAsync(serialized);
        await repository.SaveAsync(new PendingMessageRecord {
            MessageId = message.MessageId, MimeMessage = Convert.ToBase64String(serialized.ToArray()),
            Server = string.Empty, Provider = EmailProvider.None,
            Timestamp = DateTimeOffset.UtcNow, NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        var barrier = new ConcurrentSendBarrier();
        var first = await CreateSmtpAsync(new BarrierSmtpClient(barrier));
        var second = await CreateSmtpAsync(new BarrierSmtpClient(barrier));
        first.PendingMessageRepository = repository;
        second.PendingMessageRepository = repository;
        try {
            var firstWork = first.ProcessPendingMessagesAsync();
            await barrier.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await second.ProcessPendingMessagesAsync().WaitAsync(TimeSpan.FromSeconds(10));
            barrier.Release.TrySetResult(true);
            await firstWork;
            Assert.Equal(1, barrier.Sends);
        } finally {
            barrier.Release.TrySetResult(true);
            first.Dispose(); second.Dispose();
            await repository.WaitForPendingMaintenanceAsync();
            Directory.Delete(taskDirectory, recursive: true);
        }
    }

    private sealed class LoopbackServer : IDisposable {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        public readonly TaskCompletionSource<bool> Received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Uri Uri { get; }
        public Task Completion { get; }
        public int Requests;
        public LoopbackServer(int status, bool hold = false) {
            listener.Start();
            Uri = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/audit");
            Completion = ServeAsync(status, hold);
        }
        private async Task ServeAsync(int status, bool hold) {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            for (var attempt = 0; attempt < (status == 429 ? 2 : 1); attempt++) {
                using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
                using var stream = connection.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync(timeout.Token))) { }
                Requests++;
                Received.TrySetResult(true);
                if (hold) await Release.Task.WaitAsync(timeout.Token);
                var responseStatus = attempt == 0 ? status : 200;
                var response = $"HTTP/1.1 {responseStatus} Audit\r\nContent-Length: 2\r\nContent-Type: application/json\r\nConnection: close\r\nRetry-After: 0\r\n\r\n{{}}";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), timeout.Token);
            }
        }
        public void Dispose() { Release.TrySetResult(true); listener.Stop(); }
    }

    [Fact]
    public async Task Graph429Retry_ReplaysWithFreshRequest() {
        using var server = new LoopbackServer(429);
        using var result = await MicrosoftGraphUtils.InvokeGraphApiAsync("GET", server.Uri.ToString());
        await server.Completion;
        Assert.Equal(2, server.Requests);
    }

    [Fact]
    public async Task GraphLimitChange_PreservesInFlightRequestAdmission() {
        int originalLimit = MicrosoftGraphUtils.MaxConcurrentRequests;
        using var server = new LoopbackServer(200, hold: true);
        try {
            MicrosoftGraphUtils.MaxConcurrentRequests = 2;
            var request = MicrosoftGraphUtils.InvokeGraphApiAsync("GET", server.Uri.ToString());
            await server.Received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            MicrosoftGraphUtils.MaxConcurrentRequests = 1;
            server.Release.TrySetResult(true);
            using var result = await request;
            await server.Completion;
        } finally {
            server.Release.TrySetResult(true);
            MicrosoftGraphUtils.MaxConcurrentRequests = originalLimit;
        }
    }
#endif
}
