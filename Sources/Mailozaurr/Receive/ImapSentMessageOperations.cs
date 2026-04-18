using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;

namespace Mailozaurr;

/// <summary>
/// IMAP helper operations used by send/idempotency pipelines around Sent-folder behavior.
/// </summary>
public static class ImapSentMessageOperations {
    /// <summary>
    /// Abstraction over IMAP sent-folder operations for testability.
    /// </summary>
    public interface IImapSentFolder {
        /// <summary>Folder full name.</summary>
        string FullName { get; }

        /// <summary>True when folder is open.</summary>
        bool IsOpen { get; }

        /// <summary>Current folder access when opened.</summary>
        FolderAccess Access { get; }

        /// <summary>Opens the folder with requested access.</summary>
        Task OpenAsync(FolderAccess access, CancellationToken cancellationToken = default);

        /// <summary>Closes the folder.</summary>
        Task CloseAsync(bool expunge, CancellationToken cancellationToken = default);

        /// <summary>Appends a MIME message.</summary>
        Task AppendAsync(MimeMessage message, MessageFlags flags, CancellationToken cancellationToken = default);

        /// <summary>Searches for matching message UIDs.</summary>
        Task<IList<UniqueId>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default);

        /// <summary>Fetches envelope message-id for a single UID.</summary>
        Task<string?> FetchEnvelopeMessageIdAsync(UniqueId uid, CancellationToken cancellationToken = default);

        /// <summary>Fetches threading metadata for a single UID.</summary>
        Task<ImapThreadingMetadataResult?> FetchThreadingMetadataAsync(UniqueId uid, CancellationToken cancellationToken = default);
    }

    private sealed class MailKitSentFolderAdapter : IImapSentFolder {
        private readonly IMailFolder _folder;

        internal MailKitSentFolderAdapter(IMailFolder folder) {
            _folder = folder ?? throw new ArgumentNullException(nameof(folder));
        }

        public string FullName => _folder.FullName;

        public bool IsOpen => _folder.IsOpen;

        public FolderAccess Access => _folder.Access;

        public Task OpenAsync(FolderAccess access, CancellationToken cancellationToken = default) =>
            _folder.OpenAsync(access, cancellationToken);

        public Task CloseAsync(bool expunge, CancellationToken cancellationToken = default) =>
            _folder.CloseAsync(expunge, cancellationToken);

        public Task AppendAsync(MimeMessage message, MessageFlags flags, CancellationToken cancellationToken = default) =>
            _folder.AppendAsync(message, flags, cancellationToken);

        public Task<IList<UniqueId>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default) =>
            _folder.SearchAsync(query, cancellationToken);

        public async Task<string?> FetchEnvelopeMessageIdAsync(UniqueId uid, CancellationToken cancellationToken = default) {
            var summaries = await _folder.FetchAsync(new[] { uid }, MessageSummaryItems.Envelope, cancellationToken).ConfigureAwait(false);
            if (summaries == null || summaries.Count == 0) {
                return null;
            }

            return NormalizeOptional(summaries[0].Envelope?.MessageId);
        }

        public async Task<ImapThreadingMetadataResult?> FetchThreadingMetadataAsync(UniqueId uid, CancellationToken cancellationToken = default) {
            var summaries = await _folder.FetchAsync(
                new[] { uid },
                MessageSummaryItems.Envelope | MessageSummaryItems.References | MessageSummaryItems.UniqueId,
                cancellationToken).ConfigureAwait(false);
            if (summaries == null || summaries.Count == 0) {
                return null;
            }

            var summary = summaries[0];
            var env = summary.Envelope;
            return new ImapThreadingMetadataResult {
                MessageId = NormalizeOptional(env?.MessageId),
                ReplyTo = NormalizeOptional(env?.ReplyTo?.ToString()),
                Cc = NormalizeOptional(env?.Cc?.ToString()),
                InReplyTo = NormalizeOptional(env?.InReplyTo),
                References = summary.References is null ? null : new List<string>(summary.References)
            };
        }
    }

    /// <summary>
    /// Result of Sent-folder append attempt.
    /// </summary>
    public sealed class ImapSentAppendResult {
        /// <summary>True when append succeeded.</summary>
        public bool Appended { get; set; }

        /// <summary>Resolved folder name where message was appended.</summary>
        public string? Folder { get; set; }
    }

    /// <summary>
    /// Result of duplicate probe in Sent folder.
    /// </summary>
    public sealed class ImapSentDuplicateProbeResult {
        /// <summary>True when duplicate was found.</summary>
        public bool IsMatch { get; set; }

        /// <summary>Resolved folder name that was probed.</summary>
        public string? Folder { get; set; }

        /// <summary>Matched message-id when available.</summary>
        public string? MessageId { get; set; }
    }

    /// <summary>
    /// Threading metadata fetched from IMAP summary headers.
    /// </summary>
    public sealed class ImapThreadingMetadataResult {
        /// <summary>Message-Id value.</summary>
        public string? MessageId { get; set; }

        /// <summary>Reply-To value.</summary>
        public string? ReplyTo { get; set; }

        /// <summary>Cc value.</summary>
        public string? Cc { get; set; }

        /// <summary>In-Reply-To value.</summary>
        public string? InReplyTo { get; set; }

        /// <summary>References values.</summary>
        public List<string>? References { get; set; }
    }

    /// <summary>
    /// Appends a MIME message to Sent using resolved Sent-folder selection.
    /// </summary>
    public static async Task<ImapSentAppendResult> AppendToSentAsync(
        ImapClient client,
        MimeMessage message,
        string? requestedSentFolder = null,
        string? configuredSentFolder = null,
        string fallbackFolder = "Sent",
        CancellationToken cancellationToken = default) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }

        var sentFolder = await ImapSentFolderResolver.ResolveSentFolderNameAsync(
            client,
            requestedSentFolder,
            configuredSentFolder,
            fallbackFolder,
            cancellationToken).ConfigureAwait(false);
        var folder = new MailKitSentFolderAdapter(client.GetCachedFolder(sentFolder, FolderAccess.ReadOnly));
        return await AppendToSentAsync(folder, message, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Appends a MIME message to Sent using an abstract folder.
    /// </summary>
    public static async Task<ImapSentAppendResult> AppendToSentAsync(
        IImapSentFolder folder,
        MimeMessage message,
        CancellationToken cancellationToken = default) {
        if (folder == null) {
            throw new ArgumentNullException(nameof(folder));
        }
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }

        await EnsureFolderAccessAsync(folder, FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
        await folder.AppendAsync(message, MessageFlags.Seen, cancellationToken).ConfigureAwait(false);
        return new ImapSentAppendResult {
            Appended = true,
            Folder = folder.FullName
        };
    }

    /// <summary>
    /// Probes Sent for duplicate send marker by idempotency header, with optional Message-Id fallback.
    /// </summary>
    public static async Task<ImapSentDuplicateProbeResult> FindSentDuplicateAsync(
        ImapClient client,
        string idempotencyHeaderName,
        string idempotencyKey,
        string? messageIdToken,
        string? requestedSentFolder = null,
        string? configuredSentFolder = null,
        string fallbackFolder = "Sent",
        CancellationToken cancellationToken = default) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }

        var sentFolder = await ImapSentFolderResolver.ResolveSentFolderNameAsync(
            client,
            requestedSentFolder,
            configuredSentFolder,
            fallbackFolder,
            cancellationToken).ConfigureAwait(false);
        var folder = new MailKitSentFolderAdapter(client.GetCachedFolder(sentFolder, FolderAccess.ReadOnly));
        var result = await FindSentDuplicateAsync(
            folder,
            idempotencyHeaderName,
            idempotencyKey,
            messageIdToken,
            cancellationToken).ConfigureAwait(false);
        result.Folder = sentFolder;
        return result;
    }

    /// <summary>
    /// Probes Sent for duplicate send marker by idempotency header, with optional Message-Id fallback.
    /// </summary>
    public static async Task<ImapSentDuplicateProbeResult> FindSentDuplicateAsync(
        IImapSentFolder folder,
        string idempotencyHeaderName,
        string idempotencyKey,
        string? messageIdToken,
        CancellationToken cancellationToken = default) {
        if (folder == null) {
            throw new ArgumentNullException(nameof(folder));
        }
        if (string.IsNullOrWhiteSpace(idempotencyHeaderName)) {
            throw new ArgumentException("idempotencyHeaderName is required.", nameof(idempotencyHeaderName));
        }
        if (string.IsNullOrWhiteSpace(idempotencyKey)) {
            throw new ArgumentException("idempotencyKey is required.", nameof(idempotencyKey));
        }

        await EnsureFolderAccessAsync(folder, FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);

        var uids = await folder.SearchAsync(
            SearchQuery.HeaderContains(idempotencyHeaderName.Trim(), idempotencyKey.Trim()),
            cancellationToken).ConfigureAwait(false);
        if (uids.Count == 0) {
            var normalizedMessageId = NormalizeMessageIdToken(messageIdToken);
            if (!string.IsNullOrWhiteSpace(normalizedMessageId)) {
                uids = await folder.SearchAsync(
                    SearchQuery.HeaderContains("Message-Id", normalizedMessageId!),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        if (uids.Count == 0) {
            return new ImapSentDuplicateProbeResult();
        }

        var matchedMessageId = await folder.FetchEnvelopeMessageIdAsync(uids[0], cancellationToken).ConfigureAwait(false);
        return new ImapSentDuplicateProbeResult {
            IsMatch = true,
            Folder = folder.FullName,
            MessageId = string.IsNullOrWhiteSpace(matchedMessageId) ? messageIdToken : matchedMessageId
        };
    }

    /// <summary>
    /// Gets threading metadata for a message identified by folder + UID.
    /// </summary>
    public static async Task<ImapThreadingMetadataResult?> GetThreadingMetadataAsync(
        ImapClient client,
        string folder,
        uint uid,
        CancellationToken cancellationToken = default) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (string.IsNullOrWhiteSpace(folder)) {
            throw new ArgumentException("folder is required.", nameof(folder));
        }
        if (uid == 0) {
            throw new ArgumentOutOfRangeException(nameof(uid), "uid must be greater than zero.");
        }

        var mailFolder = new MailKitSentFolderAdapter(client.GetCachedFolder(folder, FolderAccess.ReadOnly));
        return await GetThreadingMetadataAsync(mailFolder, new UniqueId(uid), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets threading metadata for a message in an abstract folder.
    /// </summary>
    public static async Task<ImapThreadingMetadataResult?> GetThreadingMetadataAsync(
        IImapSentFolder folder,
        UniqueId uid,
        CancellationToken cancellationToken = default) {
        if (folder == null) {
            throw new ArgumentNullException(nameof(folder));
        }

        await EnsureFolderAccessAsync(folder, FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
        return await folder.FetchThreadingMetadataAsync(uid, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Normalizes a message-id token by trimming whitespace and angle brackets.
    /// </summary>
    public static string? NormalizeMessageIdToken(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        var token = value!.Trim();
        if (token.StartsWith("<", StringComparison.Ordinal)) {
            token = token.Substring(1);
        }
        if (token.EndsWith(">", StringComparison.Ordinal)) {
            token = token.Substring(0, token.Length - 1);
        }
        token = token.Trim();
        return token.Length == 0 ? null : token;
    }

    private static async Task EnsureFolderAccessAsync(
        IImapSentFolder folder,
        FolderAccess access,
        CancellationToken cancellationToken) {
        if (folder.IsOpen && folder.Access != access) {
            try {
                await folder.CloseAsync(expunge: false, cancellationToken).ConfigureAwait(false);
            } catch {
                // best-effort
            }
        }

        if (!folder.IsOpen || folder.Access != access) {
            await folder.OpenAsync(access, cancellationToken).ConfigureAwait(false);
        }

        if (!folder.IsOpen || folder.Access != access) {
            throw new InvalidOperationException($"Folder is not currently open in {access} mode.");
        }
    }

    private static string? NormalizeOptional(string? value) {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
