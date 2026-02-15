using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Uploads Graph attachments via upload sessions.
/// </summary>
public static class GraphLargeAttachmentUploader {
    // Must be a multiple of 320 KiB (except last chunk).
    private const int ChunkSize = 327_680 * 32; // 10 MiB

    /// <summary>
    /// Uploads decoded attachments to an existing draft message.
    /// </summary>
    /// <returns>Error text when upload fails; otherwise null.</returns>
    public static async Task<string?> UploadAsync(
        HttpClient client,
        string accessToken,
        string messageId,
        IReadOnlyList<DecodedMimeAttachment> attachments,
        CancellationToken cancellationToken = default) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (string.IsNullOrWhiteSpace(accessToken)) {
            throw new ArgumentException("accessToken is required.", nameof(accessToken));
        }
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        if (attachments == null) {
            throw new ArgumentNullException(nameof(attachments));
        }

        foreach (var attachment in attachments) {
            if (attachment == null) {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var uploadUrl = await CreateUploadSessionAsync(client, accessToken, messageId, attachment, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(uploadUrl)) {
                return "Graph upload session creation failed (empty uploadUrl).";
            }

            var uploadError = await UploadToSessionAsync(client, uploadUrl!, attachment, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(uploadError)) {
                return uploadError;
            }
        }

        return null;
    }

    private static async Task<string?> CreateUploadSessionAsync(
        HttpClient client,
        string accessToken,
        string messageId,
        DecodedMimeAttachment attachment,
        CancellationToken cancellationToken) {
        var url = "https://graph.microsoft.com/v1.0/me/messages/" + Uri.EscapeDataString(messageId) + "/attachments/createUploadSession";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Content = new StringContent(BuildUploadSessionRequestJson(attachment), Encoding.UTF8, "application/json");

        using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            var trimmed = (body ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (trimmed.Length > 500) {
                trimmed = trimmed.Substring(0, 500) + "...";
            }
            return $"Graph upload session creation failed (HTTP {((int)resp.StatusCode).ToString(CultureInfo.InvariantCulture)}): {trimmed}";
        }

        try {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("uploadUrl", out var uploadUrlEl)) {
                var uploadUrl = uploadUrlEl.GetString();
                var uploadUrlTrimmed = uploadUrl == null ? null : uploadUrl.Trim();
                return string.IsNullOrWhiteSpace(uploadUrlTrimmed) ? null : uploadUrlTrimmed;
            }
        } catch {
            // ignore parse errors, caller gets null
        }

        return null;
    }

    private static string BuildUploadSessionRequestJson(DecodedMimeAttachment attachment) {
        static string JsonString(string value) =>
            "\"" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        var sb = new StringBuilder();
        sb.Append("{\"attachmentItem\":{");
        sb.Append("\"attachmentType\":\"file\",");
        sb.Append("\"name\":").Append(JsonString(attachment.Name)).Append(",");
        sb.Append("\"size\":").Append(attachment.Length.ToString(CultureInfo.InvariantCulture));
        var contentType = attachment.ContentType;
        if (contentType != null && contentType.Trim().Length > 0) {
            sb.Append(",\"contentType\":").Append(JsonString(contentType));
        }
        if (attachment.IsInline) {
            sb.Append(",\"isInline\":true");
            var contentId = attachment.ContentId;
            if (contentId != null && contentId.Trim().Length > 0) {
                sb.Append(",\"contentId\":").Append(JsonString(contentId));
            }
        }
        sb.Append("}}");
        return sb.ToString();
    }

    private static async Task<string?> UploadToSessionAsync(
        HttpClient client,
        string uploadUrl,
        DecodedMimeAttachment attachment,
        CancellationToken cancellationToken) {
        if (attachment.Length <= 0) {
            return $"Graph upload failed: attachment '{attachment.Name}' length is {attachment.Length.ToString(CultureInfo.InvariantCulture)}.";
        }

        long offset = 0;
        using var stream = attachment.OpenRead();
        if (!stream.CanSeek) {
            return $"Graph upload failed: attachment '{attachment.Name}' stream is not seekable.";
        }

        while (offset < attachment.Length) {
            cancellationToken.ThrowIfCancellationRequested();

            var remaining = attachment.Length - offset;
            var chunkLen = (int)Math.Min(ChunkSize, remaining);
            var buffer = new byte[chunkLen];
            var read = 0;
            while (read < chunkLen) {
#if NET5_0_OR_GREATER
                var n = await stream.ReadAsync(buffer.AsMemory(read, chunkLen - read), cancellationToken).ConfigureAwait(false);
#else
                var n = await stream.ReadAsync(buffer, read, chunkLen - read, cancellationToken).ConfigureAwait(false);
#endif
                if (n <= 0) {
                    break;
                }
                read += n;
            }

            if (read <= 0) {
                return $"Graph upload failed: unexpected end of stream for '{attachment.Name}' at {offset.ToString(CultureInfo.InvariantCulture)}.";
            }

            var start = offset;
            var end = offset + read - 1;
            using var req = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
            req.Content = new ByteArrayContent(buffer, 0, read);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            req.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, attachment.Length);

            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode || (int)resp.StatusCode == 202) {
                offset += read;
                continue;
            }

#if NET5_0_OR_GREATER
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            var trimmed = (body ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (trimmed.Length > 500) {
                trimmed = trimmed.Substring(0, 500) + "...";
            }
            return $"Graph upload failed (HTTP {(int)resp.StatusCode}) for '{attachment.Name}': {trimmed}";
        }

        return null;
    }
}
