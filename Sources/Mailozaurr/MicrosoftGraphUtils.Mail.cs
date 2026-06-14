using MimeKit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

public static partial class MicrosoftGraphUtils {
    /// <summary>
    /// Retrieves mail messages for the specified user.
    /// </summary>
    public static async Task<List<Dictionary<string, object>>> GetMailMessagesAsync(GraphCredential credential, string userPrincipalName, IEnumerable<string>? properties = null, string? filter = null, int? limit = null, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com", cancellationToken).ConfigureAwait(false);
        headers["Authorization"] = token ?? string.Empty;
        var queryParams = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(filter)) queryParams["$filter"] = filter!;
        if (properties != null && properties.Any()) queryParams["$select"] = string.Join(",", properties);
        var uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/messages", queryParams);
        var messages = new List<Dictionary<string, object>>();
        while (!string.IsNullOrEmpty(uri)) {
            cancellationToken.ThrowIfCancellationRequested();
            var doc = await InvokeGraphApiAsync("GET", uri!, headers, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array) {
                foreach (var item in valueElement.EnumerateArray()) {
                    cancellationToken.ThrowIfCancellationRequested();
                    var native = ConvertJsonElementToNativeObject(item) as Dictionary<string, object>;
                    if (native != null) {
                        messages.Add(native);
                        if (limit.HasValue && messages.Count >= limit.Value) {
                            return messages;
                        }
                    }
                }
            }
            if (!doc.RootElement.TryGetProperty("@odata.nextLink", out var nextLinkElement)) {
                break;
            }
            var nextLink = nextLinkElement.GetString();
            if (string.IsNullOrEmpty(nextLink)) {
                break;
            }
            uri = nextLink;
        }
        return messages;
    }

    /// <summary>
    /// Retrieves attachments for a specific message.
    /// </summary>
    public static async Task<List<Attachment>> GetMailMessageAttachmentsAsync(GraphCredential credential, string userPrincipalName, string messageId, IEnumerable<string>? properties = null, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com", cancellationToken).ConfigureAwait(false);
        headers["Authorization"] = token;
        var queryParams = new Dictionary<string, object>();
        if (properties != null && properties.Any()) queryParams["$select"] = string.Join(",", properties);
        var uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/messages/{messageId}/attachments", queryParams);
        var attachments = new List<Attachment>();
        while (!string.IsNullOrEmpty(uri)) {
            cancellationToken.ThrowIfCancellationRequested();
            var doc = await InvokeGraphApiAsync("GET", uri!, headers, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array) {
                foreach (var item in valueElement.EnumerateArray()) {
                    cancellationToken.ThrowIfCancellationRequested();
                    var att = JsonSerializer.Deserialize(item.GetRawText(), MailozaurrJsonContext.Default.Attachment);
                    if (att != null) {
                        attachments.Add(att);
                    }
                }
            }

            if (!doc.RootElement.TryGetProperty("@odata.nextLink", out var nextLinkElement)) {
                break;
            }
            var nextLink = nextLinkElement.GetString();
            if (string.IsNullOrEmpty(nextLink)) {
                break;
            }
            uri = nextLink;
        }
        return attachments;
    }

    /// <summary>
    /// Lists mail folders for the specified user.
    /// </summary>
    public static async Task<List<JsonElement>> GetMailFoldersAsync(GraphCredential credential, string userPrincipalName, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com", cancellationToken).ConfigureAwait(false);
        headers["Authorization"] = token;
        var uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/mailFolders");
        var folders = new List<JsonElement>();
        while (!string.IsNullOrEmpty(uri)) {
            cancellationToken.ThrowIfCancellationRequested();
            var doc = await InvokeGraphApiAsync("GET", uri!, headers, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array) {
                foreach (var item in valueElement.EnumerateArray()) {
                    cancellationToken.ThrowIfCancellationRequested();
                    folders.Add(item);
                }
            }

            if (!doc.RootElement.TryGetProperty("@odata.nextLink", out var nextLinkElement)) {
                break;
            }
            var nextLink = nextLinkElement.GetString();
            if (string.IsNullOrEmpty(nextLink)) {
                break;
            }
            uri = nextLink;
        }
        return folders;
    }

    /// <summary>
    /// Saves the bodies of messages to disk as HTML files.
    /// </summary>
    public static void SaveMailMessages(IEnumerable<EmailGraphMessage> messages, string path) {
        var resolvedPath = Path.GetFullPath(path);
        if (!Directory.Exists(resolvedPath)) Directory.CreateDirectory(resolvedPath);
        foreach (var m in messages) {
            if (m?.Body is not null) {
                var randomFileName = Path.ChangeExtension(Path.GetRandomFileName(), "html");
                var filePath = Path.Combine(resolvedPath, randomFileName);
                try {
                    var content = m.Body is JsonElement je && je.TryGetProperty("Content", out var c) ? c.GetString() : m.Body.ToString();
                    File.WriteAllText(filePath, content);
                } catch (IOException ex) {
                    // Log or handle error
                    LoggingMessages.Logger.WriteWarning($"SaveMailMessage - Couldn't save file to {filePath}. Error: {ex.Message}");
                    LoggingMessages.Logger.WriteWarning($"SaveMailMessage - Possible issue: Ensure the directory '{resolvedPath}' exists and you have write permissions.");
                }
            }
        }
    }

    /// <summary>
    /// Saves attachments to the specified directory.
    /// </summary>
    public static void SaveAttachments(IEnumerable<Attachment> attachments, string path) {
        var resolvedPath = Path.GetFullPath(path);
        if (!Directory.Exists(resolvedPath)) Directory.CreateDirectory(resolvedPath);
        foreach (var att in attachments) {
            if (!string.IsNullOrWhiteSpace(att.ContentBytes) && !string.IsNullOrWhiteSpace(att.Name)) {
                var filePath = Path.Combine(resolvedPath, att.Name);
                try {
                    var bytes = Convert.FromBase64String(att.ContentBytes);
                    File.WriteAllBytes(filePath, bytes);
                } catch (FormatException fex) {
                    // Invalid Base64 content
                    LoggingMessages.Logger.WriteWarning($"SaveAttachment - Invalid base64 content for {att.Name}. Error: {fex.Message}");
                    LoggingMessages.Logger.WriteWarning($"SaveAttachment - Possible issue: The attachment '{att.Name}' may be corrupted.");
                } catch (IOException ex) {
                    // Log or handle other errors
                    LoggingMessages.Logger.WriteWarning($"SaveAttachment - Couldn't save file to {filePath}. Error: {ex.Message}");
                    LoggingMessages.Logger.WriteWarning($"SaveAttachment - Possible issue: Verify the path '{filePath}' exists and you have write permissions.");
                }
            }
        }
    }

    /// <summary>
    /// Executes a search query across one or more mailboxes.
    /// </summary>
    public static async Task<List<GraphMessageInfo>> SearchMailboxesAsync(
        GraphCredential credential,
        IEnumerable<string> userPrincipalNames,
        string queryString,
        int from = 0,
        int size = 25) {
        var mailboxList = userPrincipalNames as IList<string> ?? userPrincipalNames.ToList();
        if (mailboxList.Count == 0) {
            return new List<GraphMessageInfo>();
        }

        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;

        var searchPayload = new GraphSearchPayload();
        foreach (var upn in mailboxList) {
            searchPayload.Requests.Add(new GraphSearchRequest {
                EntityTypes = new[] { "message" },
                From = from,
                Size = size,
                Query = new GraphSearchQuery { QueryString = queryString },
                UserScopes = new[] { upn }
            });
        }

        var body = JsonSerializer.Serialize(searchPayload, MailozaurrJsonContext.Default.GraphSearchPayload);
        var searchUri = BuildGraphUri(GraphEndpoint.V1, "/search/query");
        var doc = await InvokeGraphApiAsync("POST", searchUri, headers, body).ConfigureAwait(false);

        var results = new List<GraphMessageInfo>();
        int index = 0;
        if (doc.RootElement.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array) {
            foreach (var item in valueElement.EnumerateArray()) {
                if (index >= mailboxList.Count) {
                    break;
                }

                var upn = mailboxList[index++];
                if (item.TryGetProperty("hitsContainers", out var containers) && containers.ValueKind == JsonValueKind.Array) {
                    foreach (var container in containers.EnumerateArray()) {
                        if (container.TryGetProperty("hits", out var hits) && hits.ValueKind == JsonValueKind.Array) {
                            foreach (var hit in hits.EnumerateArray()) {
                                string? summary = null;
                                if (hit.TryGetProperty("summary", out var sumEl)) summary = sumEl.GetString();
                                if (hit.TryGetProperty("resource", out var res) && res.ValueKind == JsonValueKind.Object) {
                                    var dict = ConvertJsonElementToNativeObject(res) as Dictionary<string, object>;
                                    if (dict != null) {
                                        results.Add(new GraphMessageInfo(dict, upn, summary));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Performs an action on a mail message.
    /// </summary>
    public static async Task ExecuteMailMessageActionAsync(GraphCredential credential, string userPrincipalName, string messageId, GraphMessageAction action, string? destinationFolderId = null) {
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;

        string method;
        string uri;
        string? body = null;

        switch (action) {
            case GraphMessageAction.Move:
                if (string.IsNullOrWhiteSpace(destinationFolderId)) throw new ArgumentNullException(nameof(destinationFolderId));
                method = "POST";
                uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/messages/{messageId}/move");
                body = JsonSerializer.Serialize(new GraphDestinationRequest { DestinationId = destinationFolderId }, MailozaurrJsonContext.Default.GraphDestinationRequest);
                break;
            case GraphMessageAction.Copy:
                if (string.IsNullOrWhiteSpace(destinationFolderId)) throw new ArgumentNullException(nameof(destinationFolderId));
                method = "POST";
                uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/messages/{messageId}/copy");
                body = JsonSerializer.Serialize(new GraphDestinationRequest { DestinationId = destinationFolderId }, MailozaurrJsonContext.Default.GraphDestinationRequest);
                break;
            case GraphMessageAction.Delete:
                method = "DELETE";
                uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/messages/{messageId}");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }

        await InvokeGraphApiAsync(method, uri, headers, body).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves a mail message to another folder.
    /// </summary>
    public static Task MoveMailMessageAsync(GraphCredential credential, string userPrincipalName, string messageId, string destinationFolderId) =>
        MoveMailMessageAsync(credential, userPrincipalName, messageId, destinationFolderId, dryRun: false);

    /// <summary>
    /// Moves a mail message to another folder, optionally simulating the change.
    /// </summary>
    public static async Task MoveMailMessageAsync(GraphCredential credential, string userPrincipalName, string messageId, string destinationFolderId, bool dryRun) {
        if (dryRun) {
            return;
        }
        await ExecuteMailMessageActionAsync(credential, userPrincipalName, messageId, GraphMessageAction.Move, destinationFolderId).ConfigureAwait(false);
    }

    /// <summary>
    /// Copies a mail message to another folder.
    /// </summary>
    public static async Task CopyMailMessageAsync(GraphCredential credential, string userPrincipalName, string messageId, string destinationFolderId) {
        await ExecuteMailMessageActionAsync(credential, userPrincipalName, messageId, GraphMessageAction.Copy, destinationFolderId).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the read state for a mail message.
    /// </summary>
    public static Task SetMailMessageAsync(GraphCredential credential, string userPrincipalName, string messageId, bool isRead) =>
        SetMailMessageAsync(credential, userPrincipalName, messageId, isRead, dryRun: false);

    /// <summary>
    /// Sets the read state for a mail message, optionally simulating the change.
    /// </summary>
    public static async Task SetMailMessageAsync(GraphCredential credential, string userPrincipalName, string messageId, bool isRead, bool dryRun) {
        if (dryRun) {
            return;
        }
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;
        var uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/messages/{messageId}");
        var body = JsonSerializer.Serialize(new GraphMarkReadRequest { IsRead = isRead }, MailozaurrJsonContext.Default.GraphMarkReadRequest);
        await InvokeGraphApiAsync("PATCH", uri, headers, body).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a mail message.
    /// </summary>
    public static Task DeleteMailMessageAsync(GraphCredential credential, string userPrincipalName, string messageId) =>
        DeleteMailMessageAsync(credential, userPrincipalName, messageId, dryRun: false);

    /// <summary>
    /// Deletes a mail message, optionally simulating the change.
    /// </summary>
    public static async Task DeleteMailMessageAsync(GraphCredential credential, string userPrincipalName, string messageId, bool dryRun) {
        if (dryRun) {
            return;
        }
        await ExecuteMailMessageActionAsync(credential, userPrincipalName, messageId, GraphMessageAction.Delete).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves the raw MIME content of a mail message.
    /// </summary>
    public static async Task<MimeMessage> GetMailMessageMimeAsync(
        GraphCredential credential,
        string userPrincipalName,
        string messageId,
        CancellationToken cancellationToken = default) {
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com", cancellationToken).ConfigureAwait(false);
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://graph.microsoft.com/v1.0/users/{userPrincipalName}/messages/{messageId}/$value");
        request.Headers.TryAddWithoutValidation("Authorization", token);
        await ConcurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return await MimeMessage.LoadAsync(stream, cancellationToken).ConfigureAwait(false);
        } finally {
            ConcurrencySemaphore.Release();
        }
    }

    /// <summary>
    /// Deletes all messages from the Junk Email folder.
    /// </summary>
    public static Task ClearJunkMailAsync(
        GraphCredential credential,
        string userPrincipalName,
        IEnumerable<string>? skipIds = null,
        IEnumerable<string>? skipFrom = null,
        IEnumerable<string>? skipTo = null,
        IEnumerable<string>? skipSubjectContains = null,
        bool skipHasAttachment = false,
        IEnumerable<string>? skipAttachmentExtension = null) =>
        ClearJunkMailAsync(credential, userPrincipalName, dryRun: false, skipIds, skipFrom, skipTo, skipSubjectContains, skipHasAttachment, skipAttachmentExtension);

    /// <summary>
    /// Deletes all messages from the Junk Email folder, optionally simulating the change.
    /// </summary>
    public static async Task ClearJunkMailAsync(
        GraphCredential credential,
        string userPrincipalName,
        bool dryRun,
        IEnumerable<string>? skipIds = null,
        IEnumerable<string>? skipFrom = null,
        IEnumerable<string>? skipTo = null,
        IEnumerable<string>? skipSubjectContains = null,
        bool skipHasAttachment = false,
        IEnumerable<string>? skipAttachmentExtension = null) {
        if (dryRun) {
            return;
        }
        var properties = new List<string> { "id" };
        if (skipFrom != null) properties.Add("from");
        if (skipTo != null) properties.Add("toRecipients");
        if (skipSubjectContains != null) properties.Add("subject");
        if (skipHasAttachment || skipAttachmentExtension != null) properties.Add("hasAttachments");

        var messages = await GetJunkMailMessagesAsync(
            credential,
            userPrincipalName,
            properties,
            skipIds,
            skipFrom,
            skipTo,
            skipSubjectContains,
            skipHasAttachment,
            skipAttachmentExtension).ConfigureAwait(false);

        foreach (var msg in messages) {
            var id = msg["id"] as string;
            if (string.IsNullOrWhiteSpace(id)) continue;
            await DeleteMailMessageAsync(credential, userPrincipalName, id!).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Filters a collection of messages using provided skip criteria.
    /// </summary>
    /// <param name="messages">Messages to filter.</param>
    /// <param name="skipIds">IDs of messages to exclude.</param>
    /// <param name="skipFrom">Sender addresses to exclude.</param>
    /// <param name="skipTo">Recipient addresses to exclude.</param>
    /// <param name="skipSubjectContains">Subject substrings to exclude.</param>
    /// <param name="skipHasAttachment">Exclude messages that have attachments.</param>
    /// <returns>List of messages that are not considered junk.</returns>
    public static List<Dictionary<string, object>> FilterJunkMessages(
        IEnumerable<Dictionary<string, object>> messages,
        IEnumerable<string>? skipIds = null,
        IEnumerable<string>? skipFrom = null,
        IEnumerable<string>? skipTo = null,
        IEnumerable<string>? skipSubjectContains = null,
        bool skipHasAttachment = false) {
        var result = new List<Dictionary<string, object>>();
        var skipIdsSet = skipIds != null ? new HashSet<string>(skipIds, StringComparer.OrdinalIgnoreCase) : null;
        var skipFromSet = skipFrom != null ? new HashSet<string>(skipFrom, StringComparer.OrdinalIgnoreCase) : null;
        var skipToSet = skipTo != null ? new HashSet<string>(skipTo, StringComparer.OrdinalIgnoreCase) : null;
        var skipSubjectTokens = skipSubjectContains?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
        foreach (var msg in messages) {
            var id = msg.TryGetValue("id", out var idObj) ? idObj as string : null;
            if (!string.IsNullOrWhiteSpace(id) && skipIdsSet != null && skipIdsSet.Contains(id!)) {
                continue;
            }

            if (skipFromSet != null &&
                msg.TryGetValue("from", out var fromObj) &&
                fromObj is Dictionary<string, object> fDict &&
                fDict.TryGetValue("emailAddress", out var addrObj) &&
                addrObj is Dictionary<string, object> addr &&
                addr.TryGetValue("address", out var fromAddrObj) &&
                fromAddrObj is string fromAddr &&
                skipFromSet.Contains(fromAddr)) {
                continue;
            }

            if (skipToSet != null &&
                msg.TryGetValue("toRecipients", out var toObj) &&
                toObj is object[] arr &&
                arr.OfType<Dictionary<string, object>>().Any(rec =>
                    rec.TryGetValue("emailAddress", out var tAddrObj) &&
                    tAddrObj is Dictionary<string, object> tAddr &&
                    tAddr.TryGetValue("address", out var addrVal) &&
                    addrVal is string addrStr &&
                    skipToSet.Contains(addrStr))) {
                continue;
            }

            if (skipSubjectTokens != null &&
                msg.TryGetValue("subject", out var subjObj) &&
                subjObj is string subj &&
                skipSubjectTokens.Any(s => subj.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)) {
                continue;
            }

            if (skipHasAttachment &&
                msg.TryGetValue("hasAttachments", out var hasObj) &&
                hasObj is bool hasAtt && hasAtt) {
                continue;
            }

            result.Add(msg);
        }
        return result;
    }

    /// <summary>
    /// Retrieves messages from the Junk Email folder.
    /// </summary>
    public static async Task<List<Dictionary<string, object>>> GetJunkMailMessagesAsync(
        GraphCredential credential,
        string userPrincipalName,
        IEnumerable<string>? properties = null,
        IEnumerable<string>? skipIds = null,
        IEnumerable<string>? skipFrom = null,
        IEnumerable<string>? skipTo = null,
        IEnumerable<string>? skipSubjectContains = null,
        bool skipHasAttachment = false,
        IEnumerable<string>? skipAttachmentExtension = null) {
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;
        var props = properties != null ? new List<string>(properties) : new List<string>();
        HashSet<string>? skipAttachmentExtensions = null;
        if (skipAttachmentExtension != null) {
            skipAttachmentExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ext in skipAttachmentExtension) {
                if (string.IsNullOrWhiteSpace(ext)) {
                    continue;
                }

                var normalized = ext.Trim().TrimStart('.');
                if (normalized.Length > 0) {
                    skipAttachmentExtensions.Add(normalized);
                }
            }
        }
        if (skipHasAttachment || (skipAttachmentExtensions?.Count > 0)) {
            if (!props.Contains("hasAttachments")) props.Add("hasAttachments");
        }
        var query = new Dictionary<string, object>();
        if (props.Count > 0) query["$select"] = string.Join(",", props);
        var uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/mailFolders/junkemail/messages", query);
        var messages = new List<Dictionary<string, object>>();
        while (!string.IsNullOrWhiteSpace(uri)) {
            var doc = await InvokeGraphApiAsync("GET", uri!, headers).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array) {
                foreach (var item in valueElement.EnumerateArray()) {
                    var native = ConvertJsonElementToNativeObject(item) as Dictionary<string, object>;
                    if (native != null) messages.Add(native);
                }
            }
            uri = null;
            if (doc.RootElement.TryGetProperty("@odata.nextLink", out var next)) {
                uri = next.GetString();
            }
        }
        if (skipIds != null || skipFrom != null || skipTo != null || skipSubjectContains != null || skipHasAttachment) {
            messages = FilterJunkMessages(messages, skipIds, skipFrom, skipTo, skipSubjectContains, skipHasAttachment);
        }

        if (skipAttachmentExtensions?.Count > 0) {
            var result = new List<Dictionary<string, object>>();
            foreach (var msg in messages) {
                if (!msg.TryGetValue("id", out var idObj) || idObj is not string id) continue;
                if (msg.TryGetValue("hasAttachments", out var hasObj) && hasObj is bool hasAtt && hasAtt) {
                    var atts = await GetMailMessageAttachmentsAsync(
                        credential,
                        userPrincipalName,
                        id,
                        new[] { "name" }).ConfigureAwait(false);
                    if (atts.Any(att =>
                            skipAttachmentExtensions.Contains(
                                System.IO.Path.GetExtension(att.Name ?? string.Empty).TrimStart('.')))) {
                        continue;
                    }
                }
                result.Add(msg);
            }
            messages = result;
        }
        return messages;
    }
}