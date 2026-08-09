using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Mailozaurr.Definitions;
using MimeKit;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphDraftTests {
    private static void SetHttpClient(Graph graph, HttpMessageHandler handler) {
        var field = typeof(Graph).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(graph, new HttpClient(handler));
    }

    private sealed class AmbiguousDirectAttachmentHandler : HttpMessageHandler {
        public int AttachmentPostCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (request.RequestUri!.AbsolutePath.Contains("/mailfolders/drafts/messages", StringComparison.OrdinalIgnoreCase)) {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) {
                    Content = new StringContent("{\"id\":\"draft-id\"}")
                });
            }
            if (request.RequestUri.AbsolutePath.EndsWith("/messages/draft-id/attachments", StringComparison.OrdinalIgnoreCase)) {
                AttachmentPostCount++;
                throw new TaskCanceledException("Response was lost after the attachment POST.");
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        }
    }

    [Fact]
    public void CreateDraft_LargeAttachments_ExcludesAttachments() {
        string tmp = Path.GetTempFileName();
        File.WriteAllBytes(tmp, new byte[4_100_000]);
        using var graph = new Graph {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "sub",
            HTML = "body",
            ContentType = "HTML",
            Attachments = new object[] { tmp }
        };

        string json = graph.CreateDraft();
        File.Delete(tmp);
        Assert.True(graph.IsLargerAttachment);
        Assert.DoesNotContain("\"attachments\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("saveToSentItems", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareAttachments_LargeFiles_CreatePlaceholders() {
        string tmp1 = Path.GetTempFileName();
        string tmp2 = Path.GetTempFileName();
        File.WriteAllBytes(tmp1, new byte[3_000_000]);
        File.WriteAllBytes(tmp2, new byte[2_000_000]);
        using var graph = new Graph { Attachments = new object[] { tmp1, tmp2 } };
        graph.CreateAttachments();
        await graph.PrepareAttachments();
        File.Delete(tmp1);
        File.Delete(tmp2);
        Assert.True(graph.IsLargerAttachment);
        Assert.Equal(2, graph.AttachmentsPlaceHolders.Count);
        Assert.All(graph.AttachmentsPlaceHolders, p => Assert.False(string.IsNullOrWhiteSpace(p.FileName)));
    }

    [Fact]
    public void CreateAttachments_TracksRawSizeSeparatelyFromSerializedSize() {
        var path = Path.GetTempFileName();
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None)) {
            stream.SetLength(120_000_000);
        }
        using var graph = new Graph { Attachments = new object[] { path } };

        try {
            graph.CreateAttachments();
        } finally {
            File.Delete(path);
        }

        Assert.Equal(120_000_000, graph.RawAttachmentSizeBytes);
        Assert.True(graph.TotalAttachmentSizeBytes > 150_000_000);
    }

    [Fact]
    public async Task SendMessageAsync_Base64ExpandedFile_UsesDraftUploadSession() {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[3_100_000]);
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"id\":\"draft-id\"}") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"uploadUrl\":\"https://upload.test/session\"}") },
            new HttpResponseMessage(HttpStatusCode.Created),
            new HttpResponseMessage(HttpStatusCode.Accepted));
        using var graph = new Graph {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "serialized size",
            HTML = "body",
            ContentType = "HTML",
            Attachments = new object[] { path },
            AccessToken = "token",
            TokenType = "Bearer"
        };
        SetHttpClient(graph, handler);

        try {
            var result = await graph.SendMessageAsync();

            Assert.True(result.Status);
            Assert.DoesNotContain(handler.Requests, request => request.RequestUri!.AbsolutePath.EndsWith("/sendMail", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(handler.Requests, request => request.RequestUri!.AbsolutePath.Contains("/mailfolders/drafts/messages", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(handler.Requests, request => request.RequestUri!.AbsolutePath.EndsWith("/createUploadSession", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(handler.Requests, request => request.Method == HttpMethod.Put && request.RequestUri!.Host == "upload.test");
            Assert.Contains(handler.Requests, request => request.RequestUri!.AbsolutePath.EndsWith("/messages/draft-id/send", StringComparison.OrdinalIgnoreCase));
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SendMessageAsync_LargeCompleteRequest_AddsSubThresholdFileDirectlyToDraft() {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[2_300_000]);
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"id\":\"draft-id\"}") },
            new HttpResponseMessage(HttpStatusCode.Created),
            new HttpResponseMessage(HttpStatusCode.Accepted));
        using var graph = new Graph {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "large body with small attachment",
            HTML = new string('x', 1_000_000),
            ContentType = "HTML",
            Attachments = new object[] {
                new FileAttachmentDescriptor(path) {
                    ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
                    ContentId = "small-inline"
                }
            },
            AccessToken = "token",
            TokenType = "Bearer"
        };
        SetHttpClient(graph, handler);

        try {
            var result = await graph.SendMessageAsync();

            Assert.True(result.Status);
            Assert.True(graph.IsLargerAttachment);
            Assert.DoesNotContain(handler.Requests, request => request.RequestUri!.AbsolutePath.EndsWith("/createUploadSession", StringComparison.OrdinalIgnoreCase));
            var attachmentRequest = Assert.Single(handler.Requests, request =>
                request.RequestUri!.AbsolutePath.EndsWith("/messages/draft-id/attachments", StringComparison.OrdinalIgnoreCase));
            var attachmentBody = await attachmentRequest.Content!.ReadAsStringAsync();
            Assert.Contains("\"contentBytes\"", attachmentBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"isInline\":true", attachmentBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"contentId\":\"small-inline\"", attachmentBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(handler.Requests, request => request.RequestUri!.AbsolutePath.EndsWith("/messages/draft-id/send", StringComparison.OrdinalIgnoreCase));
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SendMessageDraftAsync_SmallFileAddsItExactlyOnceAfterDraftCreation() {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"id\":\"draft-id\"}") },
            new HttpResponseMessage(HttpStatusCode.Created),
            new HttpResponseMessage(HttpStatusCode.Accepted));
        using var graph = new Graph {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "direct draft",
            HTML = "body",
            ContentType = "HTML",
            Attachments = new object[] { path },
            AccessToken = "token",
            TokenType = "Bearer"
        };
        SetHttpClient(graph, handler);

        try {
            var result = await graph.SendMessageDraftAsync();

            Assert.True(result.Status);
            var createBody = await handler.Requests[0].Content!.ReadAsStringAsync();
            Assert.DoesNotContain("\"attachments\"", createBody, StringComparison.OrdinalIgnoreCase);
            Assert.Single(handler.Requests, request =>
                request.RequestUri!.AbsolutePath.EndsWith("/messages/draft-id/attachments", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(handler.Requests, request =>
                request.RequestUri!.AbsolutePath.EndsWith("/createUploadSession", StringComparison.OrdinalIgnoreCase));
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SendMessageDraftAsync_DirectAttachmentFailureUsesSmtpFallbackWithInlineRole() {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"id\":\"draft-id\"}") },
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("failure") });
        Smtp? fallback = null;
        using var graph = new Graph {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "fallback inline",
            HTML = "body",
            ContentType = "HTML",
            Attachments = new object[] {
                new FileAttachmentDescriptor(path) {
                    ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
                    ContentId = "fallback-inline"
                }
            },
            AccessToken = "token",
            TokenType = "Bearer"
        };
        graph.WithSendPolicy(new GraphSendPolicy { EnableSmtpFallback = true, MaxRetries = 0 });
        graph.WithSmtpFallback(() => fallback = new Smtp { DryRun = true });
        SetHttpClient(graph, handler);

        try {
            var result = await graph.SendMessageDraftAsync();

            Assert.False(result.Status);
            Assert.NotNull(fallback);
            var inline = Assert.Single(fallback!.InlineAttachments!);
            Assert.Same(graph.Attachments![0], inline);
            Assert.Single(handler.Requests, request =>
                request.RequestUri!.AbsolutePath.EndsWith("/messages/draft-id/attachments", StringComparison.OrdinalIgnoreCase));
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SendMessageDraftAsync_UploadSessionFailureUsesSmtpFallbackOnce() {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[3_100_000]);
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"id\":\"draft-id\"}") },
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("failure") });
        Smtp? fallback = null;
        using var graph = new Graph {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "fallback upload",
            HTML = "body",
            ContentType = "HTML",
            Attachments = new object[] { path },
            AccessToken = "token",
            TokenType = "Bearer"
        };
        graph.WithSendPolicy(new GraphSendPolicy { EnableSmtpFallback = true, MaxRetries = 0 });
        graph.WithSmtpFallback(() => fallback = new Smtp { DryRun = true });
        SetHttpClient(graph, handler);

        try {
            var result = await graph.SendMessageDraftAsync();

            Assert.False(result.Status);
            Assert.NotNull(fallback);
            Assert.Single(fallback!.Attachments!);
            Assert.Single(handler.Requests, request =>
                request.RequestUri!.AbsolutePath.EndsWith("/createUploadSession", StringComparison.OrdinalIgnoreCase));
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SendMessageDraftAsync_AmbiguousDirectPostIsNotRetried() {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
        var handler = new AmbiguousDirectAttachmentHandler();
        using var graph = new Graph {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "ambiguous direct post",
            HTML = "body",
            ContentType = "HTML",
            Attachments = new object[] { path },
            AccessToken = "token",
            TokenType = "Bearer"
        };
        graph.WithSendPolicy(new GraphSendPolicy { MaxRetries = 3 });
        SetHttpClient(graph, handler);

        try {
            var result = await graph.SendMessageDraftAsync();

            Assert.False(result.Status);
            Assert.Equal(1, handler.AttachmentPostCount);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SendMessageBatchAsync_LargeFile_AuthenticatesBeforeDraftUploadSession() {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[3_100_000]);
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"token\",\"token_type\":\"Bearer\"}") },
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"id\":\"draft-id\"}") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"uploadUrl\":\"https://upload.test/session\"}") },
            new HttpResponseMessage(HttpStatusCode.Created),
            new HttpResponseMessage(HttpStatusCode.Accepted));
        using var graph = new Graph {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "batch upload",
            HTML = "body",
            ContentType = "HTML",
            Attachments = new object[] { path }
        };
        graph.Authenticate(new NetworkCredential("client@tenant", "secret"));
        SetHttpClient(graph, handler);

        try {
            var result = await graph.SendMessageBatchAsync();

            Assert.True(result.Status);
            Assert.Contains(handler.Requests, request => request.RequestUri!.Host == "login.microsoftonline.com");
            Assert.DoesNotContain(handler.Requests, request => request.RequestUri!.AbsolutePath == "/v1.0/$batch");
            Assert.Contains(handler.Requests, request => request.RequestUri!.AbsolutePath.EndsWith("/createUploadSession", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(handler.Requests, request => request.RequestUri!.AbsolutePath.EndsWith("/messages/draft-id/send", StringComparison.OrdinalIgnoreCase));
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task PrepareAttachments_RelativeAndAbsoluteAliases_CreateOnePlaceholder() {
        var fileName = $"mailozaurr-graph-large-{Guid.NewGuid():N}.tmp";
        var absolutePath = Path.Combine(Environment.CurrentDirectory, fileName);
        File.WriteAllBytes(absolutePath, new byte[4_100_000]);
        using var graph = new Graph {
            Attachments = new object[] { fileName, absolutePath }
        };

        try {
            graph.CreateAttachments();
            await graph.PrepareAttachments();
        } finally {
            File.Delete(absolutePath);
        }

        Assert.True(graph.IsLargerAttachment);
        Assert.Single(graph.AttachmentsPlaceHolders);
    }

    [Fact]
    public async Task PrepareAttachments_SameLargeFileAcrossRoles_CreatesBothPlaceholders() {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[4_100_000]);
        using var graph = new Graph {
            Attachments = new object[] {
                new FileAttachmentDescriptor(path),
                new FileAttachmentDescriptor(path) {
                    ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
                    ContentId = "shared-inline"
                }
            }
        };

        try {
            graph.CreateAttachments();
            await graph.PrepareAttachments();
        } finally {
            File.Delete(path);
        }

        Assert.True(graph.IsLargerAttachment);
        Assert.Equal(2, graph.AttachmentsPlaceHolders.Count);
        Assert.Contains(graph.AttachmentsPlaceHolders, placeholder => !placeholder.Json.Contains("\"isInline\":true", StringComparison.Ordinal));
        Assert.Contains(graph.AttachmentsPlaceHolders, placeholder => placeholder.Json.Contains("\"isInline\":true", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrepareAttachments_FileInfo_CreatePlaceholders() {
        string tmp = Path.GetTempFileName();
        File.WriteAllBytes(tmp, new byte[4_100_000]);
        using var graph = new Graph { Attachments = new object[] { new FileInfo(tmp) } };

        try {
            graph.CreateAttachments();
            await graph.PrepareAttachments();
        } finally {
            File.Delete(tmp);
        }

        Assert.True(graph.IsLargerAttachment);
        var placeholder = Assert.Single(graph.AttachmentsPlaceHolders);
        Assert.Equal(Path.GetFileName(tmp), placeholder.FileName);
    }

    [Fact]
    public async Task PrepareAttachments_LargeFileDescriptor_UsesUploadSessionPath() {
        string tmp = Path.GetTempFileName();
        File.WriteAllBytes(tmp, new byte[4_100_000]);
        using var graph = new Graph {
            Attachments = new object[] {
                new FileAttachmentDescriptor(tmp) {
                    FileName = "quarterly-report.bin",
                    ContentType = "application/x-quarterly-report",
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment)
                }
            }
        };

        try {
            graph.CreateAttachments();
            await graph.PrepareAttachments();
        } finally {
            File.Delete(tmp);
        }

        Assert.True(graph.IsLargerAttachment);
        Assert.Empty(graph.ConvertedAttachments);
        var placeholder = Assert.Single(graph.AttachmentsPlaceHolders);
        Assert.Equal("quarterly-report.bin", placeholder.FileName);
        Assert.Contains("quarterly-report.bin", placeholder.Json, StringComparison.Ordinal);
        Assert.Contains("application/x-quarterly-report", placeholder.Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrepareAttachments_LargeInlineFileDescriptor_PreservesInlineMetadata() {
        string tmp = Path.GetTempFileName();
        File.WriteAllBytes(tmp, new byte[4_100_000]);
        using var graph = new Graph {
            Attachments = new object[] {
                new FileAttachmentDescriptor(tmp) {
                    FileName = "dashboard.png",
                    ContentType = "image/png",
                    ContentId = "dashboard-image",
                    ContentDisposition = new ContentDisposition(ContentDisposition.Inline)
                }
            }
        };

        try {
            graph.CreateAttachments();
            await graph.PrepareAttachments();
        } finally {
            File.Delete(tmp);
        }

        Assert.True(graph.IsLargerAttachment);
        Assert.Empty(graph.ConvertedAttachments);
        var placeholder = Assert.Single(graph.AttachmentsPlaceHolders);
        Assert.Contains("\"isInline\":true", placeholder.Json, StringComparison.Ordinal);
        Assert.Contains("\"contentId\":\"dashboard-image\"", placeholder.Json, StringComparison.Ordinal);
        Assert.Contains("\"contentType\":\"image/png\"", placeholder.Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateGraphAttachment_MissingFile_ThrowsAndLogsWarning() {
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        using var graph = new Graph();

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() => graph.CreateGraphAttachment(missing));
        Assert.Contains("Attachment file not found", exception.Message);
        Assert.Equal(missing, exception.FileName);

        var warnings = graph.LogCollector.Logs.ToArray();
        Assert.Contains(warnings, entry => entry.Type == LogType.Warning && entry.Message.IndexOf(missing, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public async Task CreateGraphAttachment_Canceled_ThrowsOperationCanceledException() {
        string tmp = Path.GetTempFileName();
        File.WriteAllBytes(tmp, new byte[1024]);
        using var graph = new Graph();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => graph.CreateGraphAttachment(tmp, cts.Token));
        } finally {
            File.Delete(tmp);
        }
    }

    [Fact]
    public async Task PrepareAttachments_MissingFile_SkipsPlaceholderAndLogs() {
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        using var graph = new Graph { Attachments = new object[] { missing } };

        await graph.PrepareAttachments();

        Assert.Empty(graph.AttachmentsPlaceHolders);
        var warnings = graph.LogCollector.Logs.ToArray();
        Assert.True(warnings.Count(entry => entry.Type == LogType.Warning && entry.Message.IndexOf(missing, StringComparison.OrdinalIgnoreCase) >= 0) >= 1);
    }

    [Fact]
    public void CreateDraft_SetsImportanceFromPriority() {
        using var graph = new Graph {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "sub",
            HTML = "body",
            ContentType = "HTML",
            Priority = MessagePriority.High
        };

        string json = graph.CreateDraft();
        Assert.Contains("\"importance\":\"high\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateDraftForMg_SetsImportanceFromPriority() {
        using var graph = new Graph {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "sub",
            HTML = "body",
            ContentType = "HTML",
            Priority = MessagePriority.Low
        };

        string json = graph.CreateDraftForMg();
        Assert.Contains("\"importance\":\"low\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DraftMessageUris_BuildUploadSessionUri() {
        string uri = GraphDraftMessageUris.CreateUploadSession("from@example.com", "draft-id");

        Assert.Equal("https://graph.microsoft.com/v1.0/users('from@example.com')/messages/draft-id/attachments/createUploadSession", uri);
    }

    [Fact]
    public void DraftMessageUris_BuildAttachmentsUri() {
        string uri = GraphDraftMessageUris.Attachments("from@example.com", "draft-id");

        Assert.Equal("https://graph.microsoft.com/v1.0/users('from@example.com')/messages/draft-id/attachments", uri);
    }

    [Fact]
    public void DraftMessageUris_BuildSendUri() {
        string uri = GraphDraftMessageUris.Send("from@example.com", "draft-id");

        Assert.Equal("https://graph.microsoft.com/v1.0/users('from@example.com')/messages/draft-id/send", uri);
    }
}
