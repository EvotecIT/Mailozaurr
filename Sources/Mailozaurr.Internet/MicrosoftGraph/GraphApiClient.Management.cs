using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

public sealed partial class GraphApiClient {
    /// <summary>Default bounded event projection used by provider-neutral surfaces.</summary>
    public const string DefaultEventSelect = "id,subject,start,end,body,attendees";

    /// <summary>Lists inbox rules for a mailbox.</summary>
    public async Task<IReadOnlyList<GraphInboxRule>> ListInboxRulesAsync(
        string userId = "me",
        string? filter = null,
        int top = 100,
        int maxPages = 25,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        var userSegment = BuildUserSegment(userId);
        var url = new StringBuilder(userSegment)
            .Append("/mailFolders/inbox/messageRules?$top=")
            .Append(ClampInt(top, 1, 999).ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(filter)) {
            url.Append("&$filter=").Append(Uri.EscapeDataString(filter!.Trim()));
        }

        var output = new List<GraphInboxRule>();
        var resultLimit = ClampInt(top, 1, 999);
        var next = url.ToString();
        var expectedPath = new Uri(_client.BaseAddress ?? new Uri("https://graph.microsoft.com/v1.0/"), userSegment + "/mailFolders/inbox/messageRules").AbsolutePath;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var page = 0; page < ClampInt(maxPages, 1, 500) && !string.IsNullOrWhiteSpace(next); page++) {
            if (!seen.Add(next!)) throw new InvalidDataException("Graph inbox-rule pagination returned a repeated continuation URL.");
            var response = await SendJsonAsync(HttpMethod.Get, next!, content: null, "inbox-rule list", cancellationToken).ConfigureAwait(false);
            GraphInboxRuleListResponse? envelope;
            try {
                envelope = JsonSerializer.Deserialize(response, GraphJsonContext.Default.GraphInboxRuleListResponse);
            } catch (JsonException ex) {
                throw new InvalidDataException("Graph returned an invalid inbox-rule list response.", ex);
            }
            if (envelope?.Value != null) output.AddRange(envelope.Value);
            if (output.Count >= resultLimit) return output.Take(resultLimit).ToArray();
            next = NormalizeContinuation(envelope?.NextLink, expectedPath);
        }
        if (!string.IsNullOrWhiteSpace(next)) throw new InvalidDataException("Graph inbox-rule listing exceeded the configured page bound.");
        return output;
    }

    /// <summary>Gets one inbox rule.</summary>
    public Task<GraphInboxRule> GetInboxRuleAsync(string ruleId, string userId = "me", CancellationToken cancellationToken = default) =>
        GetResourceAsync(
            BuildUserSegment(userId) + "/mailFolders/inbox/messageRules/" + EscapeRequired(ruleId, nameof(ruleId)),
            GraphJsonContext.Default.GraphInboxRule,
            "inbox-rule get",
            cancellationToken);

    /// <summary>Creates an inbox rule.</summary>
    public Task<GraphInboxRule> CreateInboxRuleAsync(GraphInboxRule rule, string userId = "me", CancellationToken cancellationToken = default) =>
        WriteResourceAsync(
            HttpMethod.Post,
            BuildUserSegment(userId) + "/mailFolders/inbox/messageRules",
            GraphInboxRuleWriteRequest.From(rule ?? throw new ArgumentNullException(nameof(rule))),
            GraphJsonContext.Default.GraphInboxRuleWriteRequest,
            GraphJsonContext.Default.GraphInboxRule,
            "inbox-rule create",
            cancellationToken);

    /// <summary>Updates an inbox rule.</summary>
    public Task<GraphInboxRule> UpdateInboxRuleAsync(string ruleId, GraphInboxRule rule, string userId = "me", CancellationToken cancellationToken = default) =>
        WriteResourceAsync(
            new HttpMethod("PATCH"),
            BuildUserSegment(userId) + "/mailFolders/inbox/messageRules/" + EscapeRequired(ruleId, nameof(ruleId)),
            GraphInboxRuleWriteRequest.From(rule ?? throw new ArgumentNullException(nameof(rule))),
            GraphJsonContext.Default.GraphInboxRuleWriteRequest,
            GraphJsonContext.Default.GraphInboxRule,
            "inbox-rule update",
            cancellationToken);

    /// <summary>Deletes an inbox rule.</summary>
    public Task DeleteInboxRuleAsync(string ruleId, string userId = "me", CancellationToken cancellationToken = default) =>
        DeleteResourceAsync(
            BuildUserSegment(userId) + "/mailFolders/inbox/messageRules/" + EscapeRequired(ruleId, nameof(ruleId)),
            "inbox-rule delete",
            cancellationToken);

    /// <summary>Lists calendar events for a mailbox.</summary>
    public async Task<IReadOnlyList<GraphEvent>> ListEventsAsync(
        string userId = "me",
        string? filter = null,
        string? select = DefaultEventSelect,
        int top = 100,
        int maxPages = 25,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        var url = new StringBuilder(BuildUserSegment(userId))
            .Append("/events?$top=")
            .Append(ClampInt(top, 1, 999).ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(filter)) url.Append("&$filter=").Append(Uri.EscapeDataString(filter!.Trim()));
        if (!string.IsNullOrWhiteSpace(select)) url.Append("&$select=").Append(Uri.EscapeDataString(select!.Trim()));

        var output = new List<GraphEvent>();
        var resultLimit = ClampInt(top, 1, 999);
        var next = url.ToString();
        var expectedPath = new Uri(_client.BaseAddress ?? new Uri("https://graph.microsoft.com/v1.0/"), BuildUserSegment(userId) + "/events").AbsolutePath;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var page = 0; page < ClampInt(maxPages, 1, 500) && !string.IsNullOrWhiteSpace(next); page++) {
            if (!seen.Add(next!)) throw new InvalidDataException("Graph event pagination returned a repeated continuation URL.");
            var response = await SendJsonAsync(HttpMethod.Get, next!, content: null, "event list", cancellationToken).ConfigureAwait(false);
            GraphEventListResponse? envelope;
            try {
                envelope = JsonSerializer.Deserialize(response, GraphJsonContext.Default.GraphEventListResponse);
            } catch (JsonException ex) {
                throw new InvalidDataException("Graph returned an invalid event list response.", ex);
            }
            if (envelope?.Value != null) output.AddRange(envelope.Value);
            if (output.Count >= resultLimit) return output.Take(resultLimit).ToArray();
            next = NormalizeContinuation(envelope?.NextLink, expectedPath);
        }
        if (!string.IsNullOrWhiteSpace(next)) throw new InvalidDataException("Graph event listing exceeded the configured page bound.");
        return output;
    }

    /// <summary>Gets one calendar event.</summary>
    public Task<GraphEvent> GetEventAsync(string eventId, string userId = "me", CancellationToken cancellationToken = default) =>
        GetResourceAsync(
            BuildUserSegment(userId) + "/events/" + EscapeRequired(eventId, nameof(eventId)),
            GraphJsonContext.Default.GraphEvent,
            "event get",
            cancellationToken);

    /// <summary>Creates a calendar event.</summary>
    public Task<GraphEvent> CreateEventAsync(GraphEvent graphEvent, string userId = "me", CancellationToken cancellationToken = default) =>
        WriteResourceAsync(
            HttpMethod.Post,
            BuildUserSegment(userId) + "/events",
            GraphEventWriteRequest.From(graphEvent ?? throw new ArgumentNullException(nameof(graphEvent))),
            GraphJsonContext.Default.GraphEventWriteRequest,
            GraphJsonContext.Default.GraphEvent,
            "event create",
            cancellationToken);

    /// <summary>Updates a calendar event.</summary>
    public Task<GraphEvent> UpdateEventAsync(string eventId, GraphEvent graphEvent, string userId = "me", CancellationToken cancellationToken = default) =>
        WriteResourceAsync(
            new HttpMethod("PATCH"),
            BuildUserSegment(userId) + "/events/" + EscapeRequired(eventId, nameof(eventId)),
            GraphEventWriteRequest.From(graphEvent ?? throw new ArgumentNullException(nameof(graphEvent))),
            GraphJsonContext.Default.GraphEventWriteRequest,
            GraphJsonContext.Default.GraphEvent,
            "event update",
            cancellationToken);

    /// <summary>Deletes a calendar event.</summary>
    public Task DeleteEventAsync(string eventId, string userId = "me", CancellationToken cancellationToken = default) =>
        DeleteResourceAsync(
            BuildUserSegment(userId) + "/events/" + EscapeRequired(eventId, nameof(eventId)),
            "event delete",
            cancellationToken);

    private async Task<T> GetResourceAsync<T>(
        string url,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        string operation,
        CancellationToken cancellationToken) {
        var json = await SendJsonAsync(HttpMethod.Get, url, content: null, operation, cancellationToken).ConfigureAwait(false);
        try {
            return JsonSerializer.Deserialize(json, typeInfo)
                ?? throw new InvalidDataException($"Graph returned an empty {operation} response.");
        } catch (JsonException ex) {
            throw new InvalidDataException($"Graph returned an invalid {operation} response.", ex);
        }
    }

    private async Task<TResponse> WriteResourceAsync<TRequest, TResponse>(
        HttpMethod method,
        string url,
        TRequest value,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TRequest> requestTypeInfo,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TResponse> responseTypeInfo,
        string operation,
        CancellationToken cancellationToken) {
        var requestBody = JsonSerializer.Serialize(value, requestTypeInfo);
        using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        var response = await SendJsonAsync(method, url, content, operation, cancellationToken).ConfigureAwait(false);
        try {
            return JsonSerializer.Deserialize(response, responseTypeInfo)
                ?? throw new InvalidDataException($"Graph returned an empty {operation} response.");
        } catch (JsonException ex) {
            throw new InvalidDataException($"Graph returned an invalid {operation} response.", ex);
        }
    }

    private async Task DeleteResourceAsync(string url, string operation, CancellationToken cancellationToken) {
        _ = await SendJsonAsync(HttpMethod.Delete, url, content: null, operation, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> SendJsonAsync(
        HttpMethod method,
        string url,
        HttpContent? content,
        string operation,
        CancellationToken cancellationToken) {
        ThrowIfDisposed();
        using var request = new HttpRequestMessage(method, url) { Content = content };
        ApplyAuthHeader(request);
        using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!response.IsSuccessStatusCode) {
            throw new GraphApiException(response.StatusCode, $"Graph {operation} failed ({(int)response.StatusCode}).", body, TryGetRetryAfter(response));
        }
        return body;
    }

    private static string EscapeRequired(string value, string parameterName) {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{parameterName} is required.", parameterName);
        return Uri.EscapeDataString(value.Trim());
    }

    private string? NormalizeContinuation(string? nextLink, string expectedPath) {
        if (string.IsNullOrWhiteSpace(nextLink)) return null;
        var trimmed = nextLink!.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.RelativeOrAbsolute, out var uri)) {
            throw new InvalidDataException("Graph returned an invalid continuation URL.");
        }
        var origin = _client.BaseAddress ?? new Uri("https://graph.microsoft.com/v1.0/");
        var absolute = uri.IsAbsoluteUri ? uri : new Uri(origin, uri);
        if (!string.Equals(absolute.Scheme, origin.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(absolute.Host, origin.Host, StringComparison.OrdinalIgnoreCase) ||
            absolute.Port != origin.Port ||
            !string.IsNullOrEmpty(absolute.UserInfo) ||
            !string.IsNullOrEmpty(absolute.Fragment) ||
            !string.Equals(absolute.AbsolutePath, expectedPath, StringComparison.Ordinal)) {
            throw new InvalidDataException("Graph returned a continuation URL outside the configured Graph origin.");
        }
        return absolute.AbsoluteUri;
    }
}
