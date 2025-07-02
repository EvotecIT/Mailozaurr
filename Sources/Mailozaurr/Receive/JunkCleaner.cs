using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Helper methods for clearing junk mail folders.
/// </summary>
public static class JunkCleaner {
    /// <summary>
    /// Deletes all messages from the specified junk folder.
    /// </summary>
    /// <param name="client">Connected IMAP client.</param>
    /// <param name="folder">Folder name or <c>null</c> for "Junk".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task ClearImapJunkAsync(
        ImapClient client,
        string? folder = null,
        IEnumerable<string>? skipFrom = null,
        IEnumerable<string>? skipTo = null,
        IEnumerable<string>? skipSubjectContains = null,
        IEnumerable<string>? skipMessageId = null,
        IEnumerable<UniqueId>? skipUid = null,
        bool skipHasAttachment = false,
        IEnumerable<string>? skipAttachmentExtension = null,
        CancellationToken cancellationToken = default) {
        var junk = client.GetCachedFolder(folder ?? "Junk", FolderAccess.ReadWrite);
        var uids = await junk.SearchAsync(SearchQuery.All, cancellationToken).ConfigureAwait(false);
        if (uids.Count == 0) {
            return;
        }

        var skipUidSet = skipUid != null ? new HashSet<UniqueId>(skipUid) : null;
        var toDelete = new List<UniqueId>(uids.Count);
        foreach (var uid in uids) {
            if (skipUidSet != null && skipUidSet.Contains(uid)) continue;
            var message = await junk.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
            if (skipMessageId != null && message.MessageId != null && skipMessageId.Contains(message.MessageId)) continue;
            if (skipFrom != null && message.From.Mailboxes.Any(m => skipFrom.Contains(m.Address, StringComparer.OrdinalIgnoreCase))) continue;
            if (skipTo != null && message.To.Mailboxes.Any(m => skipTo.Contains(m.Address, StringComparer.OrdinalIgnoreCase))) continue;
            if (skipSubjectContains != null && message.Subject != null && skipSubjectContains.Any(s => message.Subject.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
            if (skipHasAttachment && message.Attachments.Any()) continue;
            if (skipAttachmentExtension != null && message.Attachments.OfType<MimeKit.MimePart>().Any(att =>
                    skipAttachmentExtension.Contains(
                        System.IO.Path.GetExtension(att.FileName ?? string.Empty).TrimStart('.'),
                        StringComparer.OrdinalIgnoreCase))) {
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
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async IAsyncEnumerable<ImapEmailMessage> GetImapJunkAsync(
        ImapClient client,
        string? folder = null,
        IEnumerable<string>? skipFrom = null,
        IEnumerable<string>? skipTo = null,
        IEnumerable<string>? skipSubjectContains = null,
        IEnumerable<string>? skipMessageId = null,
        IEnumerable<UniqueId>? skipUid = null,
        bool skipHasAttachment = false,
        IEnumerable<string>? skipAttachmentExtension = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        var junk = client.GetCachedFolder(folder ?? "Junk", FolderAccess.ReadOnly);
        var uids = await junk.SearchAsync(SearchQuery.All, cancellationToken).ConfigureAwait(false);
        var skipUidSet = skipUid != null ? new HashSet<UniqueId>(skipUid) : null;
        foreach (var uid in uids) {
            if (skipUidSet != null && skipUidSet.Contains(uid)) continue;
            var message = await junk.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
            if (skipMessageId != null && message.MessageId != null && skipMessageId.Contains(message.MessageId)) continue;
            if (skipFrom != null && message.From.Mailboxes.Any(m => skipFrom.Contains(m.Address, StringComparer.OrdinalIgnoreCase))) continue;
            if (skipTo != null && message.To.Mailboxes.Any(m => skipTo.Contains(m.Address, StringComparer.OrdinalIgnoreCase))) continue;
            if (skipSubjectContains != null && message.Subject != null && skipSubjectContains.Any(s => message.Subject.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
            if (skipHasAttachment && message.Attachments.Any()) continue;
            if (skipAttachmentExtension != null && message.Attachments.OfType<MimeKit.MimePart>().Any(att =>
                    skipAttachmentExtension.Contains(
                        System.IO.Path.GetExtension(att.FileName ?? string.Empty).TrimStart('.'),
                        StringComparer.OrdinalIgnoreCase))) {
                continue;
            }
            yield return new ImapEmailMessage(uid, message);
        }
    }
}
