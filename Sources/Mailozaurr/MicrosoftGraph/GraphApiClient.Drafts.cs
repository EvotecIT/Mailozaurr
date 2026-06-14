using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

public sealed partial class GraphApiClient {
    /// <summary>
    /// Creates a message draft, optionally under a specific folder.
    /// </summary>
    public async Task<GraphMessage> CreateMessageAsync(
        GraphMessage message,
        string userId = "me",
        string? folderIdOrWellKnownName = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }

        var userSegment = BuildUserSegment(userId);
        string? trimmedFolderId = null;
        if (folderIdOrWellKnownName != null) {
            var candidate = folderIdOrWellKnownName.Trim();
            if (candidate.Length > 0) {
                trimmedFolderId = candidate;
            }
        }
        var url = trimmedFolderId == null
            ? userSegment + "/messages"
            : userSegment + "/mailFolders/" + Uri.EscapeDataString(trimmedFolderId) + "/messages";
        var payload = JsonSerializer.Serialize(message, MailozaurrJsonContext.Default.GraphMessage);
        using var req = new HttpRequestMessage(HttpMethod.Post, url) {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph message create failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }

        GraphMessage? created;
        try {
            created = JsonSerializer.Deserialize(body, MailozaurrJsonContext.Default.GraphMessage);
        } catch (JsonException ex) {
            throw new InvalidDataException("Failed to parse Graph message create response.", ex);
        }
        if (created == null || string.IsNullOrWhiteSpace(created.Id)) {
            throw new InvalidDataException("Graph returned an invalid created message response.");
        }
        return created;
    }

    /// <summary>
    /// Sends an existing draft message.
    /// </summary>
    public async Task SendDraftMessageAsync(
        string messageId,
        string userId = "me",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }

        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(messageId.Trim());
        using var req = new HttpRequestMessage(HttpMethod.Post, userSegment + "/messages/" + selector + "/send");
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph draft send failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }
    }

    /// <summary>
    /// Creates attachment upload session for an existing draft message.
    /// </summary>
    public async Task<GraphUploadSessionResult> CreateAttachmentUploadSessionAsync(
        string messageId,
        GraphAttachmentItem attachmentItem,
        string userId = "me",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        if (attachmentItem == null) {
            throw new ArgumentNullException(nameof(attachmentItem));
        }
        if (string.IsNullOrWhiteSpace(attachmentItem.Name)) {
            throw new ArgumentException("attachmentItem.Name is required.", nameof(attachmentItem));
        }
        if (attachmentItem.Size <= 0) {
            throw new ArgumentOutOfRangeException(nameof(attachmentItem), "attachmentItem.Size must be > 0.");
        }

        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(messageId.Trim());
        var url = userSegment + "/messages/" + selector + "/attachments/createUploadSession";

        var payload = BuildCreateUploadSessionPayload(attachmentItem);
        using var req = new HttpRequestMessage(HttpMethod.Post, url) {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph upload session create failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }

        GraphUploadSessionResult? result;
        try {
            result = JsonSerializer.Deserialize(body, MailozaurrJsonContext.Default.GraphUploadSessionResult);
        } catch (JsonException ex) {
            throw new InvalidDataException("Failed to parse Graph upload session response.", ex);
        }
        if (result == null || string.IsNullOrWhiteSpace(result.UploadUrl)) {
            throw new InvalidDataException("Graph returned an invalid upload session response.");
        }
        return result;
    }

    /// <summary>
    /// Uploads one attachment chunk to a Graph upload session URL.
    /// </summary>
    public async Task UploadAttachmentChunkAsync(
        string uploadUrl,
        byte[] chunk,
        long startInclusive,
        long endInclusive,
        long totalLength,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(uploadUrl)) {
            throw new ArgumentException("uploadUrl is required.", nameof(uploadUrl));
        }
        if (chunk == null) {
            throw new ArgumentNullException(nameof(chunk));
        }
        if (chunk.Length == 0) {
            throw new ArgumentException("chunk must not be empty.", nameof(chunk));
        }

        using var req = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
        req.Content = new ByteArrayContent(chunk);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        req.Content.Headers.ContentRange = new ContentRangeHeaderValue(startInclusive, endInclusive, totalLength);

        using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (resp.IsSuccessStatusCode || (int)resp.StatusCode == 202) {
            return;
        }

        throw new GraphApiException(resp.StatusCode, $"Graph attachment chunk upload failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
    }

    private static string BuildCreateUploadSessionPayload(GraphAttachmentItem attachmentItem) {
        static string JsonString(string value) =>
            "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        var sb = new StringBuilder();
        sb.Append("{\"attachmentItem\":{");
        sb.Append("\"attachmentType\":").Append(JsonString(string.IsNullOrWhiteSpace(attachmentItem.AttachmentType) ? "file" : attachmentItem.AttachmentType.Trim())).Append(',');
        sb.Append("\"name\":").Append(JsonString(attachmentItem.Name.Trim())).Append(',');
        sb.Append("\"size\":").Append(attachmentItem.Size.ToString(CultureInfo.InvariantCulture));
        string? contentType = null;
        if (attachmentItem.ContentType != null) {
            var candidate = attachmentItem.ContentType.Trim();
            if (candidate.Length > 0) {
                contentType = candidate;
            }
        }
        if (contentType != null) {
            sb.Append(",\"contentType\":").Append(JsonString(contentType));
        }
        if (attachmentItem.IsInline.HasValue && attachmentItem.IsInline.Value) {
            sb.Append(",\"isInline\":true");
            string? contentId = null;
            if (attachmentItem.ContentId != null) {
                var candidate = attachmentItem.ContentId.Trim();
                if (candidate.Length > 0) {
                    contentId = candidate;
                }
            }
            if (contentId != null) {
                sb.Append(",\"contentId\":").Append(JsonString(contentId));
            }
        }
        sb.Append("}}");
        return sb.ToString();
    }
}