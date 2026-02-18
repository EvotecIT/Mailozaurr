using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Shared normalization/retrieval helpers for native-provider threading metadata.
/// </summary>
public static class NativeMailboxThreadingMetadataOperations {
    /// <summary>
    /// Provider-agnostic threading metadata shape.
    /// </summary>
    public sealed class NativeMailboxThreadingMetadataResult {
        /// <summary>Normalized RFC822 Message-Id.</summary>
        public string? MessageId { get; set; }

        /// <summary>Reply-To header value.</summary>
        public string? ReplyTo { get; set; }

        /// <summary>Cc header value.</summary>
        public string? Cc { get; set; }

        /// <summary>Normalized RFC822 In-Reply-To value.</summary>
        public string? InReplyTo { get; set; }

        /// <summary>Normalized RFC822 References tokens.</summary>
        public List<string>? References { get; set; }
    }

    /// <summary>
    /// Reads and normalizes Graph threading metadata for a message.
    /// </summary>
    public static async Task<NativeMailboxThreadingMetadataResult> GetGraphThreadingMetadataAsync(
        GraphMailboxBrowser browser,
        string nativeId,
        int maxMimeBytes = GraphMailboxBrowser.DefaultThreadingMetadataMaxMimeBytes,
        CancellationToken cancellationToken = default) {
        if (browser == null) {
            throw new ArgumentNullException(nameof(browser));
        }
        if (string.IsNullOrWhiteSpace(nativeId)) {
            throw new ArgumentException("nativeId is required.", nameof(nativeId));
        }

        var metadata = await browser.GetThreadingMetadataAsync(
            nativeId.Trim(),
            maxMimeBytes: maxMimeBytes,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return Normalize(metadata);
    }

    /// <summary>
    /// Reads and normalizes Gmail threading metadata for a message.
    /// </summary>
    public static async Task<NativeMailboxThreadingMetadataResult> GetGmailThreadingMetadataAsync(
        GmailMailboxBrowser browser,
        string nativeId,
        CancellationToken cancellationToken = default) {
        if (browser == null) {
            throw new ArgumentNullException(nameof(browser));
        }
        if (string.IsNullOrWhiteSpace(nativeId)) {
            throw new ArgumentException("nativeId is required.", nameof(nativeId));
        }

        var metadata = await browser.GetThreadingMetadataAsync(
            nativeId.Trim(),
            cancellationToken).ConfigureAwait(false);
        return Normalize(metadata);
    }

    /// <summary>
    /// Normalizes provider-agnostic threading metadata values.
    /// </summary>
    public static NativeMailboxThreadingMetadataResult Normalize(
        string? messageId,
        string? replyTo,
        string? cc,
        string? inReplyTo,
        IEnumerable<string>? references) {
        var normalizedReferences = NormalizeMessageIdList(references);
        return new NativeMailboxThreadingMetadataResult {
            MessageId = ImapSentMessageOperations.NormalizeMessageIdToken(messageId),
            ReplyTo = NormalizeOptional(replyTo),
            Cc = NormalizeOptional(cc),
            InReplyTo = ImapSentMessageOperations.NormalizeMessageIdToken(inReplyTo),
            References = normalizedReferences
        };
    }

    /// <summary>
    /// Normalizes Graph threading metadata values.
    /// </summary>
    public static NativeMailboxThreadingMetadataResult Normalize(
        GraphMailboxBrowser.GraphMailboxThreadingMetadataResult metadata) {
        if (metadata == null) {
            throw new ArgumentNullException(nameof(metadata));
        }

        return Normalize(
            metadata.MessageId,
            metadata.ReplyTo,
            metadata.Cc,
            metadata.InReplyTo,
            metadata.References);
    }

    /// <summary>
    /// Normalizes Gmail threading metadata values.
    /// </summary>
    public static NativeMailboxThreadingMetadataResult Normalize(
        GmailMailboxBrowser.GmailMailboxThreadingMetadataResult metadata) {
        if (metadata == null) {
            throw new ArgumentNullException(nameof(metadata));
        }

        return Normalize(
            metadata.MessageId,
            metadata.ReplyTo,
            metadata.Cc,
            metadata.InReplyTo,
            metadata.References);
    }

    private static List<string>? NormalizeMessageIdList(IEnumerable<string>? references) {
        if (references == null) {
            return null;
        }

        var output = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in references) {
            var normalized = ImapSentMessageOperations.NormalizeMessageIdToken(reference);
            if (normalized == null || !seen.Add(normalized)) {
                continue;
            }
            output.Add(normalized);
        }

        return output.Count == 0 ? null : output;
    }

    private static string? NormalizeOptional(string? value) {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
