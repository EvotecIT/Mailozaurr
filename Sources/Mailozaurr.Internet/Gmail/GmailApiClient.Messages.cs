using MimeKit;
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

public sealed partial class GmailApiClient {
    /// <summary>
    /// Lists messages matching the supplied query.
    /// </summary>
    public Task<IList<GmailMessage>> ListAsync(string userId, string? query = null, int? maxResults = null, CancellationToken cancellationToken = default) =>
        ListAdvancedAsync(userId, query, labelIds: null, includeSpamTrash: false, maxResultsTotal: maxResults, fields: null, cancellationToken: cancellationToken);

    private static int? ClampMaxResults(int? maxResults) {
        if (!maxResults.HasValue) {
            return null;
        }
        var v = maxResults.Value;
        if (v < 1) {
            return 1;
        }
        if (v > 500) {
            return 500;
        }
        return v;
    }

    /// <summary>
    /// Lists a single page of messages.
    /// </summary>
    public async Task<GmailListResponse> ListPageAsync(
        string userId,
        string? query = null,
        IReadOnlyList<string>? labelIds = null,
        bool includeSpamTrash = false,
        int? maxResults = null,
        string? pageToken = null,
        string? fields = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        var url = new StringBuilder($"users/{userId}/messages");
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(query)) qs.Add($"q={Uri.EscapeDataString(query)}");
        var safeMax = ClampMaxResults(maxResults);
        if (safeMax.HasValue) qs.Add($"maxResults={safeMax.Value}");
        if (!string.IsNullOrWhiteSpace(pageToken)) qs.Add($"pageToken={Uri.EscapeDataString(pageToken)}");
        if (includeSpamTrash) qs.Add("includeSpamTrash=true");
        if (labelIds != null) {
            for (var i = 0; i < labelIds.Count; i++) {
                var lid = labelIds[i];
                if (!string.IsNullOrWhiteSpace(lid)) {
                    qs.Add($"labelIds={Uri.EscapeDataString(lid.Trim())}");
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(fields)) qs.Add($"fields={Uri.EscapeDataString(fields)}");
        if (qs.Count > 0) {
            url.Append('?').Append(string.Join("&", qs));
        }

        using var response = await _client.GetAsync(url.ToString(), cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailListResponse? list;
        try {
            list = JsonSerializer.Deserialize(json, GmailJsonContext.Default.GmailListResponse);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API list response.", json, ex);
        }
        if (list is null) {
            throw new InvalidDataException("Gmail API returned an invalid list response.");
        }
        return list;
    }

    /// <summary>
    /// Lists messages matching the supplied query.
    /// </summary>
    public async Task<IList<GmailMessage>> ListAdvancedAsync(
        string userId,
        string? query = null,
        IReadOnlyList<string>? labelIds = null,
        bool includeSpamTrash = false,
        int? maxResultsTotal = null,
        string? fields = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        var messages = new List<GmailMessage>();
        string? pageToken = null;
        int? remaining = maxResultsTotal;
        while (true) {
            var list = await ListPageAsync(
                userId,
                query: query,
                labelIds: labelIds,
                includeSpamTrash: includeSpamTrash,
                maxResults: remaining,
                pageToken: pageToken,
                fields: fields,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (list.Messages != null) {
                messages.AddRange(list.Messages);
            }
            if (maxResultsTotal.HasValue && messages.Count >= maxResultsTotal.Value) {
                break;
            }
            pageToken = list.NextPageToken;
            if (string.IsNullOrEmpty(pageToken)) {
                break;
            }
            if (maxResultsTotal.HasValue) {
                remaining = maxResultsTotal.Value - messages.Count;
            }
        }

        if (maxResultsTotal.HasValue && messages.Count > maxResultsTotal.Value) {
            messages = messages.GetRange(0, maxResultsTotal.Value);
        }
        return messages;
    }

    /// <summary>
    /// Retrieves a single message by id.
    /// </summary>
    public async Task<GmailMessage> GetAsync(string userId, string id, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        using var response = await _client.GetAsync($"users/{userId}/messages/{id}", cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailMessage? message;
        try {
            message = JsonSerializer.Deserialize(json, GmailJsonContext.Default.GmailMessage);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API message response.", json, ex);
        }
        if (message is null) {
            throw new InvalidDataException("Gmail API returned an invalid message response.");
        }
        return message;
    }

    /// <summary>
    /// Retrieves a single message by id, optionally requesting a specific format/fields subset and metadata headers.
    /// </summary>
    /// <remarks>
    /// This is a low-level helper for callers that need Gmail partial responses or <c>format=metadata</c>.
    /// Prefer <see cref="GetFullAsync"/> and <see cref="GetRawAsync"/> when possible.
    /// </remarks>
    public Task<GmailMessage> GetMessageWithOptionsAsync(
        string userId,
        string id,
        string? format = null,
        IReadOnlyCollection<string>? metadataHeaders = null,
        string? fields = null,
        CancellationToken cancellationToken = default)
        => GetMessageWithOptionsCoreAsync(userId, id, format, metadataHeaders, fields, cancellationToken);

    private async Task<GmailMessage> GetMessageWithOptionsCoreAsync(
        string userId,
        string id,
        string? format,
        IReadOnlyCollection<string>? metadataHeaders,
        string? fields,
        CancellationToken cancellationToken) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(id)) {
            throw new ArgumentException("id is required.", nameof(id));
        }

        var safeId = Uri.EscapeDataString(id.Trim());
        var url = new StringBuilder($"users/{userId}/messages/{safeId}");
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(format)) qs.Add($"format={Uri.EscapeDataString(format!.Trim())}");
        if (!string.IsNullOrWhiteSpace(fields)) qs.Add($"fields={Uri.EscapeDataString(fields!.Trim())}");
        if (metadataHeaders != null) {
            foreach (var h in metadataHeaders) {
                if (!string.IsNullOrWhiteSpace(h)) {
                    qs.Add($"metadataHeaders={Uri.EscapeDataString(h.Trim())}");
                }
            }
        }
        if (qs.Count > 0) {
            url.Append('?').Append(string.Join("&", qs));
        }

        using var response = await _client.GetAsync(url.ToString(), cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailMessage? message;
        try {
            message = JsonSerializer.Deserialize(json, GmailJsonContext.Default.GmailMessage);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API message response.", json, ex);
        }
        if (message is null) {
            throw new InvalidDataException("Gmail API returned an invalid message response.");
        }
        return message;
    }

    private Task<GmailMessage> GetMessageWithFormatAsync(
        string userId,
        string id,
        string format,
        string? fields,
        CancellationToken cancellationToken)
        => GetMessageWithOptionsCoreAsync(userId, id, format, metadataHeaders: null, fields: fields, cancellationToken);

    /// <summary>
    /// Retrieves a MIME message by id.
    /// </summary>
    public async Task<MimeMessage> GetMimeMessageAsync(string userId, string id, CancellationToken cancellationToken = default) {
        var msg = await GetMessageWithFormatAsync(userId, id, format: "raw", fields: null, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(msg.Raw)) {
            throw new InvalidDataException("Gmail API returned an invalid message response.");
        }
        var raw = msg.Raw!;
        var data = raw.Replace('-', '+').Replace('_', '/');
        int padding = (4 - data.Length % 4) % 4;
        if (padding > 0) data = data.PadRight(data.Length + padding, '=');
        var bytes = Convert.FromBase64String(data);
        using var ms = new MemoryStream(bytes);
        return MimeMessage.Load(ms);
    }

    /// <summary>
    /// Retrieves a raw message (base64url MIME) by id.
    /// </summary>
    public Task<GmailMessage> GetRawAsync(string userId, string id, string? fields = null, CancellationToken cancellationToken = default) =>
        GetMessageWithFormatAsync(userId, id, format: "raw", fields: fields, cancellationToken: cancellationToken);

    /// <summary>
    /// Retrieves a raw message while bounding the encoded HTTP response before it is materialized.
    /// </summary>
    public async Task<GmailMessage> GetRawBoundedAsync(
        string userId,
        string id,
        long maxDecodedBytes,
        string? fields = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(id)) {
            throw new ArgumentException("id is required.", nameof(id));
        }
        if (maxDecodedBytes <= 0 || maxDecodedBytes > int.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(maxDecodedBytes));
        }

        var safeId = Uri.EscapeDataString(id.Trim());
        var url = new StringBuilder($"users/{userId}/messages/{safeId}?format=raw");
        if (!string.IsNullOrWhiteSpace(fields)) {
            url.Append("&fields=").Append(Uri.EscapeDataString(fields!.Trim()));
        }

        // Base64 uses at most four encoded bytes for every three decoded bytes.
        // The fixed allowance covers the small JSON envelope and message id.
        var maxEncodedBytes = checked(((maxDecodedBytes + 2L) / 3L) * 4L);
        var maxResponseBytes = checked(maxEncodedBytes + 64L * 1024L);
        if (maxResponseBytes > int.MaxValue) {
            maxResponseBytes = int.MaxValue;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url.ToString());
        using var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) {
            throw new GmailApiException(
                response.StatusCode,
                $"Gmail message '{id.Trim()}' was not found.",
                string.Empty);
        }
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long contentLength && contentLength > maxResponseBytes) {
            throw new InvalidDataException($"Gmail raw response exceeds the bounded response size for {maxDecodedBytes} MIME bytes.");
        }

#if NET5_0_OR_GREATER
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
        using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
#if NET5_0_OR_GREATER
            var read = await responseStream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
#else
            var read = await responseStream.ReadAsync(chunk, 0, chunk.Length, cancellationToken).ConfigureAwait(false);
#endif
            if (read <= 0) break;
            if (buffer.Length + read > maxResponseBytes) {
                throw new InvalidDataException($"Gmail raw response exceeds the bounded response size for {maxDecodedBytes} MIME bytes.");
            }
            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        GmailMessage? message;
        try {
            message = await JsonSerializer.DeserializeAsync(
                buffer,
                GmailJsonContext.Default.GmailMessage,
                cancellationToken).ConfigureAwait(false);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API raw-message response.", string.Empty, ex);
        }
        return message ?? throw new InvalidDataException("Gmail API returned an invalid raw-message response.");
    }

    /// <summary>
    /// Retrieves a full message by id.
    /// </summary>
    public Task<GmailMessage> GetFullAsync(string userId, string id, string? fields = null, CancellationToken cancellationToken = default) =>
        GetMessageWithFormatAsync(userId, id, format: "full", fields: fields, cancellationToken: cancellationToken);

    /// <summary>
    /// Imports a message (MIME base64url) into a mailbox.
    /// </summary>
    /// <remarks>
    /// This calls <c>users.messages.import</c>. It is typically used to append a sent copy into Gmail.
    /// </remarks>
    public async Task<GmailMessage> ImportAsync(
        string userId,
        string raw,
        IReadOnlyCollection<string>? labelIds = null,
        string internalDateSource = "dateHeader",
        bool neverMarkSpam = true,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(raw)) {
            throw new ArgumentException("raw is required.", nameof(raw));
        }
        if (DryRun) {
            return new GmailMessage { Id = string.Empty, ThreadId = string.Empty };
        }

        var url = new StringBuilder($"users/{userId}/messages/import");
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(internalDateSource)) qs.Add($"internalDateSource={Uri.EscapeDataString(internalDateSource.Trim())}");
        qs.Add($"neverMarkSpam={(neverMarkSpam ? "true" : "false")}");
        if (qs.Count > 0) {
            url.Append('?').Append(string.Join("&", qs));
        }

        var request = new GmailImportMessageRequest {
            Raw = raw.Trim(),
            LabelIds = labelIds is null || labelIds.Count == 0 ? null : new List<string>(labelIds)
        };
        var jsonRequest = JsonSerializer.Serialize(request, GmailJsonContext.Default.GmailImportMessageRequest);
        using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync(url.ToString(), content, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailMessage? message;
        try {
            message = JsonSerializer.Deserialize(json, GmailJsonContext.Default.GmailMessage);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API import response.", json, ex);
        }
        if (message is null) {
            throw new InvalidDataException("Gmail API returned an invalid import response.");
        }
        return message;
    }

    /// <summary>
    /// Deletes a message by id.
    /// </summary>
    public async Task DeleteAsync(string userId, string id, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return;
        }
        using var response = await _client.DeleteAsync($"users/{userId}/messages/{id}", cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Moves a message to trash.
    /// </summary>
    public async Task<GmailMessage> TrashMessageAsync(string userId, string id, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return new GmailMessage { Id = id, ThreadId = string.Empty };
        }
        using var response = await _client.PostAsync($"users/{userId}/messages/{id}/trash", content: null, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailMessage? message;
        try {
            message = JsonSerializer.Deserialize(json, GmailJsonContext.Default.GmailMessage);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API trash response.", json, ex);
        }
        if (message is null) {
            throw new InvalidDataException("Gmail API returned an invalid trash response.");
        }
        return message;
    }

    /// <summary>
    /// Modifies labels on a single message.
    /// </summary>
    public async Task<GmailMessage> ModifyMessageLabelsAsync(
        string userId,
        string id,
        IReadOnlyCollection<string>? addLabelIds = null,
        IReadOnlyCollection<string>? removeLabelIds = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return new GmailMessage { Id = id, ThreadId = string.Empty };
        }
        var request = new GmailModifyLabelsRequest {
            AddLabelIds = addLabelIds ?? Array.Empty<string>(),
            RemoveLabelIds = removeLabelIds ?? Array.Empty<string>()
        };
        var jsonRequest = JsonSerializer.Serialize(request, GmailJsonContext.Default.GmailModifyLabelsRequest);
        using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync($"users/{userId}/messages/{id}/modify", content, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailMessage? message;
        try {
            message = JsonSerializer.Deserialize(json, GmailJsonContext.Default.GmailMessage);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API message modify response.", json, ex);
        }
        if (message is null) {
            throw new InvalidDataException("Gmail API returned an invalid message modify response.");
        }
        return message;
    }

    /// <summary>
    /// Modifies labels on multiple messages.
    /// </summary>
    public async Task BatchModifyMessagesAsync(
        string userId,
        IReadOnlyCollection<string> ids,
        IReadOnlyCollection<string>? addLabelIds = null,
        IReadOnlyCollection<string>? removeLabelIds = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return;
        }
        if (ids == null) {
            throw new ArgumentNullException(nameof(ids));
        }
        if (ids.Count == 0) {
            return;
        }
        var request = new GmailBatchModifyRequest {
            Ids = ids,
            AddLabelIds = addLabelIds ?? Array.Empty<string>(),
            RemoveLabelIds = removeLabelIds ?? Array.Empty<string>()
        };
        var jsonRequest = JsonSerializer.Serialize(request, GmailJsonContext.Default.GmailBatchModifyRequest);
        using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync($"users/{userId}/messages/batchModify", content, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Deletes multiple messages.
    /// </summary>
    public async Task BatchDeleteMessagesAsync(string userId, IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return;
        }
        if (ids == null) {
            throw new ArgumentNullException(nameof(ids));
        }
        if (ids.Count == 0) {
            return;
        }
        var request = new GmailBatchDeleteRequest { Ids = ids };
        var jsonRequest = JsonSerializer.Serialize(request, GmailJsonContext.Default.GmailBatchDeleteRequest);
        using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync($"users/{userId}/messages/batchDelete", content, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Lists labels for the specified user.
    /// </summary>
    public async Task<IList<GmailLabel>> ListLabelsAsync(string userId, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        using var response = await _client.GetAsync(
            BuildGmailUserSegment(userId) + "/labels?fields=labels(id,name,type,messageListVisibility,labelListVisibility,messagesTotal,messagesUnread,threadsTotal,threadsUnread,color)",
            cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailLabelListResponse? list;
        try {
            list = JsonSerializer.Deserialize(json, GmailJsonContext.Default.GmailLabelListResponse);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API labels response.", json, ex);
        }
        return (IList<GmailLabel>)(list?.Labels ?? new List<GmailLabel>());
    }
}
