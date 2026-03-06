using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Search;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Mailozaurr;

/// <summary>
/// Provides helper methods for fetching messages from IMAP or POP3 clients.
/// </summary>
public static class MessageFetcher {
    /// <summary>
    /// Fetches messages from an IMAP client using optional filters.
    /// </summary>
    /// <param name="client">The connected IMAP client.</param>
    /// <param name="folder">Optional folder to search, defaults to Inbox.</param>
    /// <param name="subject">Subject filter.</param>
    /// <param name="fromContains">Sender address filter.</param>
    /// <param name="toContains">Recipient address filter.</param>
    /// <param name="priority">Message priority filter.</param>
    /// <param name="since">Earliest delivery date.</param>
    /// <param name="before">Latest delivery date.</param>
    /// <param name="all">If set, ignores other filters.</param>
    /// <param name="delete">If set, messages are deleted after fetching.</param>
    /// <param name="hasAttachment">When set, only messages with attachments are returned.</param>
    /// <param name="additionalQueries">Additional <see cref="SearchQuery"/> filters.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Collection of matching messages.</returns>
    public static IAsyncEnumerable<ImapEmailMessage> Fetch(
        ImapClient client,
        string? folder = null,
        string? subject = null,
        string? fromContains = null,
        string? toContains = null,
        MessagePriority? priority = null,
        DateTime? since = null,
        DateTime? before = null,
        bool all = false,
        bool delete = false,
        bool hasAttachment = false,
        IEnumerable<SearchQuery>? additionalQueries = null,
        CancellationToken cancellationToken = default) =>
        Fetch(
            client,
            folder,
            subject,
            fromContains,
            toContains,
            priority,
            since,
            before,
            all,
            delete,
            hasAttachment,
            additionalQueries,
            dryRun: false,
            cancellationToken);

    /// <summary>
    /// Fetches messages from an IMAP client using optional filters, optionally simulating deletes.
    /// </summary>
    public static async IAsyncEnumerable<ImapEmailMessage> Fetch(
        ImapClient client,
        string? folder,
        string? subject,
        string? fromContains,
        string? toContains,
        MessagePriority? priority,
        DateTime? since,
        DateTime? before,
        bool all,
        bool delete,
        bool hasAttachment,
        IEnumerable<SearchQuery>? additionalQueries,
        bool dryRun,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        var performDelete = delete && !dryRun;
        IMailFolder mailFolder;
        try {
            mailFolder = client.GetCachedFolder(folder, performDelete ? FolderAccess.ReadWrite : FolderAccess.ReadOnly);
        } catch (FolderNotFoundException ex) {
            LoggingMessages.Logger.WriteError($"Failed to get folder '{folder}': {ex.Message}");
            throw;
        }

        SearchQuery query = SearchQuery.All;
        if (!all) {
            if (!string.IsNullOrWhiteSpace(subject)) {
                query = query.And(SearchQuery.SubjectContains(subject));
            }
            if (!string.IsNullOrWhiteSpace(fromContains)) {
                query = query.And(SearchQuery.FromContains(fromContains));
            }
            if (!string.IsNullOrWhiteSpace(toContains)) {
                query = query.And(SearchQuery.ToContains(toContains));
            }
            if (since.HasValue) {
                query = query.And(SearchQuery.DeliveredAfter(since.Value));
            }
            if (before.HasValue) {
                query = query.And(SearchQuery.DeliveredBefore(before.Value));
            }
        }

        if (additionalQueries != null) {
            foreach (var q in additionalQueries) {
                if (q != null) {
                    query = query.And(q);
                }
            }
        }

        var uids = await mailFolder.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        foreach (var uid in uids) {
            MimeMessage msg;
            try {
                msg = await mailFolder.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
            } catch (MessageNotFoundException ex) {
                LoggingMessages.Logger.WriteError($"Failed to get message UID {uid}: {ex.Message}");
                throw;
            }
            if (hasAttachment && !msg.Attachments.Any()) {
                continue;
            }
            if (priority.HasValue && msg.Priority != ConvertPriority(priority.Value)) {
                continue;
            }
            var reports = MimeKitUtils.GetNonDeliveryReports(msg);
            yield return new ImapEmailMessage(uid, msg, reports);
            if (performDelete) {
                await mailFolder.AddFlagsAsync(uid, MessageFlags.Deleted, true, cancellationToken).ConfigureAwait(false);
            }
        }
        if (performDelete && uids.Count > 0) {
            await mailFolder.ExpungeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Fetches messages from a POP3 client using optional filters.
    /// </summary>
    /// <param name="client">The connected POP3 client.</param>
    /// <param name="subject">Subject filter.</param>
    /// <param name="fromContains">Sender address filter.</param>
    /// <param name="toContains">Recipient address filter.</param>
    /// <param name="priority">Message priority filter.</param>
    /// <param name="since">Earliest delivery date.</param>
    /// <param name="before">Latest delivery date.</param>
    /// <param name="all">If set, ignores other filters.</param>
    /// <param name="delete">If set, messages are deleted after fetching.</param>
    /// <param name="hasAttachment">When set, only messages with attachments are returned.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Collection of matching messages.</returns>
    public static IAsyncEnumerable<Pop3EmailMessage> Fetch(
        Pop3Client client,
        string? subject = null,
        string? fromContains = null,
        string? toContains = null,
        MessagePriority? priority = null,
        DateTime? since = null,
        DateTime? before = null,
        bool all = false,
        bool delete = false,
        bool hasAttachment = false,
        CancellationToken cancellationToken = default) =>
        Fetch(
            client,
            subject,
            fromContains,
            toContains,
            priority,
            since,
            before,
            all,
            delete,
            hasAttachment,
            dryRun: false,
            cancellationToken);

    /// <summary>
    /// Fetches messages from a POP3 client using optional filters, optionally simulating deletes.
    /// </summary>
    public static async IAsyncEnumerable<Pop3EmailMessage> Fetch(
        Pop3Client client,
        string? subject,
        string? fromContains,
        string? toContains,
        MessagePriority? priority,
        DateTime? since,
        DateTime? before,
        bool all,
        bool delete,
        bool hasAttachment,
        bool dryRun,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        var performDelete = delete && !dryRun;
        for (int i = 0; i < client.Count; i++) {
            MimeMessage message;
            try {
                message = await client.GetMessageAsync(i, cancellationToken).ConfigureAwait(false);
            } catch (Pop3CommandException ex) {
                LoggingMessages.Logger.WriteError($"Failed to get message index {i}: {ex.Message}");
                throw;
            }

            if (!all) {
                if (!string.IsNullOrWhiteSpace(subject) && (message.Subject == null || message.Subject.IndexOf(subject, StringComparison.OrdinalIgnoreCase) < 0)) {
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(fromContains) && !AddressMatches(message.From, fromContains!)) {
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(toContains) && !AddressMatches(message.To, toContains!)) {
                    continue;
                }
                var msgDate = message.Date.DateTime;
                if (since.HasValue && msgDate < since.Value) {
                    continue;
                }
                if (before.HasValue && msgDate > before.Value) {
                    continue;
                }
                if (priority.HasValue && message.Priority != ConvertPriority(priority.Value)) {
                    continue;
                }
            }

            if (hasAttachment && !message.Attachments.Any()) {
                continue;
            }

            var reports = MimeKitUtils.GetNonDeliveryReports(message);
            yield return new Pop3EmailMessage(i, message, reports);
            if (performDelete) {
                await client.DeleteMessageAsync(i, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static MimeKit.MessagePriority ConvertPriority(MessagePriority priority) {
        return priority switch {
            MessagePriority.High => MimeKit.MessagePriority.Urgent,
            MessagePriority.Low => MimeKit.MessagePriority.NonUrgent,
            _ => MimeKit.MessagePriority.Normal,
        };
    }

    private static bool AddressMatches(InternetAddressList list, string filter) {
        foreach (var addr in list.Mailboxes) {
            if (!string.IsNullOrWhiteSpace(addr.Address) &&
                addr.Address.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }
            var displayName = addr.Name;
            if (!string.IsNullOrWhiteSpace(displayName) &&
                displayName!.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }
        }
        return false;
    }
}
