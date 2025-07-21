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
public sealed class GmailApiClient {
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes the client using the provided OAuth credential.
    /// </summary>
    public GmailApiClient(OAuthCredential credential) {
        _client = new HttpClient {
            BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/")
        };
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);
    }

    internal GmailApiClient(HttpClient client) {
        _client = client;
    }

    private static async Task ThrowIfAuthErrorAsync(HttpResponseMessage response) {
        if (response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden) {
            string content = await response.Content.ReadAsStringAsync();
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
        await ThrowIfAuthErrorAsync(response);
        response.EnsureSuccessStatusCode();
        var resultJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GmailMessage>(resultJson, s_jsonOptions)!;
    }

    /// <summary>
    /// Lists messages matching the supplied query.
    /// </summary>
    public async Task<IList<GmailMessage>> ListAsync(string userId, string? query = null, int? maxResults = null, CancellationToken cancellationToken = default) {
        var url = new StringBuilder($"users/{userId}/messages");
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(query)) qs.Add($"q={Uri.EscapeDataString(query)}");
        if (maxResults.HasValue) qs.Add($"maxResults={maxResults.Value}");
        if (qs.Count > 0) {
            url.Append('?').Append(string.Join("&", qs));
        }
        using var response = await _client.GetAsync(url.ToString(), cancellationToken);
        await ThrowIfAuthErrorAsync(response);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize<GmailListResponse>(json, s_jsonOptions);
        return list?.Messages ?? new List<GmailMessage>();
    }

    /// <summary>
    /// Retrieves a single message by id.
    /// </summary>
    public async Task<GmailMessage> GetAsync(string userId, string id, CancellationToken cancellationToken = default) {
        using var response = await _client.GetAsync($"users/{userId}/messages/{id}", cancellationToken);
        await ThrowIfAuthErrorAsync(response);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GmailMessage>(json, s_jsonOptions)!;
    }

    /// <summary>
    /// Deletes a message by id.
    /// </summary>
    public async Task DeleteAsync(string userId, string id, CancellationToken cancellationToken = default) {
        using var response = await _client.DeleteAsync($"users/{userId}/messages/{id}", cancellationToken);
        await ThrowIfAuthErrorAsync(response);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Lists attachment metadata for a message.
    /// </summary>
    public async Task<IList<GmailAttachmentInfo>> ListAttachmentsAsync(string userId, string id, CancellationToken cancellationToken = default) {
        using var response = await _client.GetAsync($"users/{userId}/messages/{id}?format=full", cancellationToken);
        await ThrowIfAuthErrorAsync(response);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync();
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
        await ThrowIfAuthErrorAsync(response);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AttachmentResponse>(json, s_jsonOptions)!;
        var data = result.Data?.Replace('-', '+').Replace('_', '/') ?? string.Empty;
        int padding = (4 - data.Length % 4) % 4;
        if (padding > 0) data = data.PadRight(data.Length + padding, '=');
        return Convert.FromBase64String(data);
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
    }
}
