using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using Xunit;

namespace Mailozaurr.Tests;

public class MailgunClientTests
{
    private class DummyCredentials : ICredentials
    {
        public NetworkCredential GetCredential(Uri uri, string authType) => new NetworkCredential();
    }

    private sealed class TrackingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;
        public bool ResponseDisposed { get; private set; }

        public TrackingHandler(HttpStatusCode statusCode, string content = "")
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new TrackingContent(_content, () => ResponseDisposed = true)
            };
            return Task.FromResult(response);
        }

        private sealed class TrackingContent : StringContent
        {
            private readonly Action _onDispose;

            public TrackingContent(string content, Action onDispose) : base(content)
            {
                _onDispose = onDispose;
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                _onDispose();
            }
        }
    }

    private sealed class DisposingHandler : HttpMessageHandler
    {
        public bool Disposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Disposed = true;
        }
    }

    private sealed class DerivedMailgunClient : MailgunClient
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposed = true;
            }
            base.Dispose(disposing);
        }
    }

    [Fact]
    public void EmailDomain_InvalidAddress_ThrowsArgumentException()
    {
        using var client = new MailgunClient { From = "invalid" };
        PropertyInfo? prop = typeof(MailgunClient).GetProperty("EmailDomain", BindingFlags.NonPublic | BindingFlags.Instance);
        var ex = Assert.Throws<TargetInvocationException>(() => prop!.GetValue(client));
        Assert.IsType<ArgumentException>(ex.InnerException);
        Assert.Contains("invalid", ex.InnerException!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateContentAsync_WithHeaders_IncludesHeaders()
    {
        using var client = new MailgunClient
        {
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
    public async Task CreateContentAsync_DuplicateAttachmentPaths_SkipsDuplicates()
    {
        var file = Path.GetTempFileName();
        try
        {
            using var client = new MailgunClient
            {
                From = "sender@example.com",
                To = new List<object> { "to@example.com" },
                Attachment = new[] { file, file }
            };
            MethodInfo? method = typeof(MailgunClient).GetMethod("CreateContentAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task<MultipartFormDataContent>)method!.Invoke(client, new object[] { default(CancellationToken) })!;
            using var content = await task;
            var parts = content.Count(c => c.Headers.ContentDisposition?.Name?.Trim('"') == "attachment");
            Assert.Equal(1, parts);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task CreateContentAsync_DuplicateInlineAttachmentPaths_SkipsDuplicates()
    {
        var file = Path.GetTempFileName();
        try
        {
            using var client = new MailgunClient
            {
                From = "sender@example.com",
                To = new List<object> { "to@example.com" },
                InlineAttachment = new[] { file, file }
            };
            MethodInfo? method = typeof(MailgunClient).GetMethod("CreateContentAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task<MultipartFormDataContent>)method!.Invoke(client, new object[] { default(CancellationToken) })!;
            using var content = await task;
            var parts = content.Count(c => c.Headers.ContentDisposition?.Name?.Trim('"') == "inline");
            Assert.Equal(1, parts);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task CreateContentAsync_AttachmentAndInlineDuplicatePaths_SkipsDuplicates()
    {
        var file1 = Path.GetTempFileName();
        var file2 = Path.GetTempFileName();
        try
        {
            using var client = new MailgunClient
            {
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
            Assert.Equal(1, inlines);
        }
        finally
        {
            File.Delete(file1);
            File.Delete(file2);
        }
    }

    [Fact]
    public async Task SendEmailAsync_InvalidCredentials_ThrowsInvalidOperationException()
    {
        using var client = new MailgunClient
        {
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Credentials = new DummyCredentials()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendEmailAsync());
    }

    [Fact]
    public async Task SendEmailAsync_DisposesResponse_OnSuccess()
    {
        var handler = new TrackingHandler(HttpStatusCode.OK);
        using var client = CreateClient(handler);
        var result = await client.SendEmailAsync();
        Assert.True(result.Status);
        Assert.True(handler.ResponseDisposed);
    }

    [Fact]
    public async Task SendEmailAsync_DisposesResponse_OnFailure()
    {
        var handler = new TrackingHandler(HttpStatusCode.BadRequest, "bad");
        using var client = CreateClient(handler);
        var result = await client.SendEmailAsync();
        Assert.False(result.Status);
        Assert.True(handler.ResponseDisposed);
    }

    [Fact]
    public void Dispose_DerivedType_DisposesHttpClient()
    {
        var handler = new DisposingHandler();
        var httpClient = new HttpClient(handler);
        var client = new DerivedMailgunClient
        {
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Credentials = new NetworkCredential(string.Empty, "key")
        };
        var field = typeof(MailgunClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(client, httpClient);

        client.Dispose();

        Assert.True(handler.Disposed);
        Assert.True(client.Disposed);
    }

    [Fact]
    public async Task SendEmailAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var client = new MailgunClient
        {
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Credentials = new NetworkCredential(string.Empty, "key")
        };

        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.SendEmailAsync());
    }

    private static MailgunClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var client = new MailgunClient
        {
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Credentials = new NetworkCredential(string.Empty, "key")
        };
        var field = typeof(MailgunClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(client, httpClient);
        return client;
    }
}
