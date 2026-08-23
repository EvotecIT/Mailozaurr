using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

public sealed partial class GmailApiClient {
    /// <summary>Lists server-side Gmail filters.</summary>
    public async Task<IReadOnlyList<GmailFilter>> ListFiltersAsync(string userId, CancellationToken cancellationToken = default) {
        var json = await SendSettingsJsonAsync(
            HttpMethod.Get,
            BuildGmailUserSegment(userId) + "/settings/filters",
            content: null,
            "filter list",
            cancellationToken).ConfigureAwait(false);
        GmailFilterListResponse? response;
        try {
            response = JsonSerializer.Deserialize(json, GmailJsonContext.Default.GmailFilterListResponse);
        } catch (JsonException ex) {
            throw new InvalidDataException("Gmail returned an invalid filter list response.", ex);
        }
        return response?.Filter ?? (IReadOnlyList<GmailFilter>)Array.Empty<GmailFilter>();
    }

    /// <summary>Gets one server-side Gmail filter.</summary>
    public Task<GmailFilter> GetFilterAsync(string userId, string filterId, CancellationToken cancellationToken = default) =>
        GetSettingsResourceAsync(
            BuildGmailUserSegment(userId) + "/settings/filters/" + EscapeGmailRequired(filterId, nameof(filterId)),
            GmailJsonContext.Default.GmailFilter,
            "filter get",
            cancellationToken);

    /// <summary>Creates a server-side Gmail filter.</summary>
    public Task<GmailFilter> CreateFilterAsync(string userId, GmailFilter filter, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (filter == null) throw new ArgumentNullException(nameof(filter));
        if (DryRun) return Task.FromResult(filter);
        return WriteSettingsResourceAsync(
            HttpMethod.Post,
            BuildGmailUserSegment(userId) + "/settings/filters",
            new GmailFilterWriteRequest { Criteria = filter.Criteria, Action = filter.Action },
            GmailJsonContext.Default.GmailFilterWriteRequest,
            GmailJsonContext.Default.GmailFilter,
            "filter create",
            cancellationToken);
    }

    /// <summary>Deletes a server-side Gmail filter.</summary>
    public async Task DeleteFilterAsync(string userId, string filterId, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        _ = EscapeGmailRequired(filterId, nameof(filterId));
        if (DryRun) return;
        _ = await SendSettingsJsonAsync(
            HttpMethod.Delete,
            BuildGmailUserSegment(userId) + "/settings/filters/" + EscapeGmailRequired(filterId, nameof(filterId)),
            content: null,
            "filter delete",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets one Gmail label.</summary>
    public Task<GmailLabel> GetLabelAsync(string userId, string labelId, CancellationToken cancellationToken = default) =>
        GetSettingsResourceAsync(
            BuildGmailUserSegment(userId) + "/labels/" + EscapeGmailRequired(labelId, nameof(labelId)),
            GmailJsonContext.Default.GmailLabel,
            "label get",
            cancellationToken);

    /// <summary>Creates a Gmail user label.</summary>
    public Task<GmailLabel> CreateLabelAsync(string userId, GmailLabel label, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (label == null) throw new ArgumentNullException(nameof(label));
        if (DryRun) return Task.FromResult(label);
        return WriteSettingsResourceAsync(
            HttpMethod.Post,
            BuildGmailUserSegment(userId) + "/labels",
            GmailLabelWriteRequest.From(label),
            GmailJsonContext.Default.GmailLabelWriteRequest,
            GmailJsonContext.Default.GmailLabel,
            "label create",
            cancellationToken);
    }

    /// <summary>Patches a Gmail user label.</summary>
    public Task<GmailLabel> UpdateLabelAsync(string userId, string labelId, GmailLabel label, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        var escapedLabelId = EscapeGmailRequired(labelId, nameof(labelId));
        if (label == null) throw new ArgumentNullException(nameof(label));
        if (DryRun) return Task.FromResult(label);
        return WriteSettingsResourceAsync(
            new HttpMethod("PATCH"),
            BuildGmailUserSegment(userId) + "/labels/" + escapedLabelId,
            GmailLabelWriteRequest.From(label),
            GmailJsonContext.Default.GmailLabelWriteRequest,
            GmailJsonContext.Default.GmailLabel,
            "label update",
            cancellationToken);
    }

    /// <summary>Deletes a Gmail user label.</summary>
    public async Task DeleteLabelAsync(string userId, string labelId, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        _ = EscapeGmailRequired(labelId, nameof(labelId));
        if (DryRun) return;
        _ = await SendSettingsJsonAsync(
            HttpMethod.Delete,
            BuildGmailUserSegment(userId) + "/labels/" + EscapeGmailRequired(labelId, nameof(labelId)),
            content: null,
            "label delete",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lists exactly one page of Gmail threads.</summary>
    public async Task<GmailThreadPage> ListThreadsPageAsync(
        string userId,
        string? query = null,
        int pageSize = 100,
        string? pageToken = null,
        CancellationToken cancellationToken = default) {
        var url = new StringBuilder(BuildGmailUserSegment(userId))
            .Append("/threads?maxResults=")
            .Append(Math.Max(1, Math.Min(pageSize, 500)));
        if (!string.IsNullOrWhiteSpace(query)) url.Append("&q=").Append(Uri.EscapeDataString(query!.Trim()));
        if (!string.IsNullOrWhiteSpace(pageToken)) url.Append("&pageToken=").Append(Uri.EscapeDataString(pageToken!.Trim()));
        var json = await SendSettingsJsonAsync(HttpMethod.Get, url.ToString(), content: null, "thread list", cancellationToken).ConfigureAwait(false);
        GmailThreadListResponse? response;
        try {
            response = JsonSerializer.Deserialize(json, GmailJsonContext.Default.GmailThreadListResponse);
        } catch (JsonException ex) {
            throw new InvalidDataException("Gmail returned an invalid thread list response.", ex);
        }
        return new GmailThreadPage {
            Threads = response?.Threads ?? new List<GmailThreadInfo>(),
            NextPageToken = string.IsNullOrWhiteSpace(response?.NextPageToken) ? null : response!.NextPageToken!.Trim()
        };
    }

    private async Task<T> GetSettingsResourceAsync<T>(
        string url,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        string operation,
        CancellationToken cancellationToken) {
        var json = await SendSettingsJsonAsync(HttpMethod.Get, url, content: null, operation, cancellationToken).ConfigureAwait(false);
        try {
            return JsonSerializer.Deserialize(json, typeInfo)
                ?? throw new InvalidDataException($"Gmail returned an empty {operation} response.");
        } catch (JsonException ex) {
            throw new InvalidDataException($"Gmail returned an invalid {operation} response.", ex);
        }
    }

    private async Task<TResponse> WriteSettingsResourceAsync<TRequest, TResponse>(
        HttpMethod method,
        string url,
        TRequest value,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TRequest> requestTypeInfo,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TResponse> responseTypeInfo,
        string operation,
        CancellationToken cancellationToken) {
        var payload = JsonSerializer.Serialize(value, requestTypeInfo);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var json = await SendSettingsJsonAsync(method, url, content, operation, cancellationToken).ConfigureAwait(false);
        try {
            return JsonSerializer.Deserialize(json, responseTypeInfo)
                ?? throw new InvalidDataException($"Gmail returned an empty {operation} response.");
        } catch (JsonException ex) {
            throw new InvalidDataException($"Gmail returned an invalid {operation} response.", ex);
        }
    }

    private async Task<string> SendSettingsJsonAsync(
        HttpMethod method,
        string url,
        HttpContent? content,
        string operation,
        CancellationToken cancellationToken) {
        ThrowIfDisposed();
        using var request = new HttpRequestMessage(method, url) { Content = content };
        using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!response.IsSuccessStatusCode) {
            throw new GmailApiException(response.StatusCode, $"Gmail {operation} failed ({(int)response.StatusCode}).", body);
        }
        return body;
    }

    private static string BuildGmailUserSegment(string userId) {
        var value = (userId ?? string.Empty).Trim();
        if (value.Length == 0 || value.Equals("me", StringComparison.OrdinalIgnoreCase)) return "users/me";
        return "users/" + Uri.EscapeDataString(value);
    }

    private static string EscapeGmailRequired(string value, string parameterName) {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{parameterName} is required.", parameterName);
        return Uri.EscapeDataString(value.Trim());
    }

    internal sealed class GmailFilterWriteRequest {
        [System.Text.Json.Serialization.JsonPropertyName("criteria")]
        public GmailFilterCriteria? Criteria { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("action")]
        public GmailFilterAction? Action { get; set; }
    }

    internal sealed class GmailLabelWriteRequest {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("messageListVisibility")]
        public string? MessageListVisibility { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("labelListVisibility")]
        public string? LabelListVisibility { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("color")]
        public GmailLabelColor? Color { get; set; }

        internal static GmailLabelWriteRequest From(GmailLabel label) => new() {
            Name = label.Name,
            MessageListVisibility = label.MessageListVisibility,
            LabelListVisibility = label.LabelListVisibility,
            Color = label.Color
        };
    }

    /// <summary>One Gmail thread provider page.</summary>
    public sealed class GmailThreadPage {
        /// <summary>Thread references returned by the provider.</summary>
        public List<GmailThreadInfo> Threads { get; set; } = new();

        /// <summary>Provider continuation token.</summary>
        public string? NextPageToken { get; set; }
    }
}
