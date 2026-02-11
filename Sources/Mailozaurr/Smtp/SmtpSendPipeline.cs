using System;
using System.Collections.Generic;
using MimeKit;

namespace Mailozaurr;

/// <summary>
/// Provides reusable SMTP send-pipeline primitives for threading/idempotency header shaping.
/// </summary>
public static class SmtpSendPipeline {
    /// <summary>
    /// Applies normalized threading/idempotency headers to a MIME message.
    /// </summary>
    /// <param name="message">Message instance to update.</param>
    /// <param name="inReplyToCandidate">Optional In-Reply-To message-id token.</param>
    /// <param name="referenceCandidates">Optional References message-id tokens.</param>
    /// <param name="messageId">Optional deterministic Message-Id token.</param>
    /// <param name="idempotencyHeaderName">Optional custom idempotency header name.</param>
    /// <param name="idempotencyKey">Optional idempotency header value.</param>
    public static void ApplyThreadingHeaders(
        MimeMessage message,
        string? inReplyToCandidate,
        IEnumerable<string?>? referenceCandidates,
        string? messageId = null,
        string? idempotencyHeaderName = null,
        string? idempotencyKey = null) {
        if (message is null) {
            throw new ArgumentNullException(nameof(message));
        }

        if (!string.IsNullOrWhiteSpace(idempotencyHeaderName) && !string.IsNullOrWhiteSpace(idempotencyKey)) {
            message.Headers.Replace(idempotencyHeaderName, idempotencyKey);
        }

        var normalizedMessageId = NormalizeMessageIdToken(messageId);
        if (!string.IsNullOrWhiteSpace(normalizedMessageId)) {
            message.MessageId = normalizedMessageId;
        }

        var inReplyTo = NormalizeMessageIdToken(inReplyToCandidate);
        if (!string.IsNullOrWhiteSpace(inReplyTo)) {
            message.InReplyTo = inReplyTo;
        }

        var refs = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (referenceCandidates is not null) {
            foreach (var candidate in referenceCandidates) {
                AddReference(candidate);
            }
        }
        AddReference(inReplyTo);

        if (refs.Count == 0) {
            return;
        }

        message.References.Clear();
        foreach (var reference in refs) {
            message.References.Add(reference);
        }

        void AddReference(string? candidate) {
            var token = NormalizeMessageIdToken(candidate);
            if (string.IsNullOrWhiteSpace(token)) {
                return;
            }
            if (seen.Add(token)) {
                refs.Add(token);
            }
        }
    }

    private static string? NormalizeMessageIdToken(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("<", StringComparison.Ordinal)) {
            normalized = normalized.Substring(1);
        }
        if (normalized.EndsWith(">", StringComparison.Ordinal)) {
            normalized = normalized.Substring(0, normalized.Length - 1);
        }
        normalized = normalized.Trim();

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
