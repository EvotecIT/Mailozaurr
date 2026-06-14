using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Helper methods for clearing junk mail folders.
/// </summary>
public static class JunkCleaner {
    private sealed class JunkSkipCriteria {
        public HashSet<string>? MessageIds { get; init; }
        public HashSet<uint>? Uids { get; init; }
        public HashSet<string>? From { get; init; }
        public HashSet<string>? To { get; init; }
        public string[]? SubjectTokens { get; init; }
        public HashSet<string>? AttachmentExtensions { get; init; }
    }

    /// <summary>
    /// Deletes all messages from the specified junk folder.
    /// </summary>
    /// <param name="client">Connected IMAP client.</param>
    /// <param name="folder">Folder name or <c>null</c> for "Junk".</param>
    /// <param name="skipFrom">Sender addresses to exclude.</param>
    /// <param name="skipTo">Recipient addresses to exclude.</param>
    /// <param name="skipSubjectContains">Subjects that, if contained, will exclude the message.</param>
    /// <param name="skipMessageId">Message IDs to exclude.</param>
    /// <param name="skipUid">Message UIDs to exclude.</param>
    /// <param name="skipHasAttachment">When set, skip messages that contain attachments.</param>
    /// <param name="skipAttachmentExtension">Attachment extensions that, if present, will cause skipping.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task ClearImapJunkAsync(
        ImapClient client,
        string? folder = null,
        IEnumerable<string>? skipFrom = null,
        IEnumerable<string>? skipTo = null,
        IEnumerable<string>? skipSubjectContains = null,
        IEnumerable<string>? skipMessageId = null,
        IEnumerable<uint>? skipUid = null,
        bool skipHasAttachment = false,
        IEnumerable<string>? skipAttachmentExtension = null,
        CancellationToken cancellationToken = default) {
        await ClearImapJunkAsync(
            client,
            folder,
            skipFrom,
            skipTo,
            skipSubjectContains,
            skipMessageId,
            skipUid,
            skipHasAttachment,
            skipAttachmentExtension,
            dryRun: false,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes all messages from the specified junk folder, optionally simulating the change.
    /// </summary>
    public static async Task ClearImapJunkAsync(
        ImapClient client,
        string? folder,
        IEnumerable<string>? skipFrom,
        IEnumerable<string>? skipTo,
        IEnumerable<string>? skipSubjectContains,
        IEnumerable<string>? skipMessageId,
        IEnumerable<uint>? skipUid,
        bool skipHasAttachment,
        IEnumerable<string>? skipAttachmentExtension,
        bool dryRun,
        CancellationToken cancellationToken = default) {
        if (dryRun) {
            return;
        }
        var junk = client.GetCachedFolder(folder ?? "Junk", FolderAccess.ReadWrite);
        var uids = await junk.SearchAsync(SearchQuery.All, cancellationToken).ConfigureAwait(false);
        if (uids.Count == 0) {
            return;
        }

        var criteria = CreateSkipCriteria(skipFrom, skipTo, skipSubjectContains, skipMessageId, skipUid, skipAttachmentExtension);
        var toDelete = new List<UniqueId>(uids.Count);
        foreach (var uid in uids) {
            if (criteria.Uids != null && criteria.Uids.Contains(uid.Id)) continue;
            var message = await junk.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
            if (ShouldSkipMessage(message, skipHasAttachment, criteria)) {
                continue;
            }
            toDelete.Add(uid);
        }

        if (toDelete.Count == 0) return;

        await junk.AddFlagsAsync(toDelete, MessageFlags.Deleted, true, cancellationToken).ConfigureAwait(false);
        await junk.ExpungeAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves messages from the specified junk folder without deleting them.
    /// </summary>
    /// <param name="client">Connected IMAP client.</param>
    /// <param name="folder">Folder name or <c>null</c> for "Junk".</param>
    /// <param name="skipFrom">Sender addresses to exclude.</param>
    /// <param name="skipTo">Recipient addresses to exclude.</param>
    /// <param name="skipSubjectContains">Subjects that, if contained, will exclude the message.</param>
    /// <param name="skipMessageId">Message IDs to exclude.</param>
    /// <param name="skipUid">Message UIDs to exclude.</param>
    /// <param name="skipHasAttachment">When set, skip messages that contain attachments.</param>
    /// <param name="skipAttachmentExtension">Attachment extensions that, if present, will cause skipping.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async IAsyncEnumerable<ImapEmailMessage> GetImapJunkAsync(
        ImapClient client,
        string? folder = null,
        IEnumerable<string>? skipFrom = null,
        IEnumerable<string>? skipTo = null,
        IEnumerable<string>? skipSubjectContains = null,
        IEnumerable<string>? skipMessageId = null,
        IEnumerable<uint>? skipUid = null,
        bool skipHasAttachment = false,
        IEnumerable<string>? skipAttachmentExtension = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        var junk = client.GetCachedFolder(folder ?? "Junk", FolderAccess.ReadOnly);
        var uids = await junk.SearchAsync(SearchQuery.All, cancellationToken).ConfigureAwait(false);
        var criteria = CreateSkipCriteria(skipFrom, skipTo, skipSubjectContains, skipMessageId, skipUid, skipAttachmentExtension);
        foreach (var uid in uids) {
            if (criteria.Uids != null && criteria.Uids.Contains(uid.Id)) continue;
            var message = await junk.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
            if (ShouldSkipMessage(message, skipHasAttachment, criteria)) {
                continue;
            }
            yield return new ImapEmailMessage(uid, message);
        }
    }

    private static JunkSkipCriteria CreateSkipCriteria(
        IEnumerable<string>? skipFrom,
        IEnumerable<string>? skipTo,
        IEnumerable<string>? skipSubjectContains,
        IEnumerable<string>? skipMessageId,
        IEnumerable<uint>? skipUid,
        IEnumerable<string>? skipAttachmentExtension) =>
        new() {
            MessageIds = skipMessageId != null ? new HashSet<string>(skipMessageId, StringComparer.OrdinalIgnoreCase) : null,
            Uids = skipUid != null ? new HashSet<uint>(skipUid) : null,
            From = skipFrom != null ? new HashSet<string>(skipFrom, StringComparer.OrdinalIgnoreCase) : null,
            To = skipTo != null ? new HashSet<string>(skipTo, StringComparer.OrdinalIgnoreCase) : null,
            SubjectTokens = skipSubjectContains?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray(),
            AttachmentExtensions = NormalizeAttachmentExtensions(skipAttachmentExtension)
        };

    private static HashSet<string>? NormalizeAttachmentExtensions(IEnumerable<string>? skipAttachmentExtension) {
        if (skipAttachmentExtension == null) {
            return null;
        }

        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in skipAttachmentExtension) {
            if (string.IsNullOrWhiteSpace(extension)) {
                continue;
            }

            var value = extension.Trim().TrimStart('.');
            if (value.Length > 0) {
                normalized.Add(value);
            }
        }

        return normalized;
    }

    private static bool ShouldSkipMessage(MimeMessage message, bool skipHasAttachment, JunkSkipCriteria criteria) {
        if (criteria.MessageIds != null && message.MessageId != null && criteria.MessageIds.Contains(message.MessageId)) {
            return true;
        }

        if (criteria.From != null && message.From.Mailboxes.Any(mailbox => criteria.From.Contains(mailbox.Address))) {
            return true;
        }

        if (criteria.To != null && message.To.Mailboxes.Any(mailbox => criteria.To.Contains(mailbox.Address))) {
            return true;
        }

        if (criteria.SubjectTokens != null && message.Subject != null &&
            criteria.SubjectTokens.Any(token => message.Subject.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)) {
            return true;
        }

        if (skipHasAttachment && message.Attachments.Any()) {
            return true;
        }

        if (criteria.AttachmentExtensions != null && message.Attachments.OfType<MimePart>().Any(att =>
                criteria.AttachmentExtensions.Contains(System.IO.Path.GetExtension(att.FileName ?? string.Empty).TrimStart('.')))) {
            return true;
        }

        return false;
    }
}