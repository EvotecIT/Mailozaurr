using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Search;
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

    /// <summary>
    /// Probes a sent folder for an existing copy using idempotency header, then Message-Id fallback.
    /// </summary>
    /// <param name="sentFolder">Opened sent folder to probe.</param>
    /// <param name="idempotencyHeaderName">Header name used for idempotency tagging.</param>
    /// <param name="idempotencyKey">Idempotency value to search by.</param>
    /// <param name="idempotentMessageId">Optional deterministic Message-Id fallback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Duplicate probe result.</returns>
    public static async Task<SmtpDuplicateProbeResult> TryFindExistingSentCopyAsync(
        IMailFolder sentFolder,
        string idempotencyHeaderName,
        string idempotencyKey,
        string? idempotentMessageId,
        CancellationToken cancellationToken = default) {
        if (sentFolder is null) {
            throw new ArgumentNullException(nameof(sentFolder));
        }
        if (string.IsNullOrWhiteSpace(idempotencyHeaderName)) {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(idempotencyHeaderName));
        }
        if (string.IsNullOrWhiteSpace(idempotencyKey)) {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(idempotencyKey));
        }

        try {
            var uids = await sentFolder.SearchAsync(SearchQuery.HeaderContains(idempotencyHeaderName, idempotencyKey), cancellationToken).ConfigureAwait(false);
            if (uids.Count == 0) {
                var token = NormalizeMessageIdToken(idempotentMessageId);
                if (!string.IsNullOrWhiteSpace(token)) {
                    uids = await sentFolder.SearchAsync(SearchQuery.HeaderContains("Message-Id", token), cancellationToken).ConfigureAwait(false);
                }
            }

            if (uids.Count == 0) {
                return SmtpDuplicateProbeResult.None;
            }

            string? matchedMessageId = null;
            try {
                var message = await sentFolder.GetMessageAsync(uids[0], cancellationToken).ConfigureAwait(false);
                matchedMessageId = message?.MessageId;
            } catch {
                // best-effort
            }

            return new SmtpDuplicateProbeResult {
                IsMatch = true,
                Folder = sentFolder.FullName,
                MessageId = string.IsNullOrWhiteSpace(matchedMessageId) ? idempotentMessageId : matchedMessageId
            };
        } catch {
            return SmtpDuplicateProbeResult.None;
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
