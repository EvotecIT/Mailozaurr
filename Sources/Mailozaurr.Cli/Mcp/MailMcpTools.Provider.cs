using Mailozaurr;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Mailozaurr.Cli.Mcp;

public sealed partial class MailMcpTools {
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Inspects bounded Graph or Gmail permission evidence and its authority limits.")]
    public Task<MailPermissionEvidenceResult> mail_provider_permission_evidence(
        [Description("Configured Graph or Gmail profile identifier.")] string profileId,
        [Description("Optional mailbox override.")] string? mailboxId = null,
        CancellationToken cancellationToken = default) =>
        _application.PermissionEvidence.GetEvidenceAsync(profileId, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Lists Microsoft Graph Inbox rules through the shared Graph mailbox service.")]
    public Task<IReadOnlyList<GraphInboxRule>> mail_graph_rule_list(
        [Description("Configured Graph profile identifier.")] string profileId,
        [Description("Optional mailbox override.")] string? mailboxId = null,
        [Description("Optional OData filter.")] string? filter = null,
        [Description("Maximum result count from 1 to 999.")] int top = 100,
        [Description("Maximum provider pages to read.")] int maxPages = 25,
        CancellationToken cancellationToken = default) =>
        _application.GraphMailbox.ListRulesAsync(profileId, mailboxId, filter, top, maxPages, cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Gets one Microsoft Graph Inbox rule.")]
    public Task<GraphInboxRule> mail_graph_rule_get(string profileId, string ruleId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        _application.GraphMailbox.GetRuleAsync(profileId, ruleId, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description("Creates one Microsoft Graph Inbox rule.")]
    public Task<GraphInboxRule> mail_graph_rule_create(string profileId, GraphInboxRule rule, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        _application.GraphMailbox.CreateRuleAsync(profileId, rule, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("Updates one Microsoft Graph Inbox rule.")]
    public Task<GraphInboxRule> mail_graph_rule_update(string profileId, string ruleId, GraphInboxRule rule, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        _application.GraphMailbox.UpdateRuleAsync(profileId, ruleId, rule, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description("Deletes one Microsoft Graph Inbox rule.")]
    public async Task<OperationResult> mail_graph_rule_delete(string profileId, string ruleId, string? mailboxId = null, CancellationToken cancellationToken = default) {
        await _application.GraphMailbox.DeleteRuleAsync(profileId, ruleId, mailboxId, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success("Graph Inbox rule deleted.");
    }

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Lists Microsoft Graph calendar events.")]
    public Task<IReadOnlyList<GraphEvent>> mail_graph_event_list(string profileId, string? mailboxId = null, string? filter = null, string? select = GraphApiClient.DefaultEventSelect, int top = 100, int maxPages = 25, CancellationToken cancellationToken = default) =>
        _application.GraphMailbox.ListEventsAsync(profileId, mailboxId, filter, select, top, maxPages, cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Gets one Microsoft Graph calendar event.")]
    public Task<GraphEvent> mail_graph_event_get(string profileId, string eventId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        _application.GraphMailbox.GetEventAsync(profileId, eventId, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description("Creates one Microsoft Graph calendar event.")]
    public Task<GraphEvent> mail_graph_event_create(string profileId, GraphEvent graphEvent, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        _application.GraphMailbox.CreateEventAsync(profileId, graphEvent, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("Updates one Microsoft Graph calendar event.")]
    public Task<GraphEvent> mail_graph_event_update(string profileId, string eventId, GraphEvent graphEvent, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        _application.GraphMailbox.UpdateEventAsync(profileId, eventId, graphEvent, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description("Deletes one Microsoft Graph calendar event.")]
    public async Task<OperationResult> mail_graph_event_delete(string profileId, string eventId, string? mailboxId = null, CancellationToken cancellationToken = default) {
        await _application.GraphMailbox.DeleteEventAsync(profileId, eventId, mailboxId, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success("Graph event deleted.");
    }

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Lists messages in one Microsoft Graph conversation.")]
    public Task<IReadOnlyList<GraphMailMessage>> mail_graph_thread_get(string profileId, string conversationId, string? mailboxId = null, int top = 100, int maxPages = 25, CancellationToken cancellationToken = default) =>
        _application.GraphMailbox.GetThreadAsync(profileId, conversationId, mailboxId, top, maxPages, cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Lists Gmail server-side filters.")]
    public Task<IReadOnlyList<GmailFilter>> mail_gmail_filter_list(string profileId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        _application.GmailMailbox.ListFiltersAsync(profileId, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Gets one Gmail server-side filter.")]
    public Task<GmailFilter> mail_gmail_filter_get(string profileId, string filterId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        _application.GmailMailbox.GetFilterAsync(profileId, filterId, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description("Creates one Gmail server-side filter.")]
    public Task<GmailFilter> mail_gmail_filter_create(string profileId, GmailFilter filter, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        _application.GmailMailbox.CreateFilterAsync(profileId, filter, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description("Permanently deletes one Gmail filter. Gmail filters are immutable and must be replaced by delete plus create.")]
    public async Task<OperationResult> mail_gmail_filter_delete(string profileId, string filterId, string? mailboxId = null, CancellationToken cancellationToken = default) {
        await _application.GmailMailbox.DeleteFilterAsync(profileId, filterId, mailboxId, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success("Gmail filter deleted.");
    }

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Lists Gmail labels.")]
    public Task<IReadOnlyList<GmailLabel>> mail_gmail_label_list(string profileId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        _application.GmailMailbox.ListLabelsAsync(profileId, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Gets one Gmail label.")]
    public Task<GmailLabel> mail_gmail_label_get(string profileId, string labelId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        _application.GmailMailbox.GetLabelAsync(profileId, labelId, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description("Creates one Gmail user label.")]
    public Task<GmailLabel> mail_gmail_label_create(string profileId, GmailLabel label, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        _application.GmailMailbox.CreateLabelAsync(profileId, label, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("Updates one Gmail user label.")]
    public Task<GmailLabel> mail_gmail_label_update(string profileId, string labelId, GmailLabel label, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        _application.GmailMailbox.UpdateLabelAsync(profileId, labelId, label, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description("Permanently deletes one Gmail user label and removes it from messages and threads.")]
    public async Task<OperationResult> mail_gmail_label_delete(string profileId, string labelId, string? mailboxId = null, CancellationToken cancellationToken = default) {
        await _application.GmailMailbox.DeleteLabelAsync(profileId, labelId, mailboxId, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success("Gmail label deleted.");
    }

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Lists exactly one provider page of Gmail threads and returns its continuation token.")]
    public Task<GmailApiClient.GmailThreadPage> mail_gmail_thread_list(string profileId, string? mailboxId = null, string? query = null, int pageSize = 100, string? pageToken = null, CancellationToken cancellationToken = default) =>
        _application.GmailMailbox.ListThreadsAsync(profileId, mailboxId, query, pageSize, pageToken, cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Gets one Gmail thread with its messages.")]
    public Task<GmailThread> mail_gmail_thread_get(string profileId, string threadId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        _application.GmailMailbox.GetThreadAsync(profileId, threadId, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("Adds and removes labels on one Gmail thread.")]
    public Task<GmailThread> mail_gmail_thread_labels(string profileId, string threadId, string[]? addLabelIds = null, string[]? removeLabelIds = null, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        _application.GmailMailbox.ModifyThreadLabelsAsync(profileId, threadId, addLabelIds, removeLabelIds, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("Moves one Gmail thread to trash.")]
    public Task<GmailThread> mail_gmail_thread_trash(string profileId, string threadId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        _application.GmailMailbox.TrashThreadAsync(profileId, threadId, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description("Immediately and permanently deletes one Gmail thread.")]
    public async Task<OperationResult> mail_gmail_thread_delete(string profileId, string threadId, string? mailboxId = null, CancellationToken cancellationToken = default) {
        await _application.GmailMailbox.DeleteThreadAsync(profileId, threadId, mailboxId, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success("Gmail thread permanently deleted.");
    }
}
