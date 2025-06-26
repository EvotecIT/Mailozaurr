using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Search;
using MimeKit;
using System;
using System.Collections.Generic;

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
    /// <returns>Collection of matching messages.</returns>
    public static IEnumerable<ImapEmailMessage> Fetch(
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
        bool hasAttachment = false) {
        IMailFolder mailFolder = client.Inbox;
        if (!string.IsNullOrEmpty(folder)) {
            try {
                mailFolder = client.GetFolder(folder);
            } catch (Exception ex) {
                LoggingMessages.Logger.WriteError($"Failed to get folder '{folder}': {ex.Message}");
                try {
                    if (client.PersonalNamespaces.Count > 0) {
                        mailFolder = client.GetFolder(client.PersonalNamespaces[0]).GetSubfolder(folder);
                    } else {
                        LoggingMessages.Logger.WriteError("Personal namespaces list is empty. Unable to access subfolder.");
                        throw;
                    }
                } catch (Exception innerEx) {
                    LoggingMessages.Logger.WriteError($"Failed to get subfolder '{folder}': {innerEx.Message}");
                    throw;
                }
            }
        }

        mailFolder.Open(delete ? FolderAccess.ReadWrite : FolderAccess.ReadOnly);

        SearchQuery query = SearchQuery.All;
        if (!all) {
            if (!string.IsNullOrEmpty(subject)) {
                query = query.And(SearchQuery.SubjectContains(subject));
            }
            if (!string.IsNullOrEmpty(fromContains)) {
                query = query.And(SearchQuery.FromContains(fromContains));
            }
            if (!string.IsNullOrEmpty(toContains)) {
                query = query.And(SearchQuery.ToContains(toContains));
            }
            if (since.HasValue) {
                query = query.And(SearchQuery.DeliveredAfter(since.Value));
            }
            if (before.HasValue) {
                query = query.And(SearchQuery.DeliveredBefore(before.Value));
            }
        }

        var uids = mailFolder.Search(query);
        foreach (var uid in uids) {
            MimeMessage msg;
            try {
                msg = mailFolder.GetMessage(uid);
            } catch (Exception ex) {
                LoggingMessages.Logger.WriteError($"Failed to get message UID {uid}: {ex.Message}");
                throw;
            }
            if (hasAttachment && !msg.Attachments.Any()) {
                continue;
            }
            if (priority.HasValue && msg.Priority != ConvertPriority(priority.Value)) {
                continue;
            }
            yield return new ImapEmailMessage(uid, msg);
            if (delete) {
                mailFolder.AddFlags(uid, MessageFlags.Deleted, true);
            }
        }
        if (delete && uids.Count > 0) {
            mailFolder.Expunge();
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
    /// <returns>Collection of matching messages.</returns>
    public static IEnumerable<Pop3EmailMessage> Fetch(
        Pop3Client client,
        string? subject = null,
        string? fromContains = null,
        string? toContains = null,
        MessagePriority? priority = null,
        DateTime? since = null,
        DateTime? before = null,
        bool all = false,
        bool delete = false,
        bool hasAttachment = false) {
        for (int i = 0; i < client.Count; i++) {
            MimeMessage message;
            try {
                message = client.GetMessage(i);
            } catch (Exception ex) {
                LoggingMessages.Logger.WriteError($"Failed to get message index {i}: {ex.Message}");
                throw;
            }

            if (!all) {
                if (!string.IsNullOrEmpty(subject) && (message.Subject == null || message.Subject.IndexOf(subject, StringComparison.OrdinalIgnoreCase) < 0)) {
                    continue;
                }
                if (!string.IsNullOrEmpty(fromContains) && !AddressMatches(message.From, fromContains)) {
                    continue;
                }
                if (!string.IsNullOrEmpty(toContains) && !AddressMatches(message.To, toContains)) {
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

            yield return new Pop3EmailMessage(i, message);
            if (delete) {
                client.DeleteMessage(i);
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
            if (addr.Address.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }
            if (!string.IsNullOrEmpty(addr.Name) && addr.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }
        }
        return false;
    }
}
