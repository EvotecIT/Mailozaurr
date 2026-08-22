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
    /// Modifies labels on a thread.
    /// </summary>
    public async Task<GmailThread> ModifyThreadLabelsAsync(
        string userId,
        string id,
        IReadOnlyCollection<string>? addLabelIds = null,
        IReadOnlyCollection<string>? removeLabelIds = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return new GmailThread { Id = id, Messages = new List<GmailMessage>() };
        }
        var request = new GmailModifyLabelsRequest {
            AddLabelIds = addLabelIds ?? Array.Empty<string>(),
            RemoveLabelIds = removeLabelIds ?? Array.Empty<string>()
        };
        var jsonRequest = JsonSerializer.Serialize(request, GmailJsonContext.Default.GmailModifyLabelsRequest);
        using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync($"users/{userId}/threads/{id}/modify", content, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailThread? thread;
        try {
            thread = JsonSerializer.Deserialize(json, GmailJsonContext.Default.GmailThread);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API thread modify response.", json, ex);
        }
        if (thread is null) {
            throw new InvalidDataException("Gmail API returned an invalid thread modify response.");
        }
        return thread;
    }

    /// <summary>
    /// Moves a thread to trash.
    /// </summary>
    public async Task<GmailThread> TrashThreadAsync(string userId, string id, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return new GmailThread { Id = id, Messages = new List<GmailMessage>() };
        }
        using var response = await _client.PostAsync($"users/{userId}/threads/{id}/trash", content: null, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailThread? thread;
        try {
            thread = JsonSerializer.Deserialize(json, GmailJsonContext.Default.GmailThread);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API thread trash response.", json, ex);
        }
        if (thread is null) {
            throw new InvalidDataException("Gmail API returned an invalid thread trash response.");
        }
        return thread;
    }

    /// <summary>
    /// Deletes a thread by id.
    /// </summary>
    public async Task DeleteThreadAsync(string userId, string id, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return;
        }
        using var response = await _client.DeleteAsync($"users/{userId}/threads/{id}", cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Gets the Gmail profile for the specified user.
    /// </summary>
    public Task<GmailProfile> GetProfileAsync(string userId, CancellationToken cancellationToken = default) =>
        GetProfileCoreAsync(userId, refreshOnAuthenticationError: true, cancellationToken);

    /// <summary>
    /// Gets the Gmail profile without invoking the token-refresh delegate when the endpoint returns 401/403.
    /// </summary>
    /// <remarks>
    /// This is intended for optional capability probes where an insufficient-scope response is evidence and must not mutate authentication state.
    /// </remarks>
    public Task<GmailProfile> GetProfileWithoutRefreshAsync(string userId, CancellationToken cancellationToken = default) =>
        GetProfileCoreAsync(userId, refreshOnAuthenticationError: false, cancellationToken);

    private async Task<GmailProfile> GetProfileCoreAsync(
        string userId,
        bool refreshOnAuthenticationError,
        CancellationToken cancellationToken) {
        ThrowIfDisposed();
        using var response = await _client.GetAsync($"users/{userId}/profile", cancellationToken).ConfigureAwait(false);
        if (refreshOnAuthenticationError) {
            await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        } else if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden) {
#if NET5_0_OR_GREATER
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            throw new GmailAuthenticationException(response.StatusCode, errorContent);
        }
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailProfile? profile;
        try {
            profile = JsonSerializer.Deserialize(json, GmailJsonContext.Default.GmailProfile);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API profile response.", json, ex);
        }
        if (profile is null) {
            throw new InvalidDataException("Gmail API returned an invalid profile response.");
        }
        return profile;
    }

    /// <summary>
    /// Starts a Gmail push notification watch for the specified topic.
    /// </summary>
    /// <remarks>
    /// This calls <c>users.watch</c> and returns the watch response (historyId + expiration).
    /// </remarks>
    public async Task<GmailWatchResponse> WatchAsync(
        string userId,
        string topicName,
        IReadOnlyList<string>? labelIds = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(topicName)) {
            throw new ArgumentException("topicName is required.", nameof(topicName));
        }
        if (DryRun) {
            return new GmailWatchResponse { HistoryId = string.Empty, Expiration = 0 };
        }

        var request = new GmailWatchRequest { TopicName = topicName };
        if (labelIds != null && labelIds.Count > 0) {
            request.LabelIds = new List<string>(labelIds);
            request.LabelFilterAction = "include";
        }

        var body = JsonSerializer.Serialize(request, GmailJsonContext.Default.GmailWatchRequest);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync($"users/{userId}/watch", content, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailWatchResponse? watch;
        try {
            watch = JsonSerializer.Deserialize(json, GmailJsonContext.Default.GmailWatchResponse);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API watch response.", json, ex);
        }
        if (watch is null) {
            throw new InvalidDataException("Gmail API returned an invalid watch response.");
        }
        return watch;
    }

    /// <summary>
    /// Stops Gmail push notification watches for the specified user.
    /// </summary>
    /// <remarks>
    /// This calls <c>users.stop</c>.
    /// </remarks>
    public async Task StopWatchAsync(string userId, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return;
        }
        using var response = await _client.PostAsync($"users/{userId}/stop", new StringContent("{}", Encoding.UTF8, "application/json"), cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
#if NET5_0_OR_GREATER
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            throw new GmailApiException(
                response.StatusCode,
                $"Gmail users.stop failed ({(int)response.StatusCode}).",
                content);
        }
    }

    /// <summary>
    /// Lists Gmail history changes for the specified user.
    /// </summary>
    public async Task<GmailHistoryListResponse> ListHistoryAsync(
        string userId,
        string startHistoryId,
        string? labelId = null,
        IReadOnlyList<string>? historyTypes = null,
        int? maxResults = null,
        string? pageToken = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(startHistoryId)) {
            throw new ArgumentException("startHistoryId is required.", nameof(startHistoryId));
        }

        var url = new StringBuilder($"users/{userId}/history");
        var qs = new List<string> {
            $"startHistoryId={Uri.EscapeDataString(startHistoryId)}"
        };
        if (!string.IsNullOrWhiteSpace(labelId)) qs.Add($"labelId={Uri.EscapeDataString(labelId)}");
        if (maxResults.HasValue) qs.Add($"maxResults={maxResults.Value}");
        if (!string.IsNullOrWhiteSpace(pageToken)) qs.Add($"pageToken={Uri.EscapeDataString(pageToken)}");
        if (historyTypes != null) {
            for (var i = 0; i < historyTypes.Count; i++) {
                var t = historyTypes[i];
                if (!string.IsNullOrWhiteSpace(t)) {
                    qs.Add($"historyTypes={Uri.EscapeDataString(t)}");
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
        GmailHistoryListResponse? history;
        try {
            history = JsonSerializer.Deserialize(json, GmailJsonContext.Default.GmailHistoryListResponse);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API history response.", json, ex);
        }
        if (history is null) {
            throw new InvalidDataException("Gmail API returned an invalid history response.");
        }
        return history;
    }

    /// <summary>
    /// Lists threads matching the supplied query.
    /// </summary>
    public async Task<IList<GmailThreadInfo>> ListThreadsAsync(string userId, string? query = null, int? maxResults = null, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        var threads = new List<GmailThreadInfo>();
        string? pageToken = null;
        do {
            var url = new StringBuilder($"users/{userId}/threads");
            var qs = new List<string>();
            if (!string.IsNullOrWhiteSpace(query)) qs.Add($"q={Uri.EscapeDataString(query)}");
            if (maxResults.HasValue) qs.Add($"maxResults={maxResults.Value}");
            if (!string.IsNullOrEmpty(pageToken)) qs.Add($"pageToken={pageToken}");
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
            GmailThreadListResponse? list;
            try {
                list = JsonSerializer.Deserialize(json, GmailJsonContext.Default.GmailThreadListResponse);
            } catch (JsonException ex) {
                throw new GmailApiException("Failed to parse Gmail API thread list response.", json, ex);
            }
            if (list?.Threads != null) {
                threads.AddRange(list.Threads);
            }
            pageToken = list?.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return threads;
    }

    /// <summary>
    /// Retrieves a single thread by id.
    /// </summary>
    public async Task<GmailThread> GetThreadAsync(string userId, string id, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        using var response = await _client.GetAsync($"users/{userId}/threads/{id}", cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailThread? thread;
        try {
            thread = JsonSerializer.Deserialize(json, GmailJsonContext.Default.GmailThread);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API thread response.", json, ex);
        }
        if (thread is null) {
            throw new InvalidDataException("Gmail API returned an invalid thread response.");
        }
        return thread;
    }

    /// <summary>
    /// Retrieves a single thread by id, optionally requesting a partial response.
    /// </summary>
    public async Task<GmailThread> GetThreadWithOptionsAsync(
        string userId,
        string id,
        string? format = null,
        string? fields = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(id)) {
            throw new ArgumentException("id is required.", nameof(id));
        }

        var safeId = Uri.EscapeDataString(id.Trim());
        var url = new StringBuilder($"users/{userId}/threads/{safeId}");
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(format)) qs.Add($"format={Uri.EscapeDataString(format!.Trim())}");
        if (!string.IsNullOrWhiteSpace(fields)) qs.Add($"fields={Uri.EscapeDataString(fields!.Trim())}");
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
        GmailThread? thread;
        try {
            thread = JsonSerializer.Deserialize(json, GmailJsonContext.Default.GmailThread);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API thread response.", json, ex);
        }
        if (thread is null) {
            throw new InvalidDataException("Gmail API returned an invalid thread response.");
        }
        return thread;
    }

    /// <summary>
    /// Lists attachment metadata for a message.
    /// </summary>
    public async Task<IList<GmailAttachmentInfo>> ListAttachmentsAsync(string userId, string id, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        using var response = await _client.GetAsync($"users/{userId}/messages/{id}?format=full", cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var list = new List<GmailAttachmentInfo>();
        if (doc.RootElement.TryGetProperty("payload", out var payload)) {
            ExtractAttachments(payload, list);
        }
        return list;
    }

    /// <summary>
    /// Downloads a single attachment by id.
    /// </summary>
    public async Task<byte[]> DownloadAttachmentAsync(string userId, string messageId, string attachmentId, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        using var response = await _client.GetAsync($"users/{userId}/messages/{messageId}/attachments/{attachmentId}", cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        AttachmentResponse? result;
        try {
            result = JsonSerializer.Deserialize(json, GmailJsonContext.Default.AttachmentResponse);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API attachment response.", json, ex);
        }
        if (string.IsNullOrEmpty(result?.Data)) {
            return Array.Empty<byte>();
        }

        var dataProp = result!.Data!;
        var data = dataProp.Replace('-', '+').Replace('_', '/');

        if (data.Length % 4 == 1) {
            throw new InvalidDataException("Attachment data is not a valid Base64 string.");
        }

        int padding = (4 - data.Length % 4) % 4;
        if (padding > 0) {
            data = data.PadRight(data.Length + padding, '=');
        }

        try {
            return Convert.FromBase64String(data);
        } catch (FormatException ex) {
            throw new InvalidDataException("Attachment data is not a valid Base64 string.", ex);
        }
    }

    private static void ExtractAttachments(JsonElement part, List<GmailAttachmentInfo> list) {
        string? fileName = part.GetProperty("filename").GetString();
        string? mime = part.GetProperty("mimeType").GetString();
        if (!string.IsNullOrEmpty(fileName) &&
            part.TryGetProperty("body", out var body) &&
            body.TryGetProperty("attachmentId", out var idProp)) {
            list.Add(new GmailAttachmentInfo { Id = idProp.GetString(), FileName = fileName, MimeType = mime });
        }
        if (part.TryGetProperty("parts", out var parts)) {
            foreach (var p in parts.EnumerateArray()) {
                ExtractAttachments(p, list);
            }
        }
    }
}
