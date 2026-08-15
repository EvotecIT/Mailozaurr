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
    /// Lists mail folders for the given user, recursively expanding child folders.
    /// </summary>
    /// <remarks>
    /// This performs best-effort traversal with safeguards to prevent infinite loops in pathological cases.
    /// </remarks>
    public async Task<IReadOnlyList<GraphMailFolder>> ListMailFoldersRecursiveAsync(
        string userId = "me",
        int top = 200,
        string? select = "id,displayName,parentFolderId,childFolderCount,wellKnownName,totalItemCount,unreadItemCount",
        int maxRequests = 250,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        var safeTop = ClampInt(top, 1, 999);
        var safeMax = ClampInt(maxRequests, 1, 5000);
        var userSegment = BuildUserSegment(userId);

        var foldersById = new Dictionary<string, GraphMailFolder>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        var initial = new StringBuilder();
        initial.Append(userSegment).Append("/mailFolders?$top=").Append(safeTop.ToString(CultureInfo.InvariantCulture));
        var selectValue = select == null ? null : select.Trim();
        if (selectValue != null && selectValue.Length > 0) {
            initial.Append("&$select=").Append(Uri.EscapeDataString(selectValue));
        }
        pending.Enqueue(initial.ToString());

        var processed = 0;
        while (pending.Count > 0) {
            cancellationToken.ThrowIfCancellationRequested();
            if (processed++ >= safeMax) {
                break;
            }

            var url = pending.Dequeue();
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyAuthHeader(req);
            using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            if (!resp.IsSuccessStatusCode) {
                throw new GraphApiException(resp.StatusCode, $"Graph mailFolders list failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array) {
                foreach (var item in value.EnumerateArray()) {
                    var folder = TryParseMailFolder(item);
                    if (folder == null || string.IsNullOrWhiteSpace(folder.Id)) {
                        continue;
                    }
                    foldersById[folder.Id] = folder;

                    if (folder.ChildFolderCount.HasValue && folder.ChildFolderCount.Value > 0) {
                        var childUrl = new StringBuilder();
                        childUrl.Append(userSegment)
                            .Append("/mailFolders/")
                            .Append(Uri.EscapeDataString(folder.Id))
                            .Append("/childFolders?$top=")
                            .Append(safeTop.ToString(CultureInfo.InvariantCulture));
                        if (selectValue != null && selectValue.Length > 0) {
                            childUrl.Append("&$select=").Append(Uri.EscapeDataString(selectValue));
                        }
                        pending.Enqueue(childUrl.ToString());
                    }
                }
            }

            if (doc.RootElement.TryGetProperty("@odata.nextLink", out var next) && next.ValueKind == JsonValueKind.String) {
                var nextLink = next.GetString();
                if (nextLink != null && nextLink.Trim().Length > 0) {
                    pending.Enqueue(nextLink);
                }
            }
        }

        var output = new List<GraphMailFolder>(foldersById.Count);
        output.AddRange(foldersById.Values);
        output.Sort(static (a, b) => {
            var r = string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (r != 0) return r;
            return string.Compare(a.Id, b.Id, StringComparison.Ordinal);
        });
        return output;
    }

    /// <summary>
    /// Gets a single mail folder record.
    /// </summary>
    public async Task<GraphMailFolder> GetMailFolderAsync(
        string folderIdOrWellKnownName,
        string userId = "me",
        string? select = "id,displayName,parentFolderId,childFolderCount,wellKnownName,totalItemCount,unreadItemCount",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(folderIdOrWellKnownName)) {
            throw new ArgumentException("folderIdOrWellKnownName is required.", nameof(folderIdOrWellKnownName));
        }

        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(folderIdOrWellKnownName.Trim());
        var url = new StringBuilder();
        url.Append(userSegment).Append("/mailFolders/").Append(selector);
        var selectValue = select == null ? null : select.Trim();
        if (selectValue != null && selectValue.Length > 0) {
            url.Append("?$select=").Append(Uri.EscapeDataString(selectValue));
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, url.ToString());
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph mailFolder get failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }

        using var doc = JsonDocument.Parse(body);
        var folder = TryParseMailFolder(doc.RootElement);
        if (folder is null) {
            throw new InvalidDataException("Graph returned an invalid mail folder response.");
        }
        return folder;
    }

    /// <summary>
    /// Lists messages within a mail folder.
    /// </summary>
    public async Task<GraphPage<GraphMailMessage>> ListMessagesAsync(
        string folderIdOrWellKnownName,
        string userId = "me",
        int top = 100,
        int? skip = null,
        string? select = "id,subject,receivedDateTime,from,toRecipients,internetMessageId,hasAttachments,isRead,flag,conversationId",
        string? orderBy = "receivedDateTime desc",
        string? filter = null,
        string? search = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(folderIdOrWellKnownName)) {
            throw new ArgumentException("folderIdOrWellKnownName is required.", nameof(folderIdOrWellKnownName));
        }

        var safeTop = ClampInt(top, 1, 999);
        var userSegment = BuildUserSegment(userId);
        var folderSelector = Uri.EscapeDataString(folderIdOrWellKnownName.Trim());

        var url = new StringBuilder();
        url.Append(userSegment).Append("/mailFolders/").Append(folderSelector).Append("/messages");
        url.Append("?$top=").Append(safeTop.ToString(CultureInfo.InvariantCulture));
        if (skip.HasValue && skip.Value > 0) {
            url.Append("&$skip=").Append(skip.Value.ToString(CultureInfo.InvariantCulture));
        }
        var orderByValue = orderBy == null ? null : orderBy.Trim();
        if (orderByValue != null && orderByValue.Length > 0) {
            url.Append("&$orderby=").Append(Uri.EscapeDataString(orderByValue));
        }
        var selectValue = select == null ? null : select.Trim();
        if (selectValue != null && selectValue.Length > 0) {
            url.Append("&$select=").Append(Uri.EscapeDataString(selectValue));
        }
        var filterValue = filter == null ? null : filter.Trim();
        if (filterValue != null && filterValue.Length > 0) {
            url.Append("&$filter=").Append(Uri.EscapeDataString(filterValue));
        }
        var searchValue = search == null ? null : search.Trim();
        if (searchValue != null && searchValue.Length > 0) {
            var sanitized = searchValue.Replace("\"", string.Empty).Trim();
            if (sanitized.Length > 0) {
                url.Append("&$search=").Append(Uri.EscapeDataString("\"" + sanitized + "\""));
            }
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, url.ToString());
        ApplyAuthHeader(req);
        if (searchValue != null && searchValue.Length > 0) {
            req.Headers.TryAddWithoutValidation("ConsistencyLevel", "eventual");
            req.Headers.TryAddWithoutValidation("Prefer", "HonorNonIndexedQueriesWarning=true");
        }
        using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph messages list failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }

        var items = new List<GraphMailMessage>();
        string? nextLink = null;
        using (var doc = JsonDocument.Parse(body)) {
            if (doc.RootElement.TryGetProperty("@odata.nextLink", out var next) && next.ValueKind == JsonValueKind.String) {
                nextLink = next.GetString();
            }
            if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array) {
                foreach (var it in value.EnumerateArray()) {
                    var msg = TryParseMailMessage(it);
                    if (msg != null) {
                        items.Add(msg);
                    }
                }
            }
        }

        return new GraphPage<GraphMailMessage>(items, nextLink);
    }

    /// <summary>
    /// Lists message ids for a Graph conversation.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListConversationMessageIdsAsync(
        string conversationId,
        string userId = "me",
        int top = 100,
        int maxPages = 25,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(conversationId)) {
            throw new ArgumentException("conversationId is required.", nameof(conversationId));
        }

        var safeTop = ClampInt(top, 1, 999);
        var safeMaxPages = ClampInt(maxPages, 1, 500);
        var userSegment = BuildUserSegment(userId);

        var ids = new List<string>();
        var pages = 0;
        var filter = "conversationId eq '" + EscapeODataStringLiteral(conversationId.Trim()) + "'";
        var url = new StringBuilder();
        url.Append(userSegment).Append("/messages?$select=id&$top=").Append(safeTop.ToString(CultureInfo.InvariantCulture));
        url.Append("&$filter=").Append(Uri.EscapeDataString(filter));

        string nextUrl = url.ToString();
        while (pages++ < safeMaxPages) {
            cancellationToken.ThrowIfCancellationRequested();
            using var req = new HttpRequestMessage(HttpMethod.Get, nextUrl);
            ApplyAuthHeader(req);
            using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            if (!resp.IsSuccessStatusCode) {
                throw new GraphApiException(resp.StatusCode, $"Graph conversation list failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array) {
                foreach (var it in value.EnumerateArray()) {
                    var id = TryGetString(it, "id");
                    if (id != null) {
                        var trimmed = id.Trim();
                        if (trimmed.Length > 0) {
                            ids.Add(trimmed);
                        }
                    }
                }
            }
            if (doc.RootElement.TryGetProperty("@odata.nextLink", out var next) && next.ValueKind == JsonValueKind.String) {
                var link = next.GetString();
                if (link != null) {
                    var trimmed = link.Trim();
                    if (trimmed.Length > 0) {
                        nextUrl = trimmed;
                        continue;
                    }
                }
            }
            break;
        }

        return ids;
    }

    /// <summary>
    /// Lists message metadata for a Graph conversation.
    /// </summary>
    public async Task<IReadOnlyList<GraphMailMessage>> ListConversationMessagesAsync(
        string conversationId,
        string userId = "me",
        int top = 100,
        int maxPages = 25,
        string? select = "id,subject,receivedDateTime,from,toRecipients,internetMessageId,hasAttachments,isRead,flag,conversationId",
        string? orderBy = "receivedDateTime desc",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(conversationId)) {
            throw new ArgumentException("conversationId is required.", nameof(conversationId));
        }

        var safeTop = ClampInt(top, 1, 999);
        var safeMaxPages = ClampInt(maxPages, 1, 500);
        var userSegment = BuildUserSegment(userId);

        var messages = new List<GraphMailMessage>();
        var pages = 0;
        var filter = "conversationId eq '" + EscapeODataStringLiteral(conversationId.Trim()) + "'";
        var url = new StringBuilder();
        url.Append(userSegment).Append("/messages?$top=").Append(safeTop.ToString(CultureInfo.InvariantCulture));
        url.Append("&$filter=").Append(Uri.EscapeDataString(filter));

        var selectValue = select == null ? null : select.Trim();
        if (selectValue != null && selectValue.Length > 0) {
            url.Append("&$select=").Append(Uri.EscapeDataString(selectValue));
        }

        var orderByValue = orderBy == null ? null : orderBy.Trim();
        if (orderByValue != null && orderByValue.Length > 0) {
            url.Append("&$orderby=").Append(Uri.EscapeDataString(orderByValue));
        }

        string nextUrl = url.ToString();
        while (pages++ < safeMaxPages) {
            cancellationToken.ThrowIfCancellationRequested();
            using var req = new HttpRequestMessage(HttpMethod.Get, nextUrl);
            ApplyAuthHeader(req);
            using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            if (!resp.IsSuccessStatusCode) {
                throw new GraphApiException(resp.StatusCode, $"Graph conversation list failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array) {
                foreach (var it in value.EnumerateArray()) {
                    var msg = TryParseMailMessage(it);
                    if (msg != null) {
                        messages.Add(msg);
                    }
                }
            }

            if (doc.RootElement.TryGetProperty("@odata.nextLink", out var next) && next.ValueKind == JsonValueKind.String) {
                var link = next.GetString();
                if (link != null) {
                    var trimmed = link.Trim();
                    if (trimmed.Length > 0) {
                        nextUrl = trimmed;
                        continue;
                    }
                }
            }
            break;
        }

        return messages;
    }

    /// <summary>
    /// Gets message metadata.
    /// </summary>
    public async Task<GraphMailMessage> GetMessageAsync(
        string messageId,
        string userId = "me",
        string? select = "id,subject,receivedDateTime,from,toRecipients,internetMessageId,hasAttachments,isRead,flag,conversationId",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }

        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(messageId.Trim());
        var url = new StringBuilder();
        url.Append(userSegment).Append("/messages/").Append(selector);
        var selectValue = select == null ? null : select.Trim();
        if (selectValue != null && selectValue.Length > 0) {
            url.Append("?$select=").Append(Uri.EscapeDataString(selectValue));
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, url.ToString());
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph message get failed for messageId '{messageId.Trim()}' ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }

        using var doc = JsonDocument.Parse(body);
        var msg = TryParseMailMessage(doc.RootElement);
        if (msg is null) {
            throw new InvalidDataException("Graph returned an invalid message response.");
        }
        return msg;
    }

    /// <summary>
    /// Downloads the message MIME content via the <c>/$value</c> endpoint.
    /// </summary>
    public async Task<byte[]> GetMessageMimeAsync(
        string messageId,
        string userId = "me",
        int maxBytes = 25 * 1024 * 1024,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        if (maxBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "maxBytes must be > 0.");
        }
        const int hardLimitBytes = 256 * 1024 * 1024;
        if (maxBytes > hardLimitBytes) {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), $"maxBytes must be <= {hardLimitBytes}.");
        }

        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(messageId.Trim());
        var url = userSegment + "/messages/" + selector + "/$value";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) {
#if NET5_0_OR_GREATER
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            throw new GraphApiException(resp.StatusCode, $"Graph MIME fetch failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }

#if NET5_0_OR_GREATER
        await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
        using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
#if NET5_0_OR_GREATER
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
#else
            var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
#endif
            if (read <= 0) {
                break;
            }
            if (ms.Length + read > maxBytes) {
                throw new InvalidDataException($"Graph MIME content exceeds {maxBytes} bytes.");
            }
#if NET5_0_OR_GREATER
            await ms.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
#else
            ms.Write(buffer, 0, read);
#endif
        }
        return ms.ToArray();
    }
}