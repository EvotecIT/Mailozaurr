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
    /// Moves a mail folder to another location.
    /// </summary>
    /// <param name="credential">Credential used to authenticate to Microsoft Graph.</param>
    /// <param name="userPrincipalName">User principal name owning the mail folder.</param>
    /// <param name="folderId">Identifier of the folder to move.</param>
    /// <param name="destinationFolderId">Identifier of the new parent folder.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task MoveFolderAsync(
        GraphCredential credential,
        string userPrincipalName,
        string folderId,
        string destinationFolderId) =>
        MoveFolderAsync(credential, userPrincipalName, folderId, destinationFolderId, dryRun: false);

    /// <summary>
    /// Moves a mail folder to another location, optionally simulating the change.
    /// </summary>
    public static async Task MoveFolderAsync(
        GraphCredential credential,
        string userPrincipalName,
        string folderId,
        string destinationFolderId,
        bool dryRun) {
        if (dryRun) {
            return;
        }
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;
        var uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/mailFolders/{folderId}/move");
        var body = JsonSerializer.Serialize(new GraphDestinationRequest { DestinationId = destinationFolderId }, MailozaurrJsonContext.Default.GraphDestinationRequest);
        await InvokeGraphApiAsync("POST", uri, headers, body).ConfigureAwait(false);
    }

    /// <summary>
    /// Renames a mail folder.
    /// </summary>
    /// <param name="credential">Credential used to authenticate to Microsoft Graph.</param>
    /// <param name="userPrincipalName">User principal name owning the mail folder.</param>
    /// <param name="folderId">Identifier of the folder to rename.</param>
    /// <param name="newDisplayName">New display name for the folder.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task RenameFolderAsync(
        GraphCredential credential,
        string userPrincipalName,
        string folderId,
        string newDisplayName) =>
        RenameFolderAsync(credential, userPrincipalName, folderId, newDisplayName, dryRun: false);

    /// <summary>
    /// Renames a mail folder, optionally simulating the change.
    /// </summary>
    public static async Task RenameFolderAsync(
        GraphCredential credential,
        string userPrincipalName,
        string folderId,
        string newDisplayName,
        bool dryRun) {
        if (dryRun) {
            return;
        }
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;
        var uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/mailFolders/{folderId}");
        var body = JsonSerializer.Serialize(new GraphFolderRenameRequest { DisplayName = newDisplayName }, MailozaurrJsonContext.Default.GraphFolderRenameRequest);
        await InvokeGraphApiAsync("PATCH", uri, headers, body).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a mail folder.
    /// </summary>
    /// <param name="credential">Credential used to authenticate to Microsoft Graph.</param>
    /// <param name="userPrincipalName">User principal name owning the mail folder.</param>
    /// <param name="folderId">Identifier of the folder to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task RemoveFolderAsync(
        GraphCredential credential,
        string userPrincipalName,
        string folderId) =>
        RemoveFolderAsync(credential, userPrincipalName, folderId, dryRun: false);

    /// <summary>
    /// Removes a mail folder, optionally simulating the change.
    /// </summary>
    public static async Task RemoveFolderAsync(
        GraphCredential credential,
        string userPrincipalName,
        string folderId,
        bool dryRun) {
        if (dryRun) {
            return;
        }
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;
        var uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/mailFolders/{folderId}");
        await InvokeGraphApiAsync("DELETE", uri, headers).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves mailbox permissions for a user.
    /// </summary>
    public static async Task<List<GraphMailboxPermission>> GetMailboxPermissionsAsync(GraphCredential credential, string userPrincipalName) {
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;
        var uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/permissions");
        var doc = await InvokeGraphApiAsync("GET", uri, headers).ConfigureAwait(false);
        var result = new List<GraphMailboxPermission>();
        if (doc.RootElement.TryGetProperty("value", out var val) && val.ValueKind == JsonValueKind.Array) {
            foreach (var item in val.EnumerateArray()) {
                var dict = ConvertJsonElementToNativeObject(item) as Dictionary<string, object>;
                if (dict != null) result.Add(new GraphMailboxPermission(dict, userPrincipalName));
            }
        }
        return result;
    }

    /// <summary>
    /// Adds a mailbox permission.
    /// </summary>
    public static Task AddMailboxPermissionAsync(GraphCredential credential, string userPrincipalName, string body) =>
        AddMailboxPermissionAsync(credential, userPrincipalName, body, dryRun: false);

    /// <summary>
    /// Adds a mailbox permission, optionally simulating the change.
    /// </summary>
    public static async Task AddMailboxPermissionAsync(GraphCredential credential, string userPrincipalName, string body, bool dryRun) {
        if (dryRun) {
            return;
        }
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;
        var uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/permissions");
        await InvokeGraphApiAsync("POST", uri, headers, body).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a mailbox permission.
    /// </summary>
    public static Task RemoveMailboxPermissionAsync(GraphCredential credential, string userPrincipalName, string permissionId) =>
        RemoveMailboxPermissionAsync(credential, userPrincipalName, permissionId, dryRun: false);

    /// <summary>
    /// Removes a mailbox permission, optionally simulating the change.
    /// </summary>
    public static async Task RemoveMailboxPermissionAsync(GraphCredential credential, string userPrincipalName, string permissionId, bool dryRun) {
        if (dryRun) {
            return;
        }
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;
        var uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/permissions/{permissionId}");
        await InvokeGraphApiAsync("DELETE", uri, headers).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves aggregated mailbox statistics including message count and total attachment size.
    /// </summary>
    public static async Task<GraphMailboxStatistics> GetMailboxStatisticsAsync(
        GraphCredential credential,
        string userPrincipalName) {
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;

        var folderQuery = new Dictionary<string, object> {
                { "$select", "id,displayName,wellKnownName,totalItemCount,unreadItemCount,childFolderCount" },
                { "$top", "100" }
            };
        var folderUri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/mailFolders", folderQuery);
        int messageCount = 0;
        int folderCount = 0;
        var foldersStats = new List<GraphMailboxFolderStatistics>();
        while (!string.IsNullOrWhiteSpace(folderUri)) {
            var doc = await InvokeGraphApiAsync("GET", folderUri!, headers).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("value", out var folders) && folders.ValueKind == JsonValueKind.Array) {
                foreach (var item in folders.EnumerateArray()) {
                    var stat = new GraphMailboxFolderStatistics {
                        Id = item.GetProperty("id").GetString() ?? string.Empty,
                        DisplayName = item.GetProperty("displayName").GetString() ?? string.Empty,
                        WellKnownName = item.TryGetProperty("wellKnownName", out var wn) ? wn.GetString() : null,
                        TotalItemCount = item.TryGetProperty("totalItemCount", out var tic) && tic.TryGetInt32(out var c) ? c : 0,
                        UnreadItemCount = item.TryGetProperty("unreadItemCount", out var uic) && uic.TryGetInt32(out var u) ? u : 0,
                        ChildFolderCount = item.TryGetProperty("childFolderCount", out var cfc) && cfc.TryGetInt32(out var cf) ? cf : 0
                    };
                    messageCount += stat.TotalItemCount;
                    folderCount++;
                    foldersStats.Add(stat);
                }
            }
            folderUri = null;
            if (doc.RootElement.TryGetProperty("@odata.nextLink", out var next)) {
                folderUri = next.GetString();
            }
        }

        long attachmentSize = 0;
        int messagesWithAttachments = 0;
        var msgQuery = new Dictionary<string, object> {
                { "$select", "id,hasAttachments" },
                { "$top", "50" }
            };
        var msgUri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/messages", msgQuery);
        while (!string.IsNullOrWhiteSpace(msgUri)) {
            var doc = await InvokeGraphApiAsync("GET", msgUri!, headers).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("value", out var msgs) && msgs.ValueKind == JsonValueKind.Array) {
                foreach (var msg in msgs.EnumerateArray()) {
                    if (!msg.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.String) {
                        continue;
                    }
                    var hasAtt = msg.TryGetProperty("hasAttachments", out var ha) && ha.GetBoolean();
                    if (!hasAtt) {
                        continue;
                    }
                    messagesWithAttachments++;
                    var id = idEl.GetString();
                    if (string.IsNullOrWhiteSpace(id)) {
                        continue;
                    }
                    var atts = await GetMailMessageAttachmentsAsync(
                        credential,
                        userPrincipalName,
                        id!,
                        new[] { "size" }).ConfigureAwait(false);
                    foreach (var att in atts) {
                        attachmentSize += att.Size;
                    }
                }
            }
            msgUri = null;
            if (doc.RootElement.TryGetProperty("@odata.nextLink", out var nextMsg)) {
                msgUri = nextMsg.GetString();
            }
        }

        var result = new GraphMailboxStatistics {
            UserPrincipalName = userPrincipalName,
            MessageCount = messageCount,
            MessagesWithAttachments = messagesWithAttachments,
            TotalAttachmentSize = attachmentSize,
            TotalFolders = folderCount
        };
        result.FolderStatistics.AddRange(foldersStats);
        return result;
    }

    /// <summary>
    /// Retrieves inbox rules for the specified user.
    /// </summary>
    /// <param name="credential">Credential used to authenticate to Microsoft Graph.</param>
    /// <param name="userPrincipalName">User principal name owning the mailbox.</param>
    /// <param name="filter">Optional OData filter to apply to the query.</param>
    /// <returns>List of inbox rules represented as dictionaries.</returns>
    public static async Task<List<GraphInboxRule>> GetRulesAsync(
        GraphCredential credential,
        string userPrincipalName,
        string? filter = null) {
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;
        Dictionary<string, object>? qp = null;
        if (!string.IsNullOrWhiteSpace(filter)) qp = new Dictionary<string, object> { ["$filter"] = filter! };
        var uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/mailFolders/inbox/messageRules", qp);
        var doc = await InvokeGraphApiAsync("GET", uri, headers).ConfigureAwait(false);
        var rules = new List<GraphInboxRule>();
        if (doc.RootElement.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array) {
            foreach (var item in valueElement.EnumerateArray()) {
                var rule = JsonSerializer.Deserialize(item.GetRawText(), MailozaurrJsonContext.Default.GraphInboxRule);
                if (rule != null) rules.Add(rule);
            }
        }
        return rules;
    }

    /// <summary>
    /// Creates a new inbox rule.
    /// </summary>
    /// <param name="credential">Credential used to authenticate to Microsoft Graph.</param>
    /// <param name="userPrincipalName">User principal name owning the mailbox.</param>
    /// <param name="rule">Dictionary describing the rule to create.</param>
    /// <returns>The created rule as a dictionary.</returns>
    public static Task<GraphInboxRule> NewRuleAsync(
        GraphCredential credential,
        string userPrincipalName,
        GraphInboxRule rule) =>
        NewRuleAsync(credential, userPrincipalName, rule, dryRun: false);

    /// <summary>
    /// Creates a new inbox rule, optionally simulating the change.
    /// </summary>
    public static async Task<GraphInboxRule> NewRuleAsync(
        GraphCredential credential,
        string userPrincipalName,
        GraphInboxRule rule,
        bool dryRun) {
        if (dryRun) {
            return rule;
        }
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;
        var body = JsonSerializer.Serialize(rule, MailozaurrJsonContext.Default.GraphInboxRule);
        var uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/mailFolders/inbox/messageRules");
        var doc = await InvokeGraphApiAsync("POST", uri, headers, body).ConfigureAwait(false);
        var created = JsonSerializer.Deserialize(doc.RootElement.GetRawText(), MailozaurrJsonContext.Default.GraphInboxRule);
        if (created is null) {
            throw new InvalidDataException("Microsoft Graph returned an invalid inbox rule response.");
        }
        return created;
    }

    /// <summary>
    /// Updates an existing inbox rule.
    /// </summary>
    public static Task<GraphInboxRule> UpdateRuleAsync(
        GraphCredential credential,
        string userPrincipalName,
        string ruleId,
        GraphInboxRule rule) =>
        UpdateRuleAsync(credential, userPrincipalName, ruleId, rule, dryRun: false);

    /// <summary>
    /// Updates an existing inbox rule, optionally simulating the change.
    /// </summary>
    public static async Task<GraphInboxRule> UpdateRuleAsync(
        GraphCredential credential,
        string userPrincipalName,
        string ruleId,
        GraphInboxRule rule,
        bool dryRun) {
        if (dryRun) {
            return rule;
        }
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;
        var body = JsonSerializer.Serialize(rule, MailozaurrJsonContext.Default.GraphInboxRule);
        var uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/mailFolders/inbox/messageRules/{ruleId}");
        var doc = await InvokeGraphApiAsync("PATCH", uri, headers, body).ConfigureAwait(false);
        var updated = JsonSerializer.Deserialize(doc.RootElement.GetRawText(), MailozaurrJsonContext.Default.GraphInboxRule);
        if (updated is null) {
            throw new InvalidDataException("Microsoft Graph returned an invalid inbox rule response.");
        }
        return updated;
    }

    /// <summary>
    /// Removes the specified inbox rule.
    /// </summary>
    /// <param name="credential">Credential used to authenticate to Microsoft Graph.</param>
    /// <param name="userPrincipalName">User principal name owning the mailbox.</param>
    /// <param name="ruleId">Identifier of the rule to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task RemoveRuleAsync(
        GraphCredential credential,
        string userPrincipalName,
        string ruleId) =>
        RemoveRuleAsync(credential, userPrincipalName, ruleId, dryRun: false);

    /// <summary>
    /// Removes the specified inbox rule, optionally simulating the change.
    /// </summary>
    public static async Task RemoveRuleAsync(
        GraphCredential credential,
        string userPrincipalName,
        string ruleId,
        bool dryRun) {
        if (dryRun) {
            return;
        }
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;
        var uri = JoinUriQuery(GraphEndpoint.V1, $"/users/{userPrincipalName}/mailFolders/inbox/messageRules/{ruleId}");
        await InvokeGraphApiAsync("DELETE", uri, headers).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves calendar events for the specified user.
    /// </summary>
    public static async Task<List<Dictionary<string, object>>> GetEventsAsync(
        GraphCredential credential,
        string userPrincipalName,
        IEnumerable<string>? properties = null,
        string? filter = null,
        int? limit = null) {
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;
        var qp = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(filter)) qp["$filter"] = filter!;
        if (properties != null && properties.Any()) qp["$select"] = string.Join(",", properties);
        var uri = JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{userPrincipalName}/events", qp);
        var doc = await InvokeGraphApiAsync("GET", uri, headers).ConfigureAwait(false);
        var events = new List<Dictionary<string, object>>();
        if (doc.RootElement.TryGetProperty("value", out var val) && val.ValueKind == JsonValueKind.Array) {
            foreach (var item in val.EnumerateArray()) {
                if (limit.HasValue && events.Count >= limit.Value) break;
                var dict = ConvertJsonElementToNativeObject(item) as Dictionary<string, object>;
                if (dict != null) events.Add(dict);
            }
        }
        return events;
    }

    /// <summary>
    /// Creates a new calendar event.
    /// </summary>
    public static Task<GraphEvent> NewEventAsync(
        GraphCredential credential,
        string userPrincipalName,
        GraphEvent ev) =>
        NewEventAsync(credential, userPrincipalName, ev, dryRun: false);

    /// <summary>
    /// Creates a new calendar event, optionally simulating the change.
    /// </summary>
    public static async Task<GraphEvent> NewEventAsync(
        GraphCredential credential,
        string userPrincipalName,
        GraphEvent ev,
        bool dryRun) {
        if (dryRun) {
            return ev;
        }
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;
        var body = JsonSerializer.Serialize(ev, MailozaurrJsonContext.Default.GraphEvent);
        var uri = JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{userPrincipalName}/events");
        var doc = await InvokeGraphApiAsync("POST", uri, headers, body).ConfigureAwait(false);
        var created = JsonSerializer.Deserialize(doc.RootElement.GetRawText(), MailozaurrJsonContext.Default.GraphEvent);
        if (created is null) {
            throw new InvalidDataException("Microsoft Graph returned an invalid event response.");
        }
        return created;
    }

    /// <summary>
    /// Updates an existing calendar event.
    /// </summary>
    public static Task<GraphEvent> UpdateEventAsync(
        GraphCredential credential,
        string userPrincipalName,
        string eventId,
        GraphEvent ev) =>
        UpdateEventAsync(credential, userPrincipalName, eventId, ev, dryRun: false);

    /// <summary>
    /// Updates an existing calendar event, optionally simulating the change.
    /// </summary>
    public static async Task<GraphEvent> UpdateEventAsync(
        GraphCredential credential,
        string userPrincipalName,
        string eventId,
        GraphEvent ev,
        bool dryRun) {
        if (dryRun) {
            return ev;
        }
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;
        var body = JsonSerializer.Serialize(ev, MailozaurrJsonContext.Default.GraphEvent);
        var uri = JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{userPrincipalName}/events/{eventId}");
        var doc = await InvokeGraphApiAsync("PATCH", uri, headers, body).ConfigureAwait(false);
        var updated = JsonSerializer.Deserialize(doc.RootElement.GetRawText(), MailozaurrJsonContext.Default.GraphEvent);
        if (updated is null) {
            throw new InvalidDataException("Microsoft Graph returned an invalid event response.");
        }
        return updated;
    }

    /// <summary>
    /// Removes the specified calendar event.
    /// </summary>
    public static Task RemoveEventAsync(
        GraphCredential credential,
        string userPrincipalName,
        string eventId) =>
        RemoveEventAsync(credential, userPrincipalName, eventId, dryRun: false);

    /// <summary>
    /// Removes the specified calendar event, optionally simulating the change.
    /// </summary>
    public static async Task RemoveEventAsync(
        GraphCredential credential,
        string userPrincipalName,
        string eventId,
        bool dryRun) {
        if (dryRun) {
            return;
        }
        var headers = new Dictionary<string, string>();
        var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com").ConfigureAwait(false);
        headers["Authorization"] = token;
        var uri = JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{userPrincipalName}/events/{eventId}");
        await InvokeGraphApiAsync("DELETE", uri, headers).ConfigureAwait(false);
    }
}
