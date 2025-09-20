using System.Collections.Generic;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MimeKit;
using Xunit;
using Mailozaurr;

namespace Mailozaurr.Tests;

public class ClientSmtpTests
{
    [Fact]
    public void Ctor_DefaultsPriorityToNormal()
    {
        var client = new ClientSmtp();
        Assert.Equal(MessagePriority.Normal, client.Priority);
    }
    [Fact]
    public void ConvertToMailboxAddress_InvalidType_IncludesValueInException()
    {
        var client = new ClientSmtp();
        MethodInfo? method = typeof(ClientSmtp).GetMethod(
            "ConvertToMailboxAddress",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var enumerable = (IEnumerable<MailboxAddress>)method!.Invoke(client, new object[] { 42 })!;
        using var enumerator = enumerable.GetEnumerator();
        var ex = Assert.Throws<ArgumentException>(() => enumerator.MoveNext());
        Assert.Contains("42", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertStringToMailboxAddresses_InvalidInput_LogsWarning()
    {
        var client = new ClientSmtp();
        MethodInfo? method = typeof(ClientSmtp).GetMethod(
            "ConvertStringToMailboxAddresses",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var messages = new List<string>();
        void Handler(object? _, LogEventArgs e) => messages.Add(e.Message);
        LoggingMessages.Logger.OnWarningMessage += Handler;

        var enumerable = (IEnumerable<MailboxAddress>)method!.Invoke(client, new object[] { "invalid@" })!;
        var result = enumerable.ToList();

        LoggingMessages.Logger.OnWarningMessage -= Handler;
        Assert.Empty(result);
        Assert.Contains(messages, static m => m.Contains("invalid@"));
    }

    [Fact]
    public async Task CreateMessage_SyncAndAsyncProduceEquivalentMessages()
    {
        const string url = "https://example.com/img.png";
        var html = $"<img src=\"{url}\">";
        var imageContent = new byte[] { 1, 2, 3 };
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(imageContent)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("image/png") }
                }
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(imageContent)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("image/png") }
                }
            });

        var client = HtmlUtils.HttpClient;
        var handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic);
        var original = (HttpMessageHandler)handlerField!.GetValue(client)!;
        handlerField.SetValue(client, handler);

        try
        {
            var syncClient = CreateClientForTest(html);
            syncClient.CreateMessage();

            var asyncClient = CreateClientForTest(html);
            await asyncClient.CreateMessageAsync();

            Assert.Equal(syncClient.Message.HtmlBody, asyncClient.Message.HtmlBody);
            Assert.Equal(syncClient.Message.TextBody, asyncClient.Message.TextBody);
            Assert.Contains("cid:", syncClient.Message.HtmlBody);

            var syncInline = GetInlineAttachments(syncClient.Message).OrderBy(p => p.ContentId).ToList();
            var asyncInline = GetInlineAttachments(asyncClient.Message).OrderBy(p => p.ContentId).ToList();

            Assert.Equal(syncInline.Count, asyncInline.Count);
            for (var i = 0; i < syncInline.Count; i++)
            {
                Assert.Equal(syncInline[i].ContentId, asyncInline[i].ContentId);
                Assert.Equal(syncInline[i].MediaType, asyncInline[i].MediaType);
            }
        }
        finally
        {
            handlerField.SetValue(client, original);
        }
    }

    [Fact]
    public async Task CreateMessage_CancellationPropagatesToBothOverloads()
    {
        const string url = "https://example.com/slow.png";
        var html = $"<img src=\"{url}\">";
        var client = HtmlUtils.HttpClient;
        var handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic);
        var original = (HttpMessageHandler)handlerField!.GetValue(client)!;

        try
        {
            handlerField.SetValue(client, new DelayedHandler());
            var asyncClient = CreateClientForTest(html);
            using (var asyncCts = new CancellationTokenSource())
            {
                var asyncOperation = asyncClient.CreateMessageAsync(asyncCts.Token);
                asyncCts.CancelAfter(TimeSpan.FromMilliseconds(100));
                await Assert.ThrowsAsync<OperationCanceledException>(async () => await asyncOperation);
            }

            handlerField.SetValue(client, new DelayedHandler());
            var syncClient = CreateClientForTest(html);
            using var syncCts = new CancellationTokenSource();
            var syncTask = Task.Run(() => syncClient.CreateMessage(syncCts.Token));
            syncCts.CancelAfter(TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await syncTask);
        }
        finally
        {
            handlerField.SetValue(client, original);
        }
    }

    private static ClientSmtp CreateClientForTest(string html)
    {
        return new ClientSmtp
        {
            AutoEmbedRemoteImages = true,
            HtmlBody = html,
            Subject = "Hello",
            From = "sender@example.com",
            To = new object[] { "recipient@example.com" }
        };
    }

    private static List<InlineAttachmentInfo> GetInlineAttachments(MimeMessage message)
    {
        var attachments = new List<InlineAttachmentInfo>();
        foreach (var part in message.BodyParts.OfType<MimePart>())
        {
            if (!string.Equals(part.ContentDisposition?.Disposition, ContentDisposition.Inline, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            attachments.Add(new InlineAttachmentInfo(part.ContentId ?? string.Empty, part.ContentType.MimeType));
        }

        return attachments;
    }

    private sealed class InlineAttachmentInfo
    {
        public InlineAttachmentInfo(string contentId, string mediaType)
        {
            ContentId = contentId;
            MediaType = mediaType;
        }

        public string ContentId { get; }

        public string MediaType { get; }
    }

    private sealed class DelayedHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("image/png") }
                }
            };
        }
    }
}
