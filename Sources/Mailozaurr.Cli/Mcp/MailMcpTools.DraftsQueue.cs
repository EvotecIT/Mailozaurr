using Mailozaurr.Application;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Mailozaurr.Cli.Mcp;

public sealed partial class MailMcpTools {
    [McpServerTool]
    [Description("Lists reusable drafts stored by Mailozaurr.")]
    public Task<IReadOnlyList<MailDraft>> mail_draft_list(CancellationToken cancellationToken = default) =>
        _application.Drafts.GetDraftsAsync(cancellationToken);

    [McpServerTool]
    [Description("Lists reusable drafts stored by Mailozaurr using a lightweight projection.")]
    public Task<IReadOnlyList<MailDraftCompact>> mail_draft_compact_list(CancellationToken cancellationToken = default) =>
        _application.Drafts.GetDraftsCompactAsync(cancellationToken);

    [McpServerTool]
    [Description("Gets a reusable draft by identifier.")]
    public async Task<MailDraft> mail_draft_get(
        [Description("The stored draft identifier to retrieve.")] string draftId,
        CancellationToken cancellationToken = default) {
        var draft = await _application.Drafts.GetDraftAsync(draftId, cancellationToken).ConfigureAwait(false);
        return draft ?? throw new InvalidOperationException($"Draft '{draftId}' was not found.");
    }

    [McpServerTool]
    [Description("Gets a reusable draft by identifier using a lightweight projection.")]
    public async Task<MailDraftCompact> mail_draft_compact_get(
        [Description("The stored draft identifier to retrieve.")] string draftId,
        CancellationToken cancellationToken = default) {
        var draft = await _application.Drafts.GetDraftCompactAsync(draftId, cancellationToken).ConfigureAwait(false);
        return draft ?? throw new InvalidOperationException($"Draft '{draftId}' was not found.");
    }

    [McpServerTool]
    [Description("Creates or updates a reusable draft using the shared Mailozaurr draft store.")]
    public Task<OperationResult> mail_draft_save(
        [Description("The stable draft identifier to create or update.")] string draftId,
        [Description("A human-readable draft name.")] string name,
        [Description("The profile identifier that should eventually send this draft.")] string profileId,
        [Description("Primary recipient email addresses.")] string[] to,
        [Description("Optional subject line.")] string? subject = null,
        [Description("Optional plain text body.")] string? textBody = null,
        [Description("Optional HTML body.")] string? htmlBody = null,
        [Description("Optional CC recipient email addresses.")] string[]? cc = null,
        [Description("Optional BCC recipient email addresses.")] string[]? bcc = null,
        [Description("Optional Reply-To recipient email addresses.")] string[]? replyTo = null,
        [Description("Optional From email address override.")] string? from = null,
        [Description("Optional attachment file paths on the server filesystem.")] string[]? attachmentPaths = null,
        CancellationToken cancellationToken = default) =>
        _application.Drafts.SaveAsync(new MailDraft {
            Id = draftId,
            Name = name,
            Message = BuildDraftMessage(profileId, to, subject, textBody, htmlBody, cc, bcc, replyTo, from, attachmentPaths)
        }, cancellationToken);

    [McpServerTool]
    [Description("Deletes a reusable draft from the shared Mailozaurr draft store.")]
    public Task<OperationResult> mail_draft_delete(
        [Description("The stored draft identifier to delete.")] string draftId,
        CancellationToken cancellationToken = default) =>
        _application.Drafts.DeleteAsync(draftId, cancellationToken);

    [McpServerTool]
    [Description("Imports a draft JSON file into the shared Mailozaurr draft store.")]
    public async Task<MailDraft> mail_draft_import(
        [Description("The draft file path on the server filesystem.")] string path,
        [Description("Optional replacement draft identifier to use after import.")] string? draftId = null,
        [Description("Optional replacement draft name to use after import.")] string? name = null,
        CancellationToken cancellationToken = default) {
        var draft = await _application.DraftExchange.LoadAsync(path, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(draftId)) {
            draft.Id = draftId.Trim();
        }
        if (!string.IsNullOrWhiteSpace(name)) {
            draft.Name = name.Trim();
        }

        var result = await _application.Drafts.SaveAsync(draft, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded) {
            throw new InvalidOperationException(result.Message ?? $"Draft '{draft.Id}' could not be imported.");
        }

        return draft;
    }

    [McpServerTool]
    [Description("Exports a stored Mailozaurr draft to a draft JSON file.")]
    public async Task<OperationResult> mail_draft_export(
        [Description("The stored draft identifier to export.")] string draftId,
        [Description("The destination draft file path on the server filesystem.")] string path,
        CancellationToken cancellationToken = default) {
        var draft = await _application.Drafts.GetDraftAsync(draftId, cancellationToken).ConfigureAwait(false);
        if (draft == null) {
            throw new InvalidOperationException($"Draft '{draftId}' was not found.");
        }

        await _application.DraftExchange.SaveAsync(path, draft, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success("Draft exported.");
    }

    [McpServerTool]
    [Description("Sends or queues a previously stored Mailozaurr draft. Queueing is the default unless sendNow is true.")]
    public async Task<SendResult> mail_draft_send(
        [Description("The stored draft identifier to send.")] string draftId,
        [Description("When true, sends immediately instead of preferring the queue.")] bool sendNow = false,
        CancellationToken cancellationToken = default) {
        var draft = await _application.Drafts.GetDraftAsync(draftId, cancellationToken).ConfigureAwait(false);
        if (draft == null) {
            throw new InvalidOperationException($"Draft '{draftId}' was not found.");
        }

        return await _application.Send.SendAsync(new SendMessageRequest {
            ProfileId = draft.Message.ProfileId,
            PreferQueue = !sendNow,
            RequireImmediateSend = sendNow,
            Message = CloneDraftMessage(draft.Message)
        }, cancellationToken).ConfigureAwait(false);
    }

    [McpServerTool]
    [Description("Lists outbound messages currently waiting in Mailozaurr's pending queue.")]
    public Task<IReadOnlyList<QueuedMessageSummary>> mail_queue_list(CancellationToken cancellationToken = default) =>
        _application.Queue.ListAsync(cancellationToken);

    [McpServerTool]
    [Description("Lists outbound messages currently waiting in Mailozaurr's pending queue using a lightweight projection.")]
    public Task<IReadOnlyList<QueuedMessageCompact>> mail_queue_compact_list(CancellationToken cancellationToken = default) =>
        _application.Queue.ListCompactAsync(cancellationToken);

    [McpServerTool]
    [Description("Gets a queued outbound message by identifier.")]
    public async Task<QueuedMessageSummary> mail_queue_get(
        [Description("The queued message identifier to inspect.")] string messageId,
        CancellationToken cancellationToken = default) {
        var message = await _application.Queue.GetAsync(messageId, cancellationToken).ConfigureAwait(false);
        return message ?? throw new InvalidOperationException($"Queued message '{messageId}' was not found.");
    }

    [McpServerTool]
    [Description("Gets a queued outbound message by identifier using a lightweight projection.")]
    public async Task<QueuedMessageCompact> mail_queue_compact_get(
        [Description("The queued message identifier to inspect.")] string messageId,
        CancellationToken cancellationToken = default) {
        var message = await _application.Queue.GetCompactAsync(messageId, cancellationToken).ConfigureAwait(false);
        return message ?? throw new InvalidOperationException($"Queued message '{messageId}' was not found.");
    }

    [McpServerTool]
    [Description("Removes a queued outbound message without sending it.")]
    public Task<OperationResult> mail_queue_remove(
        [Description("The queued message identifier to remove.")] string messageId,
        CancellationToken cancellationToken = default) =>
        _application.Queue.RemoveAsync(messageId, cancellationToken);

    [McpServerTool]
    [Description("Processes all due queued outbound messages.")]
    public Task<QueueProcessResult> mail_queue_process(CancellationToken cancellationToken = default) =>
        _application.Queue.ProcessAsync(cancellationToken);

    private static DraftMessage BuildDraftMessage(
        string profileId,
        IEnumerable<string> to,
        string? subject,
        string? textBody,
        string? htmlBody,
        IEnumerable<string>? cc,
        IEnumerable<string>? bcc,
        IEnumerable<string>? replyTo,
        string? from,
        IEnumerable<string>? attachmentPaths) {
        var draft = new DraftMessage {
            ProfileId = profileId,
            Subject = subject,
            TextBody = textBody,
            HtmlBody = htmlBody
        };

        if (!string.IsNullOrWhiteSpace(from)) {
            draft.From = new MessageRecipient {
                Address = from.Trim()
            };
        }

        AddRecipients(draft.To, to);
        AddRecipients(draft.Cc, cc);
        AddRecipients(draft.Bcc, bcc);
        AddRecipients(draft.ReplyTo, replyTo);

        if (attachmentPaths != null) {
            foreach (var attachmentPath in attachmentPaths.Where(path => !string.IsNullOrWhiteSpace(path))) {
                draft.Attachments.Add(new DraftAttachment {
                    Path = attachmentPath.Trim()
                });
            }
        }

        return draft;
    }

    private static DraftMessage CloneDraftMessage(DraftMessage draft) => new() {
        ProfileId = draft.ProfileId,
        From = draft.From == null ? null : new MessageRecipient {
            Name = draft.From.Name,
            Address = draft.From.Address
        },
        To = draft.To.Select(ToRecipientCopy).ToList(),
        Cc = draft.Cc.Select(ToRecipientCopy).ToList(),
        Bcc = draft.Bcc.Select(ToRecipientCopy).ToList(),
        ReplyTo = draft.ReplyTo.Select(ToRecipientCopy).ToList(),
        Subject = draft.Subject,
        TextBody = draft.TextBody,
        HtmlBody = draft.HtmlBody,
        Headers = new Dictionary<string, string>(draft.Headers, StringComparer.OrdinalIgnoreCase),
        Attachments = draft.Attachments.Select(attachment => new DraftAttachment {
            Path = attachment.Path,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            IsInline = attachment.IsInline,
            ContentId = attachment.ContentId
        }).ToList()
    };

    private static void AddRecipients(ICollection<MessageRecipient> destination, IEnumerable<string>? addresses) {
        if (addresses == null) {
            return;
        }

        foreach (var address in addresses.Where(value => !string.IsNullOrWhiteSpace(value))) {
            destination.Add(new MessageRecipient {
                Address = address.Trim()
            });
        }
    }

    private static MessageRecipient ToRecipientCopy(MessageRecipient recipient) => new() {
        Name = recipient.Name,
        Address = recipient.Address
    };

    private static MailMessageActionPlanBatchQuery? BuildBatchQuery(IReadOnlyList<string>? planNames, IReadOnlyList<string>? profileIds, IReadOnlyList<string>? actions, string? sortBy, bool descending) {
        var normalizedPlanNames = (planNames ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var normalizedProfileIds = (profileIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var normalizedActions = (actions ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var hasExplicitSort = !string.IsNullOrWhiteSpace(sortBy);
        var parsedSortBy = ParseBatchSortBy(sortBy);

        if (normalizedPlanNames.Count == 0 && normalizedProfileIds.Count == 0 && normalizedActions.Count == 0 && !hasExplicitSort && parsedSortBy == MailMessageActionPlanBatchSortBy.Id && !descending) {
            return null;
        }

        return new MailMessageActionPlanBatchQuery {
            PlanNames = normalizedPlanNames,
            ProfileIds = normalizedProfileIds,
            Actions = normalizedActions,
            SortBy = parsedSortBy,
            Descending = descending
        };
    }

    private static MailMessageActionPlanBatchSortBy ParseBatchSortBy(string? rawSortBy) {
        if (string.IsNullOrWhiteSpace(rawSortBy)) {
            return MailMessageActionPlanBatchSortBy.Id;
        }

        return rawSortBy.Trim().ToLowerInvariant() switch {
            "id" => MailMessageActionPlanBatchSortBy.Id,
            "name" => MailMessageActionPlanBatchSortBy.Name,
            "plans" or "plan-count" => MailMessageActionPlanBatchSortBy.PlanCount,
            "ready" or "ready-count" => MailMessageActionPlanBatchSortBy.ReadyPlanCount,
            "updated" or "updated-at" => MailMessageActionPlanBatchSortBy.UpdatedAt,
            "actions" or "action-types" => MailMessageActionPlanBatchSortBy.ActionTypeCount,
            _ => throw new InvalidOperationException($"Unsupported batch sort '{rawSortBy}'.")
        };
    }

    private static MailProfileConnectionTestScope ParseConnectionTestScope(string? rawScope) {
        if (string.IsNullOrWhiteSpace(rawScope)) {
            return MailProfileConnectionTestScope.Auto;
        }

        if (Enum.TryParse<MailProfileConnectionTestScope>(rawScope.Trim(), ignoreCase: true, out var scope)) {
            return scope;
        }

        throw new InvalidOperationException($"Unsupported connection test scope '{rawScope}'.");
    }
}