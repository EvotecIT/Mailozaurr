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
    /// Retrieves a delta page for messages in a folder.
    /// </summary>
    /// <remarks>
    /// When <paramref name="cursor"/> is empty, starts a new delta query for the folder.
    /// Otherwise, continues from a previously returned nextLink/deltaLink.
    /// </remarks>
    public async Task<GraphDeltaPage<GraphMailMessage>> DeltaMessagesAsync(
        string folderIdOrWellKnownName,
        string? cursor = null,
        string userId = "me",
        int top = 100,
        string? select = "id,subject,receivedDateTime,from,toRecipients,internetMessageId,hasAttachments,isRead,flag,conversationId",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(folderIdOrWellKnownName)) {
            throw new ArgumentException("folderIdOrWellKnownName is required.", nameof(folderIdOrWellKnownName));
        }

        var safeTop = ClampInt(top, 1, 999);
        var userSegment = BuildUserSegment(userId);

        var url = (cursor ?? string.Empty).Trim();
        if (url.Length == 0) {
            var folderSelector = Uri.EscapeDataString(folderIdOrWellKnownName.Trim());
            var sb = new StringBuilder();
            sb.Append(userSegment).Append("/mailFolders/").Append(folderSelector).Append("/messages/delta");
            sb.Append("?$top=").Append(safeTop.ToString(CultureInfo.InvariantCulture));
            var selectValue = select == null ? null : select.Trim();
            if (selectValue != null && selectValue.Length > 0) {
                sb.Append("&$select=").Append(Uri.EscapeDataString(selectValue));
            }
            url = sb.ToString();
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph delta failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }

        var upserts = new List<GraphMailMessage>();
        var deletedIds = new List<string>();
        string? nextLink = null;
        string? deltaLink = null;

        using (var doc = JsonDocument.Parse(body)) {
            if (doc.RootElement.TryGetProperty("@odata.nextLink", out var next) && next.ValueKind == JsonValueKind.String) {
                nextLink = next.GetString();
            }
            if (doc.RootElement.TryGetProperty("@odata.deltaLink", out var delta) && delta.ValueKind == JsonValueKind.String) {
                deltaLink = delta.GetString();
            }
            if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array) {
                foreach (var item in value.EnumerateArray()) {
                    var id = TryGetString(item, "id");
                    if (id == null) {
                        continue;
                    }
                    var trimmedId = id.Trim();
                    if (trimmedId.Length == 0) {
                        continue;
                    }
                    if (item.TryGetProperty("@removed", out _)) {
                        deletedIds.Add(trimmedId);
                        continue;
                    }
                    var msg = TryParseMailMessage(item);
                    if (msg != null) {
                        upserts.Add(msg);
                    }
                }
            }
        }

        return new GraphDeltaPage<GraphMailMessage>(upserts, nextLink, deltaLink, deletedIds);
    }

    /// <summary>
    /// Moves a message to the specified destination folder id.
    /// </summary>
    public async Task MoveMessageAsync(
        string messageId,
        string destinationId,
        string userId = "me",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        if (string.IsNullOrWhiteSpace(destinationId)) {
            throw new ArgumentException("destinationId is required.", nameof(destinationId));
        }

        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(messageId.Trim());
        var json = JsonSerializer.Serialize(new GraphDestinationRequest { DestinationId = destinationId.Trim() }, GraphJsonContext.Default.GraphDestinationRequest);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var req = new HttpRequestMessage(HttpMethod.Post, userSegment + "/messages/" + selector + "/move") { Content = content };
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph move failed for messageId '{messageId.Trim()}' ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }
    }

    /// <summary>
    /// Deletes a message.
    /// </summary>
    public async Task DeleteMessageAsync(string messageId, string userId = "me", CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(messageId.Trim());
        using var req = new HttpRequestMessage(HttpMethod.Delete, userSegment + "/messages/" + selector);
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph delete failed for messageId '{messageId.Trim()}' ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }
    }

    /// <summary>
    /// Sets the read state of a message.
    /// </summary>
    public async Task SetMessageIsReadAsync(
        string messageId,
        bool isRead,
        string userId = "me",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(messageId.Trim());
        var json = JsonSerializer.Serialize(new GraphMarkReadRequest { IsRead = isRead }, GraphJsonContext.Default.GraphMarkReadRequest);
        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), userSegment + "/messages/" + selector) {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph set-isRead failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }
    }

    /// <summary>
    /// Sets the flagged state of a message.
    /// </summary>
    public async Task SetMessageFlaggedAsync(
        string messageId,
        bool flagged,
        string userId = "me",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(messageId.Trim());
        var status = flagged ? "flagged" : "notFlagged";
        var json = JsonSerializer.Serialize(
            new GraphSetFlagRequest { Flag = new GraphSetFlagRequestFlag { FlagStatus = status } },
            GraphJsonContext.Default.GraphSetFlagRequest);
        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), userSegment + "/messages/" + selector) {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph set-flag failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }
    }

    /// <summary>
    /// Moves many messages using Graph batch requests.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> BatchMoveMessagesAsync(
        IEnumerable<string> messageIds,
        string destinationFolderId,
        string userId = "me",
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }
        if (string.IsNullOrWhiteSpace(destinationFolderId)) {
            throw new ArgumentException("destinationFolderId is required.", nameof(destinationFolderId));
        }

        var ids = NormalizeBulkIds(messageIds);
        if (ids.Count == 0) {
            return Array.Empty<GraphBulkOperationResult>();
        }

        var userSegment = BuildUserSegment(userId);
        var batch = ClampInt(batchSize, 1, 20);
        var payloadJson = JsonSerializer.Serialize(
            new GraphDestinationRequest { DestinationId = destinationFolderId.Trim() },
            GraphJsonContext.Default.GraphDestinationRequest);
        using var bodyDoc = JsonDocument.Parse(payloadJson);
        var body = bodyDoc.RootElement.Clone();

        return await ExecuteMessageBatchAsync(
            ids,
            batch,
            messageId => new GraphBatchRequest {
                Method = GraphHttpMethod.POST,
                Url = userSegment + "/messages/" + Uri.EscapeDataString(messageId) + "/move",
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json" },
                Body = body
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes many messages using Graph batch requests.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> BatchDeleteMessagesAsync(
        IEnumerable<string> messageIds,
        string userId = "me",
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }

        var ids = NormalizeBulkIds(messageIds);
        if (ids.Count == 0) {
            return Array.Empty<GraphBulkOperationResult>();
        }

        var userSegment = BuildUserSegment(userId);
        var batch = ClampInt(batchSize, 1, 20);
        return await ExecuteMessageBatchAsync(
            ids,
            batch,
            messageId => new GraphBatchRequest {
                Method = GraphHttpMethod.DELETE,
                Url = userSegment + "/messages/" + Uri.EscapeDataString(messageId)
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets read/unread state for many messages using Graph batch requests.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> BatchSetMessagesIsReadAsync(
        IEnumerable<string> messageIds,
        bool isRead,
        string userId = "me",
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }

        var ids = NormalizeBulkIds(messageIds);
        if (ids.Count == 0) {
            return Array.Empty<GraphBulkOperationResult>();
        }

        var userSegment = BuildUserSegment(userId);
        var batch = ClampInt(batchSize, 1, 20);
        var payloadJson = JsonSerializer.Serialize(
            new GraphMarkReadRequest { IsRead = isRead },
            GraphJsonContext.Default.GraphMarkReadRequest);
        using var bodyDoc = JsonDocument.Parse(payloadJson);
        var body = bodyDoc.RootElement.Clone();

        return await ExecuteMessageBatchAsync(
            ids,
            batch,
            messageId => new GraphBatchRequest {
                Method = GraphHttpMethod.PATCH,
                Url = userSegment + "/messages/" + Uri.EscapeDataString(messageId),
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json" },
                Body = body
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets flagged/unflagged state for many messages using Graph batch requests.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> BatchSetMessagesFlaggedAsync(
        IEnumerable<string> messageIds,
        bool flagged,
        string userId = "me",
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }

        var ids = NormalizeBulkIds(messageIds);
        if (ids.Count == 0) {
            return Array.Empty<GraphBulkOperationResult>();
        }

        var userSegment = BuildUserSegment(userId);
        var batch = ClampInt(batchSize, 1, 20);
        var payloadJson = JsonSerializer.Serialize(
            new GraphSetFlagRequest { Flag = new GraphSetFlagRequestFlag { FlagStatus = flagged ? "flagged" : "notFlagged" } },
            GraphJsonContext.Default.GraphSetFlagRequest);
        using var bodyDoc = JsonDocument.Parse(payloadJson);
        var body = bodyDoc.RootElement.Clone();

        return await ExecuteMessageBatchAsync(
            ids,
            batch,
            messageId => new GraphBatchRequest {
                Method = GraphHttpMethod.PATCH,
                Url = userSegment + "/messages/" + Uri.EscapeDataString(messageId),
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json" },
                Body = body
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves all messages in each conversation using Graph batch requests.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> BatchMoveConversationsAsync(
        IEnumerable<string> conversationIds,
        string destinationFolderId,
        string userId = "me",
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (conversationIds == null) {
            throw new ArgumentNullException(nameof(conversationIds));
        }
        if (string.IsNullOrWhiteSpace(destinationFolderId)) {
            throw new ArgumentException("destinationFolderId is required.", nameof(destinationFolderId));
        }

        var conversations = NormalizeBulkIds(conversationIds);
        if (conversations.Count == 0) {
            return Array.Empty<GraphBulkOperationResult>();
        }

        var output = new List<GraphBulkOperationResult>(conversations.Count);
        foreach (var conversationId in conversations) {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<string> messageIds;
            try {
                messageIds = await ListConversationMessageIdsAsync(conversationId, userId: userId, cancellationToken: cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                output.Add(new GraphBulkOperationResult {
                    Id = conversationId,
                    Ok = false,
                    Error = ex.Message
                });
                continue;
            }

            if (messageIds.Count == 0) {
                output.Add(new GraphBulkOperationResult { Id = conversationId, Ok = true });
                continue;
            }

            var moved = await BatchMoveMessagesAsync(
                messageIds,
                destinationFolderId,
                userId: userId,
                batchSize: batchSize,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var failed = FindFirstFailedBulkResult(moved);
            if (failed is not null) {
                output.Add(new GraphBulkOperationResult {
                    Id = conversationId,
                    Ok = false,
                    Error = failed.Error ?? "Graph conversation move failed."
                });
                continue;
            }
            output.Add(new GraphBulkOperationResult { Id = conversationId, Ok = true });
        }

        return output;
    }

    /// <summary>
    /// Deletes all messages in each conversation using Graph batch requests.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> BatchDeleteConversationsAsync(
        IEnumerable<string> conversationIds,
        string userId = "me",
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (conversationIds == null) {
            throw new ArgumentNullException(nameof(conversationIds));
        }

        var conversations = NormalizeBulkIds(conversationIds);
        if (conversations.Count == 0) {
            return Array.Empty<GraphBulkOperationResult>();
        }

        var output = new List<GraphBulkOperationResult>(conversations.Count);
        foreach (var conversationId in conversations) {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<string> messageIds;
            try {
                messageIds = await ListConversationMessageIdsAsync(conversationId, userId: userId, cancellationToken: cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                output.Add(new GraphBulkOperationResult {
                    Id = conversationId,
                    Ok = false,
                    Error = ex.Message
                });
                continue;
            }

            if (messageIds.Count == 0) {
                output.Add(new GraphBulkOperationResult { Id = conversationId, Ok = true });
                continue;
            }

            var deleted = await BatchDeleteMessagesAsync(
                messageIds,
                userId: userId,
                batchSize: batchSize,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var failed = FindFirstFailedBulkResult(deleted);
            if (failed is not null) {
                output.Add(new GraphBulkOperationResult {
                    Id = conversationId,
                    Ok = false,
                    Error = failed.Error ?? "Graph conversation delete failed."
                });
                continue;
            }
            output.Add(new GraphBulkOperationResult { Id = conversationId, Ok = true });
        }

        return output;
    }

    /// <summary>
    /// Sends a Graph batch request using the current bearer token.
    /// </summary>
    public async Task<IReadOnlyList<GraphBatchResult>> SendBatchAsync(IEnumerable<GraphBatchRequest> requests, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (requests == null) {
            throw new ArgumentNullException(nameof(requests));
        }

        var payload = new GraphBatchPayload();
        var i = 0;
        foreach (var r in requests) {
            if (r == null) {
                continue;
            }
            var id = string.IsNullOrWhiteSpace(r.Id) ? (++i).ToString(CultureInfo.InvariantCulture) : r.Id.Trim();
            var url = (r.Url ?? string.Empty).Trim();
            if (url.Length == 0) {
                throw new ArgumentException("GraphBatchRequest.Url is required.", nameof(requests));
            }

            payload.Requests.Add(new GraphBatchRequestPayload {
                Id = id,
                Method = r.Method.ToString(),
                Url = url.TrimStart('/'),
                Headers = r.Headers,
                Body = r.Body
            });
        }

        var json = JsonSerializer.Serialize(payload, GraphJsonContext.Default.GraphBatchPayload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var req = new HttpRequestMessage(HttpMethod.Post, "$batch") { Content = content };
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph batch failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }

        var results = new List<GraphBatchResult>();
        using (var doc = JsonDocument.Parse(body)) {
            if (doc.RootElement.TryGetProperty("responses", out var responses) && responses.ValueKind == JsonValueKind.Array) {
                foreach (var item in responses.EnumerateArray()) {
                    var result = new GraphBatchResult();
                    if (item.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String) {
                        result.Id = idEl.GetString() ?? string.Empty;
                    }
                    if (item.TryGetProperty("status", out var statusEl) && statusEl.TryGetInt32(out var status)) {
                        result.Status = status;
                    }
                    if (item.TryGetProperty("headers", out var headersEl) && headersEl.ValueKind == JsonValueKind.Object) {
                        var h = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in headersEl.EnumerateObject()) {
                            if (prop.Value.ValueKind == JsonValueKind.String) {
                                h[prop.Name] = prop.Value.GetString() ?? string.Empty;
                            }
                        }
                        result.Headers = h;
                    }
                    if (item.TryGetProperty("body", out var bodyEl)) {
                        result.Body = bodyEl.Clone();
                    }
                    results.Add(result);
                }
            }
        }

        return results;
    }

    private static List<string> NormalizeBulkIds(IEnumerable<string> ids) {
        var output = new List<string>();
        foreach (var raw in ids) {
            if (string.IsNullOrWhiteSpace(raw)) {
                continue;
            }
            var id = raw.Trim();
            if (id.Length == 0) {
                continue;
            }
            output.Add(id);
        }
        return output;
    }

    private async Task<IReadOnlyList<GraphBulkOperationResult>> ExecuteMessageBatchAsync(
        List<string> messageIds,
        int batchSize,
        Func<string, GraphBatchRequest> requestFactory,
        CancellationToken cancellationToken) {
        var output = new List<GraphBulkOperationResult>(messageIds.Count);
        var chunkSize = ClampInt(batchSize, 1, 20);

        for (var i = 0; i < messageIds.Count; i += chunkSize) {
            cancellationToken.ThrowIfCancellationRequested();

            var count = Math.Min(chunkSize, messageIds.Count - i);
            var chunk = messageIds.GetRange(i, count);
            var subIds = new List<string>(chunk.Count);
            var requests = new List<GraphBatchRequest>(chunk.Count);
            for (var j = 0; j < chunk.Count; j++) {
                var subId = (j + 1).ToString(CultureInfo.InvariantCulture);
                subIds.Add(subId);
                var req = requestFactory(chunk[j]);
                req.Id = subId;
                requests.Add(req);
            }

            IReadOnlyList<GraphBatchResult> responses;
            try {
                responses = await SendBatchAsync(requests, cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                var err = ex.Message;
                foreach (var id in chunk) {
                    output.Add(new GraphBulkOperationResult { Id = id, Ok = false, Error = err });
                }
                continue;
            }

            var byId = new Dictionary<string, GraphBatchResult>(StringComparer.Ordinal);
            foreach (var response in responses) {
                if (response == null || string.IsNullOrWhiteSpace(response.Id)) {
                    continue;
                }
                byId[response.Id.Trim()] = response;
            }

            for (var j = 0; j < chunk.Count; j++) {
                var messageId = chunk[j];
                var subId = subIds[j];
                if (!byId.TryGetValue(subId, out var response)) {
                    output.Add(new GraphBulkOperationResult {
                        Id = messageId,
                        Ok = false,
                        Error = "Graph batch response missing for message id."
                    });
                    continue;
                }

                if (response.Status >= 200 && response.Status <= 299) {
                    output.Add(new GraphBulkOperationResult { Id = messageId, Ok = true });
                    continue;
                }

                output.Add(new GraphBulkOperationResult {
                    Id = messageId,
                    Ok = false,
                    Error = TryExtractBatchErrorMessage(response) ?? ("Graph batch request failed (" + response.Status.ToString(CultureInfo.InvariantCulture) + ").")
                });
            }
        }

        return output;
    }

    private static GraphBulkOperationResult? FindFirstFailedBulkResult(IReadOnlyList<GraphBulkOperationResult> results) {
        if (results == null) {
            return null;
        }
        foreach (var result in results) {
            if (result != null && !result.Ok) {
                return result;
            }
        }
        return null;
    }

    private static string? TryExtractBatchErrorMessage(GraphBatchResult response) {
        if (response?.Body == null) {
            return null;
        }

        var body = response.Body.Value;
        if (body.ValueKind != JsonValueKind.Object) {
            return null;
        }

        if (body.TryGetProperty("error", out var error)) {
            if (error.ValueKind == JsonValueKind.String) {
                var text = error.GetString();
                if (text != null) {
                    var trimmed = text.Trim();
                    if (trimmed.Length > 0) {
                        return trimmed;
                    }
                }
            }

            if (error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("message", out var messageEl) &&
                messageEl.ValueKind == JsonValueKind.String) {
                var msg = messageEl.GetString();
                if (msg != null) {
                    var trimmed = msg.Trim();
                    if (trimmed.Length > 0) {
                        return trimmed;
                    }
                }
            }
        }

        if (body.TryGetProperty("message", out var fallbackMessageEl) &&
            fallbackMessageEl.ValueKind == JsonValueKind.String) {
            var fallback = fallbackMessageEl.GetString();
            if (fallback != null) {
                var trimmed = fallback.Trim();
                if (trimmed.Length > 0) {
                    return trimmed;
                }
            }
        }

        return null;
    }
}