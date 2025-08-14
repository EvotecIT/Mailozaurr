using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MimeKit;

namespace Mailozaurr;

/// <summary>
/// Lightweight client for sending and retrieving messages using Gmail REST API.
/// </summary>
public sealed class GmailApiClient : IDisposable {
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;
    private readonly Func<CancellationToken, Task<string>>? _refreshToken;
    private bool _disposed;

    /// <summary>
    /// Initializes the client using the provided OAuth credential.
    /// </summary>
    public GmailApiClient(OAuthCredential credential, Func<CancellationToken, Task<string>>? refreshToken = null) {
        _client = new HttpClient {
            BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/")
        };
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);
        _refreshToken = refreshToken;
    }

    internal GmailApiClient(HttpClient client, Func<CancellationToken, Task<string>>? refreshToken = null) {
        _client = client;
        _refreshToken = refreshToken;
    }

    /// <inheritdoc />
    public void Dispose() {
        if (_disposed) {
            return;
        }

        _client.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private async Task ThrowIfAuthErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken) {
        if (response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden) {
            if (_refreshToken != null) {
                string token = await _refreshToken(cancellationToken);
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
#if NET5_0_OR_GREATER
            string content = await response.Content.ReadAsStringAsync(cancellationToken);
#else
            string content = await response.Content.ReadAsStringAsync();
#endif
            throw new GmailAuthenticationException(response.StatusCode, content);
        }
    }

    /// <summary>
    /// Sends the specified MIME message via Gmail API.
    /// </summary>
    public async Task<GmailMessage> SendAsync(string userId, MimeMessage message, CancellationToken cancellationToken = default) {
        using var ms = new MemoryStream();
        await message.WriteToAsync(ms, cancellationToken);
        var raw = Convert.ToBase64String(ms.ToArray())
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", string.Empty);
        var json = JsonSerializer.Serialize(new { raw }, s_jsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync($"users/{userId}/messages/send", content, cancellationToken);
        await ThrowIfAuthErrorAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var resultJson = await response.Content.ReadAsStringAsync(cancellationToken);
#else
        var resultJson = await response.Content.ReadAsStringAsync();
#endif
        var result = JsonSerializer.Deserialize<GmailMessage>(resultJson, s_jsonOptions);
        if (result is null) {
            throw new InvalidDataException("Gmail API returned an invalid send response.");
        }
        return result;
    }

    /// <summary>
    /// Lists messages matching the supplied query.
    /// </summary>
    public async Task<IList<GmailMessage>> ListAsync(string userId, string? query = null, int? maxResults = null, CancellationToken cancellationToken = default) {
        var messages = new List<GmailMessage>();
        string? pageToken = null;
        do {
            var url = new StringBuilder($"users/{userId}/messages");
            var qs = new List<string>();
            if (!string.IsNullOrWhiteSpace(query)) qs.Add($"q={Uri.EscapeDataString(query)}");
            if (maxResults.HasValue) qs.Add($"maxResults={maxResults.Value}");
            if (!string.IsNullOrEmpty(pageToken)) qs.Add($"pageToken={pageToken}");
            if (qs.Count > 0) {
                url.Append('?').Append(string.Join("&", qs));
            }
            using var response = await _client.GetAsync(url.ToString(), cancellationToken);
            await ThrowIfAuthErrorAsync(response, cancellationToken);
            response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
#else
            var json = await response.Content.ReadAsStringAsync();
#endif
            var list = JsonSerializer.Deserialize<GmailListResponse>(json, s_jsonOptions);
            if (list?.Messages != null) {
                messages.AddRange(list.Messages);
            }
            pageToken = list?.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return messages;
    }

    /// <summary>
    /// Retrieves a single message by id.
    /// </summary>
    public async Task<GmailMessage> GetAsync(string userId, string id, CancellationToken cancellationToken = default) {
        using var response = await _client.GetAsync($"users/{userId}/messages/{id}", cancellationToken);
        await ThrowIfAuthErrorAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
#else
        var json = await response.Content.ReadAsStringAsync();
#endif
        var message = JsonSerializer.Deserialize<GmailMessage>(json, s_jsonOptions);
        if (message is null) {
            throw new InvalidDataException("Gmail API returned an invalid message response.");
        }
        return message;
    }

    /// <summary>
    /// Deletes a message by id.
    /// </summary>
    public async Task DeleteAsync(string userId, string id, CancellationToken cancellationToken = default) {
        using var response = await _client.DeleteAsync($"users/{userId}/messages/{id}", cancellationToken);
        await ThrowIfAuthErrorAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Lists threads matching the supplied query.
    /// </summary>
    public async Task<IList<GmailThreadInfo>> ListThreadsAsync(string userId, string? query = null, int? maxResults = null, CancellationToken cancellationToken = default) {
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
            using var response = await _client.GetAsync(url.ToString(), cancellationToken);
            await ThrowIfAuthErrorAsync(response, cancellationToken);
            response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
#else
            var json = await response.Content.ReadAsStringAsync();
#endif
            var list = JsonSerializer.Deserialize<GmailThreadListResponse>(json, s_jsonOptions);
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
        using var response = await _client.GetAsync($"users/{userId}/threads/{id}", cancellationToken);
        await ThrowIfAuthErrorAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
#else
        var json = await response.Content.ReadAsStringAsync();
#endif
        var thread = JsonSerializer.Deserialize<GmailThread>(json, s_jsonOptions);
        if (thread is null) {
            throw new InvalidDataException("Gmail API returned an invalid thread response.");
        }
        return thread;
    }

    /// <summary>
    /// Lists attachment metadata for a message.
    /// </summary>
    public async Task<IList<GmailAttachmentInfo>> ListAttachmentsAsync(string userId, string id, CancellationToken cancellationToken = default) {
        using var response = await _client.GetAsync($"users/{userId}/messages/{id}?format=full", cancellationToken);
        await ThrowIfAuthErrorAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
#else
        using var stream = await response.Content.ReadAsStreamAsync();
#endif
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
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
        using var response = await _client.GetAsync($"users/{userId}/messages/{messageId}/attachments/{attachmentId}", cancellationToken);
        await ThrowIfAuthErrorAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
#else
        var json = await response.Content.ReadAsStringAsync();
#endif
        var result = JsonSerializer.Deserialize<AttachmentResponse>(json, s_jsonOptions);
        if (string.IsNullOrEmpty(result?.Data)) {
            return Array.Empty<byte>();
        }

        var data = result.Data.Replace('-', '+').Replace('_', '/');

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

    private sealed class AttachmentResponse {
        /// <summary>Base64 encoded attachment data.</summary>
        public string? Data { get; set; }
    }

    private sealed class GmailListResponse {
        /// <summary>Messages returned by the API.</summary>
        public List<GmailMessage>? Messages { get; set; }
        /// <summary>Token for the next page of results.</summary>
        public string? NextPageToken { get; set; }
    }

    private sealed class GmailThreadListResponse {
        /// <summary>Threads returned by the API.</summary>
        public List<GmailThreadInfo>? Threads { get; set; }
        /// <summary>Token for the next page of results.</summary>
        public string? NextPageToken { get; set; }
    }
}
