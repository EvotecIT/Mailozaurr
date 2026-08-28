using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Mailozaurr.Definitions;

namespace Mailozaurr.Tests;

public class MailgunClientTests {
    private class DummyCredentials : ICredentials {
        public NetworkCredential GetCredential(Uri uri, string authType) => new NetworkCredential();
    }

    private sealed class TrackingHandler : HttpMessageHandler {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;
        public bool ResponseDisposed { get; private set; }
        public int Calls { get; private set; }

        public TrackingHandler(HttpStatusCode statusCode, string content = "") {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Calls++;
            var response = new HttpResponseMessage(_statusCode) {
                Content = new TrackingContent(_content, () => ResponseDisposed = true)
            };
            return Task.FromResult(response);
        }

        private sealed class TrackingContent : StringContent {
            private readonly Action _onDispose;

            public TrackingContent(string content, Action onDispose) : base(content) {
                _onDispose = onDispose;
            }

            protected override void Dispose(bool disposing) {
                base.Dispose(disposing);
                _onDispose();
            }
        }
    }

    private sealed class DisposingHandler : HttpMessageHandler {
        public bool Disposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            Disposed = true;
        }
    }

    private sealed class DerivedMailgunClient : MailgunClient {
        public DerivedMailgunClient(HttpMessageHandler handler) : base(handler) {
        }

        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                Disposed = true;
            }
            base.Dispose(disposing);
        }
    }

    [Fact]
    public void EmailDomain_InvalidAddress_ThrowsArgumentException() {
        using var client = new MailgunClient { From = "invalid" };
        PropertyInfo? prop = typeof(MailgunClient).GetProperty("EmailDomain", BindingFlags.NonPublic | BindingFlags.Instance);
        var ex = Assert.Throws<TargetInvocationException>(() => prop!.GetValue(client));
        Assert.IsType<ArgumentException>(ex.InnerException);
        Assert.Contains("invalid", ex.InnerException!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EmailDomain_UsesStructuredNamedSenderAddress() {
        using var client = new MailgunClient {
            From = new MimeKit.MailboxAddress("Sender", "sender@example.com")
        };
        PropertyInfo? prop = typeof(MailgunClient).GetProperty("EmailDomain", BindingFlags.NonPublic | BindingFlags.Instance);

        var domain = Assert.IsType<string>(prop!.GetValue(client));

        Assert.Equal("example.com", domain);
    }

    [Fact]
    public async Task CreateContentAsync_WithHeaders_IncludesHeaders() {
        using var client = new MailgunClient {
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Headers = new Dictionary<string, string> { ["X-Test"] = "123" }
        };
        MethodInfo? method = typeof(MailgunClient).GetMethod("CreateContentAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var task = (Task<MultipartFormDataContent>)method!.Invoke(client, new object[] { default(System.Threading.CancellationToken) })!;
        using var content = await task;
        string body = await content.ReadAsStringAsync();
        Assert.Contains("h:X-Test", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("123", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateContentAsync_WithHighPriority_IncludesPriorityHeaders() {
        using var client = new MailgunClient {
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Priority = MessagePriority.High
        };
        MethodInfo? method = typeof(MailgunClient).GetMethod("CreateContentAsync", BindingFlags.NonPublic | BindingFlags.Instance);

        var task = (Task<MultipartFormDataContent>)method!.Invoke(client, new object[] { default(CancellationToken) })!;
        using var content = await task;
        string body = await content.ReadAsStringAsync();

        Assert.Contains("h:X-Priority", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("h:Importance", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("high", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateContentAsync_DuplicateAttachmentPaths_SkipsDuplicates() {
        var file = Path.GetTempFileName();
        try {
            using var client = new MailgunClient {
                From = "sender@example.com",
                To = new List<object> { "to@example.com" },
                Attachment = new[] { file, file }
            };
            MethodInfo? method = typeof(MailgunClient).GetMethod("CreateContentAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task<MultipartFormDataContent>)method!.Invoke(client, new object[] { default(CancellationToken) })!;
            using var content = await task;
            var parts = content.Count(c => c.Headers.ContentDisposition?.Name?.Trim('"') == "attachment");
            Assert.Equal(1, parts);
        } finally {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task CreateContentAsync_DuplicateInlineAttachmentPaths_SkipsDuplicates() {
        var file = Path.GetTempFileName();
        try {
            using var client = new MailgunClient {
                From = "sender@example.com",
                To = new List<object> { "to@example.com" },
                InlineAttachment = new[] { file, file }
            };
            MethodInfo? method = typeof(MailgunClient).GetMethod("CreateContentAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task<MultipartFormDataContent>)method!.Invoke(client, new object[] { default(CancellationToken) })!;
            using var content = await task;
            var parts = content.Count(c => c.Headers.ContentDisposition?.Name?.Trim('"') == "inline");
            Assert.Equal(1, parts);
        } finally {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task CreateContentAsync_SamePathAcrossRoles_PreservesBothAndDeduplicatesWithinEachRole() {
        var file1 = Path.GetTempFileName();
        var file2 = Path.GetTempFileName();
        try {
            using var client = new MailgunClient {
                From = "sender@example.com",
                To = new List<object> { "to@example.com" },
                Attachment = new[] { file1 },
                InlineAttachment = new[] { file1, file2, file2 }
            };
            MethodInfo? method = typeof(MailgunClient).GetMethod("CreateContentAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task<MultipartFormDataContent>)method!.Invoke(client, new object[] { default(CancellationToken) })!;
            using var content = await task;
            var attachments = content.Count(c => c.Headers.ContentDisposition?.Name?.Trim('"') == "attachment");
            var inlines = content.Count(c => c.Headers.ContentDisposition?.Name?.Trim('"') == "inline");
            Assert.Equal(1, attachments);
            Assert.Equal(2, inlines);
        } finally {
            File.Delete(file1);
            File.Delete(file2);
        }
    }

    [Fact]
    public async Task CreateContentAsync_SameStructuredPathAcrossRoles_PreservesBoth() {
        var file = Path.GetTempFileName();
        try {
            using var client = new MailgunClient {
                From = "sender@example.com",
                To = new List<object> { "to@example.com" },
                Attachments = new List<AttachmentDescriptor> { new FileAttachmentDescriptor(file) },
                InlineAttachments = new List<AttachmentDescriptor> { new FileAttachmentDescriptor(file) }
            };
            MethodInfo? method = typeof(MailgunClient).GetMethod("CreateContentAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task<MultipartFormDataContent>)method!.Invoke(client, new object[] { default(CancellationToken) })!;
            using var content = await task;

            Assert.Single(content, c => c.Headers.ContentDisposition?.Name?.Trim('"') == "attachment");
            Assert.Single(content, c => c.Headers.ContentDisposition?.Name?.Trim('"') == "inline");
        } finally {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task CreateContentAsync_DuplicatePathsAcrossLegacyAndStructuredCollections_SkipsDuplicates() {
        var legacyFile = Path.GetTempFileName();
        var structuredFile = Path.GetTempFileName();
        try {
            using var client = new MailgunClient {
                From = "sender@example.com",
                To = new List<object> { "to@example.com" },
                Attachment = new[] { legacyFile },
                Attachments = new List<AttachmentDescriptor> {
                    new FileAttachmentDescriptor(Path.GetFullPath(legacyFile)),
                    new FileAttachmentDescriptor(structuredFile),
                    new FileAttachmentDescriptor(Path.GetFullPath(structuredFile))
                }
            };
            MethodInfo? method = typeof(MailgunClient).GetMethod("CreateContentAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task<MultipartFormDataContent>)method!.Invoke(client, new object[] { default(CancellationToken) })!;
            using var content = await task;
            var parts = content.Count(c => c.Headers.ContentDisposition?.Name?.Trim('"') == "attachment");
            Assert.Equal(2, parts);
        } finally {
            File.Delete(legacyFile);
            File.Delete(structuredFile);
        }
    }

    [Fact]
    public async Task CreateContentAsync_UsesStreamContentForFiles() {
        var attachment = Path.GetTempFileName();
        var inline = Path.GetTempFileName();
        try {
            File.WriteAllBytes(attachment, new byte[1024]);
            File.WriteAllBytes(inline, new byte[1024]);

            using var client = new MailgunClient {
                From = "sender@example.com",
                To = new List<object> { "to@example.com" },
                Attachment = new[] { attachment },
                InlineAttachment = new[] { inline }
            };

            MethodInfo? method = typeof(MailgunClient).GetMethod("CreateContentAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task<MultipartFormDataContent>)method!.Invoke(client, new object[] { default(CancellationToken) })!;
            using var content = await task;

            var attachmentContent = content.Single(c => c.Headers.ContentDisposition?.Name?.Trim('"') == "attachment");
            var inlineContent = content.Single(c => c.Headers.ContentDisposition?.Name?.Trim('"') == "inline");

            Assert.IsType<StreamContent>(attachmentContent);
            Assert.IsType<StreamContent>(inlineContent);

            var streamField = typeof(StreamContent)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(f => typeof(Stream).IsAssignableFrom(f.FieldType));
            Assert.NotNull(streamField);

            var attachmentStream = (Stream)streamField!.GetValue(attachmentContent)!;
            var inlineStream = (Stream)streamField.GetValue(inlineContent)!;

            Assert.IsType<FileStream>(attachmentStream);
            Assert.IsType<FileStream>(inlineStream);
            Assert.Equal(new FileInfo(attachment).Length, attachmentContent.Headers.ContentLength);
            Assert.Equal(new FileInfo(inline).Length, inlineContent.Headers.ContentLength);
        } finally {
            File.Delete(attachment);
            File.Delete(inline);
        }
    }

    [Fact]
    public async Task CreateContentAsync_PreservesStructuredAttachmentMetadata() {
        var file = Path.GetTempFileName();
        try {
            using var client = new MailgunClient {
                From = "sender@example.com",
                To = new List<object> { "to@example.com" },
                Attachments = new List<AttachmentDescriptor> {
                    new FileAttachmentDescriptor(file) {
                        FileName = "report.pdf",
                        ContentType = "application/pdf",
                        ContentId = "report-content"
                    }
                }
            };

            MethodInfo? method = typeof(MailgunClient).GetMethod("CreateContentAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task<MultipartFormDataContent>)method!.Invoke(client, new object[] { default(CancellationToken) })!;
            using var content = await task;
            var attachment = content.Single(part => part.Headers.ContentDisposition?.Name?.Trim('"') == "attachment");

            Assert.Equal("report.pdf", attachment.Headers.ContentDisposition?.FileName?.Trim('"'));
            Assert.Equal("application/pdf", attachment.Headers.ContentType?.MediaType);
            Assert.Contains("report-content", attachment.Headers.GetValues("Content-ID"));
        } finally {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task CreateContentAsync_InfersStructuredAttachmentContentTypeFromFileName() {
        using var client = new MailgunClient {
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Attachments = new List<AttachmentDescriptor> {
                new ByteArrayAttachmentDescriptor(new byte[] { 1, 2, 3 }, "report.pdf")
            }
        };
        MethodInfo? method = typeof(MailgunClient).GetMethod(
            "CreateContentAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        using var content = await (Task<MultipartFormDataContent>)method!.Invoke(
            client,
            new object[] { CancellationToken.None })!;
        var attachment = Assert.Single(content, item =>
            item.Headers.ContentDisposition?.Name?.Trim('"') == "attachment");

        Assert.Equal("application/pdf", attachment.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void CreateContentAsync_RejectsMissingStructuredFileAttachment() {
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        using var client = new MailgunClient {
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Attachments = new List<AttachmentDescriptor> { new FileAttachmentDescriptor(missing) }
        };

        MethodInfo? method = typeof(MailgunClient).GetMethod("CreateContentAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var exception = Assert.Throws<TargetInvocationException>(() =>
            method!.Invoke(client, new object[] { default(CancellationToken) }));
        Assert.IsType<FileNotFoundException>(exception.InnerException);
    }

    [Fact]
    public async Task SendEmailAsync_InvalidCredentials_ThrowsInvalidOperationException() {
        using var client = new MailgunClient {
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Credentials = new DummyCredentials()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendEmailAsync());
    }

    [Fact]
    public async Task SendEmailAsync_DisposesResponse_OnSuccess() {
        var handler = new TrackingHandler(HttpStatusCode.OK);
        using var client = CreateClient(handler);
        var result = await client.SendEmailAsync();
        Assert.True(result.Status);
        Assert.True(handler.ResponseDisposed);
    }

    [Fact]
    public async Task SendEmailAsync_ReturnsNativeMessageId() {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(
                    "{\"id\":\"<mailgun-123@example.com>\",\"message\":\"Queued\"}")
            });
        using var client = CreateClient(handler);

        var result = await client.SendEmailAsync();

        Assert.True(result.Status);
        Assert.Equal("<mailgun-123@example.com>", result.MessageId);
    }

    [Fact]
    public async Task SendEmailAsync_DisposesResponse_OnFailure() {
        var handler = new TrackingHandler(HttpStatusCode.BadRequest, "bad");
        using var client = CreateClient(handler);
        var result = await client.SendEmailAsync();
        Assert.False(result.Status);
        Assert.True(handler.ResponseDisposed);
    }

    [Fact]
    public async Task SendEmailAsync_PermanentFailureDoesNotRetry() {
        var handler = new TrackingHandler(HttpStatusCode.BadRequest, "invalid request");
        using var client = CreateClient(handler);
        client.RetryCount = 3;

        var result = await client.SendEmailAsync();

        Assert.False(result.Status);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task SendEmailAsync_StreamAttachmentCanBeReadAcrossRetries() {
        var contentBytes = System.Text.Encoding.UTF8.GetBytes("repeatable-content");
        var source = new NonSeekableReadStream(contentBytes);
        var handler = new SequencedHandler(
            HttpStatusCode.InternalServerError,
            HttpStatusCode.OK);
        using var client = new MailgunClient(handler) {
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Credentials = new NetworkCredential(string.Empty, "key"),
            RetryCount = 1,
            Attachments = new List<AttachmentDescriptor> {
                new StreamAttachmentDescriptor(source, "report.bin", leaveStreamOpen: false)
            }
        };

        var result = await client.SendEmailAsync();

        Assert.True(result.Status);
        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.All(handler.RequestBodies, body => Assert.Contains("repeatable-content", body, StringComparison.Ordinal));
        Assert.False(source.CanRead);
    }

    [Fact]
    public void Dispose_DerivedType_DisposesHttpClient() {
        var handler = new DisposingHandler();
        var client = new DerivedMailgunClient(handler) {
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Credentials = new NetworkCredential(string.Empty, "key")
        };

        client.Dispose();

        Assert.True(handler.Disposed);
        Assert.True(client.Disposed);
    }

    [Fact]
    public async Task SendEmailAsync_AfterDispose_ThrowsObjectDisposedException() {
        var client = new MailgunClient {
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Credentials = new NetworkCredential(string.Empty, "key")
        };

        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.SendEmailAsync());
    }

    private static MailgunClient CreateClient(HttpMessageHandler handler) {
        return new MailgunClient(handler) {
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Credentials = new NetworkCredential(string.Empty, "key")
        };
    }

    private sealed class SequencedHandler : HttpMessageHandler {
        private readonly Queue<HttpStatusCode> _statuses;

        public SequencedHandler(params HttpStatusCode[] statuses) {
            _statuses = new Queue<HttpStatusCode>(statuses);
        }

        public List<string> RequestBodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync());
            return new HttpResponseMessage(_statuses.Dequeue()) {
                Content = new StringContent("response")
            };
        }
    }

    private sealed class NonSeekableReadStream : Stream {
        private readonly MemoryStream _inner;

        public NonSeekableReadStream(byte[] bytes) {
            _inner = new MemoryStream(bytes, writable: false);
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing) {
            if (disposing) {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
