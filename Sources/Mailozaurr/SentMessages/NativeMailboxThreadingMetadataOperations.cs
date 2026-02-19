#pragma warning disable CS1591
#pragma warning disable CS8600,CS8601,CS8602,CS8603,CS8604,CS8618,CS8625
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mailozaurr;
using MimeKit;

namespace Mailozaurr;

public static class NativeMailboxThreadingMetadataOperations {
    public sealed class NativeMailboxThreadingMetadataResult {
        public string? MessageId { get; init; }
        public string? ReplyTo { get; init; }
        public string? Cc { get; init; }
        public string? InReplyTo { get; init; }
        public List<string>? References { get; init; }
    }

    public static async Task<NativeMailboxThreadingMetadataResult> GetGraphThreadingMetadataAsync(
        GraphMailboxBrowser browser,
        string nativeId,
        int maxMimeBytes,
        CancellationToken cancellationToken) {
        if (browser is null) {
            throw new ArgumentNullException(nameof(browser));
        }
        if (string.IsNullOrWhiteSpace(nativeId)) {
            throw new ArgumentException("nativeId is required.", nameof(nativeId));
        }

        var message = await browser.GetMessageContentAsync(
            nativeId,
            maxMimeBytes: maxMimeBytes,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return NormalizeFromMessage(message.Message);
    }

    public static async Task<NativeMailboxThreadingMetadataResult> GetGmailThreadingMetadataAsync(
        GmailMailboxBrowser browser,
        string nativeId,
        CancellationToken cancellationToken) {
        if (browser is null) {
            throw new ArgumentNullException(nameof(browser));
        }
        if (string.IsNullOrWhiteSpace(nativeId)) {
            throw new ArgumentException("nativeId is required.", nameof(nativeId));
        }

        var message = await browser.GetMessageContentAsync(
            nativeId,
            maxMimeBytes: GmailMailboxBrowser.DefaultMaxMimeBytes,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return NormalizeFromMessage(message.Message);
    }

    public static NativeMailboxThreadingMetadataResult Normalize(
        string? messageId,
        string? replyTo,
        string? cc,
        string? inReplyTo,
        IReadOnlyCollection<string>? references) {
        var normalizedMessageId = NormalizeMessageId(messageId);
        var normalizedReplyTo = NormalizeOptional(replyTo);
        var normalizedCc = NormalizeOptional(cc);
        var normalizedInReplyTo = NormalizeMessageId(inReplyTo);
        var normalizedReferences = NormalizeReferences(references);

        return new NativeMailboxThreadingMetadataResult {
            MessageId = normalizedMessageId,
            ReplyTo = normalizedReplyTo,
            Cc = normalizedCc,
            InReplyTo = normalizedInReplyTo,
            References = normalizedReferences
        };
    }

    private static NativeMailboxThreadingMetadataResult NormalizeFromMessage(MimeMessage message) {
        if (message is null) {
            throw new ArgumentNullException(nameof(message));
        }

        var messageId = NormalizeMessageId(message.MessageId ?? ExtractFirstToken(message.Headers[HeaderId.MessageId]));
        var replyTo = JoinAddresses(message.ReplyTo);
        var cc = JoinAddresses(message.Cc);
        var inReplyTo = NormalizeMessageId(ExtractFirstToken(message.Headers[HeaderId.InReplyTo]));
        var references = NormalizeReferences(ExtractTokens(message.Headers[HeaderId.References]));

        return new NativeMailboxThreadingMetadataResult {
            MessageId = messageId,
            ReplyTo = replyTo,
            Cc = cc,
            InReplyTo = inReplyTo,
            References = references
        };
    }

    private static string? JoinAddresses(InternetAddressList addresses) {
        if (addresses is null || addresses.Count == 0) {
            return null;
        }

        var joined = string.Join(", ", addresses.Select(a => a.ToString()));
        return NormalizeOptional(joined);
    }

    private static List<string>? NormalizeReferences(IEnumerable<string>? values) {
        if (values is null) {
            return null;
        }

        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values) {
            var token = NormalizeMessageId(value);
            if (string.IsNullOrWhiteSpace(token)) {
                continue;
            }
            if (seen.Add(token)) {
                normalized.Add(token);
            }
        }

        return normalized.Count == 0 ? null : normalized;
    }

    private static IEnumerable<string> ExtractTokens(string? raw) {
        if (string.IsNullOrWhiteSpace(raw)) {
            return Array.Empty<string>();
        }

        var parts = raw.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var output = new List<string>(parts.Length);
        foreach (var part in parts) {
            var token = part.Trim();
            if (token.Length > 0) {
                output.Add(token);
            }
        }
        return output;
    }

    private static string? ExtractFirstToken(string? raw) {
        if (string.IsNullOrWhiteSpace(raw)) {
            return null;
        }

        var parts = raw.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts) {
            var token = part.Trim();
            if (token.Length > 0) {
                return token;
            }
        }
        return null;
    }

    private static string? NormalizeMessageId(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0) {
            return null;
        }

        var start = 0;
        var end = trimmed.Length;
        if (trimmed[start] == '<') {
            start++;
        }
        if (end > start && trimmed[end - 1] == '>') {
            end--;
        }

        var normalized = trimmed.Substring(start, end - start).Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    private static string? NormalizeOptional(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
