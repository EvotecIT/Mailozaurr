using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Pop3;
using MimeKit;
using MimeKit.Utils;

namespace Mailozaurr;

/// <summary>
/// Lightweight POP3 mailbox browsing helpers that avoid downloading full messages when possible.
/// </summary>
public static class Pop3MailboxBrowser {
    /// <summary>
    /// Represents the outcome of resolving a POP3 message request.
    /// </summary>
    public enum Pop3MessageResolveStatus {
        /// <summary>Message was resolved successfully.</summary>
        Success,
        /// <summary>Neither index nor uid was provided.</summary>
        MissingIdentifier,
        /// <summary>Provided index is invalid (negative).</summary>
        InvalidIndex,
        /// <summary>UID lookup is not supported by the POP3 server.</summary>
        UidLookupUnsupported,
        /// <summary>Message was not found.</summary>
        NotFound
    }

    /// <summary>Summary of a POP3 message derived from message headers.</summary>
    public sealed record Pop3MessageHeaderSnapshot(
        int Index,
        string? Uid,
        long? MessageSize,
        string? MessageId,
        string From,
        string To,
        string? Subject,
        DateTime DateUtc,
        bool HasAttachmentsHint);

    /// <summary>Response envelope for POP3 header listing.</summary>
    public sealed record Pop3ListSnapshot(
        int TotalCount,
        IReadOnlyList<Pop3MessageHeaderSnapshot> Messages);

    /// <summary>Resolved message snapshot for POP3.</summary>
    public sealed record Pop3ResolvedMessageSnapshot(
        int Index,
        string? Uid,
        long? MessageSize,
        MimeMessage Message);

    /// <summary>Result of resolving a POP3 message request.</summary>
    public sealed record Pop3MessageResolveResult(
        Pop3MessageResolveStatus Status,
        Pop3ResolvedMessageSnapshot? Snapshot);

    /// <summary>
    /// Lists messages as header-only snapshots, starting from the newest message.
    /// </summary>
    /// <remarks>
    /// POP3 servers expose messages using zero-based indices. This method iterates from newest to oldest, applying an offset
    /// (from the newest message) and returning up to <paramref name="limit"/> messages.
    /// </remarks>
    public static Task<Pop3ListSnapshot> ListMessageHeadersAsync(
        Pop3Client client,
        int limit,
        int offset,
        CancellationToken cancellationToken = default) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }
        return ListMessageHeadersCoreAsync(new MailKitPop3MailboxClient(client), limit, offset, cancellationToken);
    }

    /// <summary>
    /// Resolves and downloads a single message by index or UIDL value.
    /// </summary>
    public static Task<Pop3MessageResolveResult> ResolveMessageAsync(
        Pop3Client client,
        int? requestedIndex,
        string? requestedUid,
        CancellationToken cancellationToken = default) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }
        return ResolveMessageCoreAsync(new MailKitPop3MailboxClient(client), requestedIndex, requestedUid, cancellationToken);
    }

    internal interface IPop3MailboxClient {
        int Count { get; }
        Task<HeaderList> GetMessageHeadersAsync(int index, CancellationToken cancellationToken);
        Task<MimeMessage> GetMessageAsync(int index, CancellationToken cancellationToken);
        Task<string> GetMessageUidAsync(int index, CancellationToken cancellationToken);
        Task<IList<string>> GetMessageUidsAsync(CancellationToken cancellationToken);
        long GetMessageSize(int index, CancellationToken cancellationToken);
    }

    internal sealed class MailKitPop3MailboxClient : IPop3MailboxClient {
        private readonly Pop3Client _client;

        internal MailKitPop3MailboxClient(Pop3Client client) {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public int Count => _client.Count;

        public Task<HeaderList> GetMessageHeadersAsync(int index, CancellationToken cancellationToken) =>
            _client.GetMessageHeadersAsync(index, cancellationToken);

        public Task<MimeMessage> GetMessageAsync(int index, CancellationToken cancellationToken) =>
            _client.GetMessageAsync(index, cancellationToken);

        public Task<string> GetMessageUidAsync(int index, CancellationToken cancellationToken) =>
            _client.GetMessageUidAsync(index, cancellationToken);

        public Task<IList<string>> GetMessageUidsAsync(CancellationToken cancellationToken) =>
            _client.GetMessageUidsAsync(cancellationToken);

        public long GetMessageSize(int index, CancellationToken cancellationToken) =>
            _client.GetMessageSize(index, cancellationToken);
    }

    internal static async Task<Pop3ListSnapshot> ListMessageHeadersCoreAsync(
        IPop3MailboxClient client,
        int limit,
        int offset,
        CancellationToken cancellationToken) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (limit < 1) {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }
        if (offset < 0) {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var totalCount = client.Count;
        var messages = new List<Pop3MessageHeaderSnapshot>();
        var start = totalCount - 1 - offset;
        for (var index = start; index >= 0 && messages.Count < limit; index--) {
            var headers = await client.GetMessageHeadersAsync(index, cancellationToken).ConfigureAwait(false);
            var uid = await TryGetMessageUidAsync(client, index, cancellationToken).ConfigureAwait(false);
            var messageSize = TryGetMessageSize(client, index, cancellationToken);

            messages.Add(new Pop3MessageHeaderSnapshot(
                Index: index,
                Uid: uid,
                MessageSize: messageSize,
                MessageId: NormalizeMessageIdValue(headers[HeaderId.MessageId]),
                From: headers[HeaderId.From] ?? string.Empty,
                To: headers[HeaderId.To] ?? string.Empty,
                Subject: string.IsNullOrWhiteSpace(headers[HeaderId.Subject]) ? null : headers[HeaderId.Subject],
                DateUtc: ParseHeaderDateUtc(headers[HeaderId.Date]),
                HasAttachmentsHint: HasAttachmentHint(headers)));
        }

        return new Pop3ListSnapshot(totalCount, messages);
    }

    internal static async Task<Pop3MessageResolveResult> ResolveMessageCoreAsync(
        IPop3MailboxClient client,
        int? requestedIndex,
        string? requestedUid,
        CancellationToken cancellationToken) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }

        var uid = NormalizeOptional(requestedUid);
        if (requestedIndex is null && string.IsNullOrWhiteSpace(uid)) {
            return new Pop3MessageResolveResult(Pop3MessageResolveStatus.MissingIdentifier, null);
        }

        if (requestedIndex is not null && requestedIndex.Value < 0) {
            return new Pop3MessageResolveResult(Pop3MessageResolveStatus.InvalidIndex, null);
        }

        var resolvedIndex = requestedIndex ?? -1;
        if (!string.IsNullOrWhiteSpace(uid)) {
            var lookup = await TryResolveMessageIndexByUidAsync(client, uid!, cancellationToken).ConfigureAwait(false);
            if (!lookup.Supported) {
                return new Pop3MessageResolveResult(Pop3MessageResolveStatus.UidLookupUnsupported, null);
            }
            if (lookup.Index is null) {
                return new Pop3MessageResolveResult(Pop3MessageResolveStatus.NotFound, null);
            }
            resolvedIndex = lookup.Index.Value;
        }

        var totalCount = client.Count;
        if (resolvedIndex < 0 || resolvedIndex >= totalCount) {
            return new Pop3MessageResolveResult(Pop3MessageResolveStatus.NotFound, null);
        }

        var resolvedUid = string.IsNullOrWhiteSpace(uid)
            ? await TryGetMessageUidAsync(client, resolvedIndex, cancellationToken).ConfigureAwait(false)
            : uid;
        var messageSize = TryGetMessageSize(client, resolvedIndex, cancellationToken);
        var message = await client.GetMessageAsync(resolvedIndex, cancellationToken).ConfigureAwait(false);

        return new Pop3MessageResolveResult(
            Pop3MessageResolveStatus.Success,
            new Pop3ResolvedMessageSnapshot(resolvedIndex, resolvedUid, messageSize, message));
    }

    internal static string? NormalizeOptional(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();

    internal static string? NormalizeMessageIdValue(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }
        var trimmed = value!.Trim();
        if (trimmed.Length > 1 && trimmed[0] == '<' && trimmed[trimmed.Length - 1] == '>') {
            trimmed = trimmed.Substring(1, trimmed.Length - 2);
        }
        return trimmed;
    }

    private static DateTime ParseHeaderDateUtc(string? raw) {
        if (string.IsNullOrWhiteSpace(raw)) {
            return DateTime.UtcNow;
        }

        // Prefer RFC822/RFC2822 parsing.
        if (DateUtils.TryParse(raw, out var parsed)) {
            return parsed.UtcDateTime;
        }

        // As a fallback (non-standard servers), attempt a broad parse.
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dt)) {
            return dt.UtcDateTime;
        }

        return DateTime.UtcNow;
    }

    private static bool HasAttachmentHint(HeaderList headers) {
        var contentType = headers[HeaderId.ContentType] ?? string.Empty;
        var contentDisposition = headers[HeaderId.ContentDisposition] ?? string.Empty;
        if (contentDisposition.Contains("attachment", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }
        if (contentType.Contains("multipart/mixed", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("multipart/related", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("name=", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }
        return false;
    }

    private static async Task<Pop3UidLookupResult> TryResolveMessageIndexByUidAsync(
        IPop3MailboxClient client,
        string uid,
        CancellationToken cancellationToken) {
        try {
            var uids = await client.GetMessageUidsAsync(cancellationToken).ConfigureAwait(false);
            for (var i = 0; i < uids.Count; i++) {
                if (string.Equals(uids[i], uid, StringComparison.Ordinal)) {
                    return new Pop3UidLookupResult(true, i);
                }
            }

            return new Pop3UidLookupResult(true, null);
        } catch (NotSupportedException) {
            return new Pop3UidLookupResult(false, null);
        }
    }

    private static async Task<string?> TryGetMessageUidAsync(IPop3MailboxClient client, int index, CancellationToken cancellationToken) {
        try {
            return await client.GetMessageUidAsync(index, cancellationToken).ConfigureAwait(false);
        } catch {
            return null;
        }
    }

    private static long? TryGetMessageSize(IPop3MailboxClient client, int index, CancellationToken cancellationToken) {
        try {
            return client.GetMessageSize(index, cancellationToken);
        } catch {
            return null;
        }
    }

    private sealed record Pop3UidLookupResult(bool Supported, int? Index);
}
