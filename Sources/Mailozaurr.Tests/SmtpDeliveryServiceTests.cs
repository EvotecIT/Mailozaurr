using MailKit;
using MailKit.Security;
using Mailozaurr.Definitions;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

// Factory and Trace listeners are process-wide; use the existing serialized collection.
[Collection("GraphCollection")]
public partial class SmtpDeliveryServiceTests {
    private const string PrivateFailure = "private callback detail and secret";

    private sealed class MemoryClient : ClientSmtp {
        internal readonly List<string> Calls = new List<string>();
        internal readonly List<CancellationToken> Tokens = new List<CancellationToken>();
        internal string? FailAt;
        internal bool FailDisconnect;
        internal bool FailDispose;
        internal bool Disposed;
        internal bool CancelConnect;
        internal string? CancelAt;
        internal CancellationTokenSource? Cancellation;
        internal MimeMessage? SentMessage;
        private bool _connected;
        public override bool IsConnected => _connected;

        private void Record(string stage, CancellationToken token) {
            Calls.Add(stage);
            Tokens.Add(token);
            if (stage == CancelAt) {
                Cancellation!.Cancel();
                throw new OperationCanceledException(PrivateFailure, token);
            }
            if (stage == FailAt) {
                throw new IOException(PrivateFailure);
            }
        }

        public override void Connect(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) {
            Record("connect", cancellationToken);
            _connected = true;
        }

        public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) {
            Record("connect", cancellationToken);
            if (CancelConnect) {
                return Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
            }
            _connected = true;
            return Task.CompletedTask;
        }

        public override void Authenticate(Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default) => Record("authenticate", cancellationToken);
        public override Task AuthenticateAsync(Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default) {
            Record("authenticate", cancellationToken);
            return Task.CompletedTask;
        }

        public override void Authenticate(SaslMechanism mechanism, CancellationToken cancellationToken = default) => Record("authenticate", cancellationToken);
        public override Task AuthenticateAsync(SaslMechanism mechanism, CancellationToken cancellationToken = default) {
            Record("authenticate", cancellationToken);
            return Task.CompletedTask;
        }

        public override string Send(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null) {
            Record("send", cancellationToken);
            SentMessage = message;
            return string.Empty;
        }

        public override Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null) {
            return Task.FromResult(Send(message, cancellationToken, progress));
        }

        public override void Disconnect(bool quit, CancellationToken cancellationToken = default) {
            _connected = false;
            Calls.Add("disconnect");
            if (FailDisconnect) {
                throw new IOException(PrivateFailure);
            }
        }

        protected override void Dispose(bool disposing) {
            Disposed = true;
            base.Dispose(disposing);
            if (disposing && FailDispose) {
                throw new IOException(PrivateFailure);
            }
        }
    }

    private static Smtp CreateSmtp(MemoryClient client) {
        var smtp = new Smtp { UseConnectionPool = false };
        smtp.Client.Dispose();
        typeof(Smtp).GetField("<Client>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(smtp, client);
        smtp.From = "sender@example.test";
        smtp.To = new object[] { "recipient@example.test" };
        smtp.Subject = "Pickup and SMTP contract";
        smtp.TextBody = "A composed message.";
        return smtp;
    }

    private static SmtpSessionRequest Request(bool authenticate = true) => new SmtpSessionRequest {
        Server = "smtp.example.test",
        Port = 587,
        Authenticate = authenticate,
        UserName = "in-memory-user",
        Password = "in-memory-placeholder",
        TimeoutMs = 1234,
        RetryCount = 0
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendAsync_ComposesAndDeliversWithOptionalAuthentication_ThenDisposes(bool authenticate) {
        var client = new MemoryClient();
        var smtp = CreateSmtp(client);
        using var cancellation = new CancellationTokenSource();

        await SmtpDeliveryService.SendAsync(smtp, Request(authenticate), cancellation.Token);

        Assert.Equal(authenticate ? new[] { "connect", "authenticate", "send", "disconnect" } : new[] { "connect", "send", "disconnect" }, client.Calls);
        Assert.All(client.Tokens, token => Assert.Equal(cancellation.Token, token));
        Assert.True(client.Disposed);
        Assert.Equal(1234, smtp.Timeout);
        Assert.Equal(0, smtp.RetryCount);
        Assert.NotNull(client.SentMessage);
        Assert.Equal("Pickup and SMTP contract", client.SentMessage!.Subject);
        Assert.Equal("A composed message.", client.SentMessage.TextBody);
        Assert.Equal("sender@example.test", ((MailboxAddress)client.SentMessage.From[0]).Address);
        Assert.Equal("recipient@example.test", ((MailboxAddress)client.SentMessage.To[0]).Address);
    }

    [Theory]
    [InlineData("connect", false)]
    [InlineData("authenticate", false)]
    [InlineData("send", false)]
    [InlineData("connect", true)]
    [InlineData("authenticate", true)]
    [InlineData("send", true)]
    public async Task SendAsync_TransportFailure_ThrowsFixedErrorWithoutLeakingDiagnostics(string stage, bool throwOnError) {
        var client = new MemoryClient { FailAt = stage };
        var smtp = CreateSmtp(client);
        var collector = new LogCollector();
        smtp.LogCollector = collector;
        smtp.ErrorAction = throwOnError ? ActionPreference.Stop : ActionPreference.Continue;
        InternalLogger logger = LoggingMessages.Logger;
        int emitted = 0;
        EventHandler<LogEventArgs> listener = (_, _) => emitted++;
        logger.OnWarningMessage += listener;
        try {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SmtpDeliveryService.SendAsync(smtp, Request()));
            Assert.Equal("SMTP delivery failed.", error.Message);
            Assert.Null(error.InnerException);
            Assert.Empty(collector.Logs);
            Assert.Equal(0, emitted);
            Assert.Same(logger, LoggingMessages.Logger);
            Assert.Same(collector, smtp.LogCollector);
            Assert.False(smtp.SuppressDiagnostics);
            Assert.True(client.Disposed);
            Assert.Equal(stage == "send", client.Calls.Contains("send"));
        } finally {
            logger.OnWarningMessage -= listener;
        }
    }

    [Fact]
    public async Task SendAsync_CompositionFailure_ThrowsFixedErrorAndDoesNotSend() {
        var client = new MemoryClient();
        var smtp = CreateSmtp(client);
        smtp.From = "invalid address";
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SmtpDeliveryService.SendAsync(smtp, Request()));
        Assert.Equal("SMTP delivery failed.", error.Message);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("send", client.Calls);
        Assert.True(client.Disposed);
    }

    [Fact]
    public async Task SendAsync_CanceledConnection_PropagatesCancellationAndDisposes() {
        var client = new MemoryClient { CancelConnect = true };
        var smtp = CreateSmtp(client);
        using var cancellation = new CancellationTokenSource();
        Task sending = SmtpDeliveryService.SendAsync(smtp, Request(), cancellation.Token);
        Assert.Equal(new[] { "connect" }, client.Calls);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending);
        Assert.True(client.Disposed);
        Assert.DoesNotContain("authenticate", client.Calls);
        Assert.DoesNotContain("send", client.Calls);
    }

    [Fact]
    public async Task SendAsync_PreCanceled_DoesNotConnectAndDisposes() {
        var client = new MemoryClient();
        var smtp = CreateSmtp(client);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SmtpDeliveryService.SendAsync(smtp, Request(), cancellation.Token));
        Assert.Equal("SMTP operation canceled.", error.Message);
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Null(error.InnerException);
        Assert.Empty(client.Calls);
        Assert.True(client.Disposed);
    }

    [Theory]
    [InlineData("authenticate")]
    [InlineData("send")]
    public async Task SendAsync_CanceledAuthenticationOrSend_PropagatesAndDisposes(string stage) {
        using var cancellation = new CancellationTokenSource();
        var client = new MemoryClient { CancelAt = stage, Cancellation = cancellation };
        var smtp = CreateSmtp(client);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SmtpDeliveryService.SendAsync(smtp, Request(), cancellation.Token));
        Assert.Contains(stage, client.Calls);
        Assert.Equal(stage == "send", client.Calls.Contains("send"));
        Assert.True(client.Disposed);
    }

    [Fact]
    public async Task SendAsync_WithoutCollector_DoesNotEmitPrivateErrorsAndRestoresCallerLogger() {
        var client = new MemoryClient { FailAt = "authenticate" };
        var smtp = CreateSmtp(client);
        InternalLogger logger = LoggingMessages.Logger;
        var messages = new List<string>();
        EventHandler<LogEventArgs> listener = (_, args) => messages.Add(args.Message);
        logger.OnWarningMessage += listener;
        try {
            await Assert.ThrowsAsync<InvalidOperationException>(() => SmtpDeliveryService.SendAsync(smtp, Request()));
            Assert.Empty(messages);
            Assert.Same(logger, LoggingMessages.Logger);
            logger.WriteWarning("caller logging restored");
            Assert.Equal(new[] { "caller logging restored" }, messages);
        } finally {
            logger.OnWarningMessage -= listener;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("connect")]
    [InlineData("authenticate")]
    public async Task TestConnectionAsync_OwnsFactorySession_ConnectsAuthenticatesAndNeverSends(string? failure) {
        var client = new MemoryClient { FailAt = failure };
        var smtp = CreateSmtp(client);
        var collector = new LogCollector();
        smtp.LogCollector = collector;
        Func<Smtp> previousFactory = SmtpSessionService.SmtpFactory;
        SmtpSessionService.SmtpFactory = () => smtp;
        try {
            using var cancellation = new CancellationTokenSource();
            if (failure is null) {
                await SmtpSessionService.TestConnectionAsync(Request(), cancellation.Token);
            } else {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SmtpSessionService.TestConnectionAsync(Request(), cancellation.Token));
                Assert.Equal("SMTP connection test failed.", error.Message);
                Assert.Null(error.InnerException);
            }
            Assert.Equal("connect", client.Calls[0]);
            Assert.All(client.Tokens, token => Assert.Equal(cancellation.Token, token));
            Assert.Equal(failure != "connect", client.Calls.Contains("authenticate"));
            Assert.DoesNotContain("send", client.Calls);
            Assert.True(client.Disposed);
            Assert.Empty(collector.Logs);
        } finally {
            SmtpSessionService.SmtpFactory = previousFactory;
        }
    }

    [Fact]
    public async Task TestConnectionAsync_CanceledConnection_PropagatesAndDisposes() {
        var client = new MemoryClient { CancelConnect = true };
        var smtp = CreateSmtp(client);
        Func<Smtp> previousFactory = SmtpSessionService.SmtpFactory;
        SmtpSessionService.SmtpFactory = () => smtp;
        try {
            using var cancellation = new CancellationTokenSource();
            Task testing = SmtpSessionService.TestConnectionAsync(Request(), cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => testing);
            Assert.True(client.Disposed);
            Assert.DoesNotContain("send", client.Calls);
        } finally {
            SmtpSessionService.SmtpFactory = previousFactory;
        }
    }

    [Fact]
    public async Task SendAsync_DisconnectAndDisposeFailures_AreQuietWithFixedTraceWarning() {
        var client = new MemoryClient { FailDisconnect = true, FailDispose = true };
        var smtp = CreateSmtp(client);
        using var trace = new StringWriter();
        using var listener = new TextWriterTraceListener(trace);
        Trace.Listeners.Add(listener);
        try {
            await SmtpDeliveryService.SendAsync(smtp, Request());
            listener.Flush();
            Assert.Contains("SMTP disconnect failed.", trace.ToString());
            Assert.DoesNotContain(PrivateFailure, trace.ToString());
            Assert.True(client.Disposed);
        } finally {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public async Task WritePickupAsync_ComposesUniqueMimeFiles_WithoutTransportCalls() {
        string directory = Path.Combine(Path.GetTempPath(), "mailozaurr-pickup-" + Guid.NewGuid().ToString("N"));
        try {
            var first = new MemoryClient();
            var second = new MemoryClient();
            string firstPath = await SmtpDeliveryService.WritePickupAsync(CreateSmtp(first), directory);
            string secondPath = await SmtpDeliveryService.WritePickupAsync(CreateSmtp(second), directory);
            Assert.NotEqual(firstPath, secondPath);
            Assert.Equal(".eml", Path.GetExtension(firstPath));
            Assert.Equal(2, Directory.GetFiles(directory).Length);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
            using MimeMessage message = MimeMessage.Load(firstPath);
            Assert.Equal("Pickup and SMTP contract", message.Subject);
            Assert.Equal("A composed message.", message.TextBody?.TrimEnd('\r', '\n'));
            Assert.Equal("recipient@example.test", ((MailboxAddress)message.To[0]).Address);
            Assert.Empty(first.Calls);
            Assert.Empty(second.Calls);
            Assert.True(first.Disposed);
            Assert.True(second.Disposed);
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FailingAttachment : AttachmentDescriptor {
        private readonly bool _cancel;
        private readonly Action _onRead;
        internal FailingAttachment(bool cancel, Action onRead) { _cancel = cancel; _onRead = onRead; }
        protected override Stream CreateContentStream() => new FailingStream(_cancel, _onRead);
    }

    private sealed class FailingStream : MemoryStream {
        private readonly bool _cancel;
        private readonly Action _onRead;
        internal FailingStream(bool cancel, Action onRead) : base(Encoding.UTF8.GetBytes("attachment payload")) { _cancel = cancel; _onRead = onRead; }
        public override int Read(byte[] buffer, int offset, int count) {
            _onRead();
            throw (_cancel ? new OperationCanceledException(PrivateFailure) : new IOException(PrivateFailure));
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            _onRead();
            throw (_cancel ? new OperationCanceledException(PrivateFailure, cancellationToken) : new IOException(PrivateFailure));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WritePickupAsync_SerializationFailureOrCancellation_RemovesPartialAndPreservesExistingFiles(bool cancel) {
        string directory = Path.Combine(Path.GetTempPath(), "mailozaurr-pickup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string existing = Path.Combine(directory, "existing.eml");
        File.WriteAllText(existing, "retain this file");
        var client = new MemoryClient();
        var smtp = CreateSmtp(client);
        int stagingFilesDuringSerialization = -1;
        int pickupFilesDuringSerialization = -1;
        smtp.Attachments = new List<AttachmentDescriptor> { new FailingAttachment(cancel, () => {
            stagingFilesDuringSerialization = Directory.GetFiles(directory, "*.tmp").Length;
            pickupFilesDuringSerialization = Directory.GetFiles(directory, "*.eml").Length;
        }) { FileName = "broken.txt" } };
        try {
            if (cancel) {
                var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SmtpDeliveryService.WritePickupAsync(smtp, directory));
                Assert.Equal("SMTP operation canceled.", error.Message);
                Assert.Null(error.InnerException);
            } else {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SmtpDeliveryService.WritePickupAsync(smtp, directory));
                Assert.Equal("SMTP pickup failed.", error.Message);
                Assert.Null(error.InnerException);
            }
            Assert.Equal(new[] { existing }, Directory.GetFiles(directory));
            Assert.Equal(1, stagingFilesDuringSerialization);
            Assert.Equal(1, pickupFilesDuringSerialization);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
            Assert.Equal("retain this file", File.ReadAllText(existing));
            Assert.Empty(client.Calls);
            Assert.True(client.Disposed);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }
}
