using MailKit;
using MailKit.Search;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Provides reusable SMTP send-pipeline primitives for threading/idempotency header shaping.
/// </summary>
public static class SmtpSendPipeline {
    /// <summary>
    /// Creates a normalized send execution result contract for pipeline consumers.
    /// </summary>
    /// <param name="sendRequested">Whether send was requested by caller.</param>
    /// <param name="sendSucceeded">Whether send execution succeeded.</param>
    /// <param name="messageId">Optional emitted message-id.</param>
    /// <param name="sendError">Optional send error.</param>
    /// <param name="appendedToSent">Whether append-to-sent succeeded.</param>
    /// <param name="appendedSentFolder">Optional appended sent folder name.</param>
    /// <param name="appendError">Optional append error.</param>
    /// <returns>Normalized execution result.</returns>
    public static SmtpSendExecutionResult BuildExecutionResult(
        bool sendRequested,
        bool sendSucceeded,
        string? messageId,
        string? sendError,
        bool appendedToSent,
        string? appendedSentFolder,
        string? appendError) {
        return new SmtpSendExecutionResult {
            Ok = sendSucceeded,
            Sent = sendRequested && sendSucceeded,
            MessageId = messageId,
            Error = sendError,
            AppendedToSent = appendedToSent,
            AppendedSentFolder = appendedSentFolder,
            AppendError = appendError
        };
    }

    /// <summary>
    /// Builds send execution result and optionally performs append-to-sent orchestration.
    /// </summary>
    /// <param name="sendRequested">Whether send was requested by caller.</param>
    /// <param name="sendSucceeded">Whether send execution succeeded.</param>
    /// <param name="messageId">Optional emitted message-id.</param>
    /// <param name="sendError">Optional send error.</param>
    /// <param name="appendToSentRequested">Whether append-to-sent was requested.</param>
    /// <param name="appendAsync">Optional append callback executed only on successful real send + append request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Normalized execution result with append metadata.</returns>
    public static async Task<SmtpSendExecutionResult> BuildExecutionResultWithOptionalSentAppendAsync(
        bool sendRequested,
        bool sendSucceeded,
        string? messageId,
        string? sendError,
        bool appendToSentRequested,
        Func<CancellationToken, Task<SmtpAppendExecutionResult>>? appendAsync = null,
        CancellationToken cancellationToken = default) {
        var appendedToSent = false;
        string? appendedSentFolder = null;
        string? appendError = null;

        if (sendRequested && sendSucceeded && appendToSentRequested) {
            if (appendAsync is null) {
                appendError = "append callback is not configured";
            } else {
                try {
                    var appendResult = await appendAsync(cancellationToken).ConfigureAwait(false) ?? SmtpAppendExecutionResult.None;
                    appendedToSent = appendResult.Appended;
                    appendedSentFolder = appendResult.Folder;
                    appendError = appendResult.Error;
                } catch (Exception ex) {
                    appendError = ex.Message;
                }
            }
        }

        return BuildExecutionResult(
            sendRequested,
            sendSucceeded,
            messageId,
            sendError,
            appendedToSent,
            appendedSentFolder,
            appendError);
    }

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

        if (!string.IsNullOrWhiteSpace(idempotencyHeaderName) &&
            !string.IsNullOrWhiteSpace(idempotencyKey)) {
            var headerName = idempotencyHeaderName!.Trim();
            var headerValue = idempotencyKey!.Trim();
            message.Headers.Replace(headerName, headerValue);
        }

        var normalizedMessageId = NormalizeMessageIdToken(messageId);
        if (normalizedMessageId is { Length: > 0 } ensuredMessageId) {
            message.MessageId = ensuredMessageId;
        }

        var inReplyTo = NormalizeMessageIdToken(inReplyToCandidate);
        if (inReplyTo is { Length: > 0 } ensuredInReplyTo) {
            message.InReplyTo = ensuredInReplyTo;
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
            if (token is null || token.Length == 0) {
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
            var uids = await sentFolder.SearchAsync(SearchQuery.HeaderContains(idempotencyHeaderName!, idempotencyKey!), cancellationToken).ConfigureAwait(false);
            if (uids.Count == 0) {
                var token = NormalizeMessageIdToken(idempotentMessageId);
                if (!string.IsNullOrWhiteSpace(token)) {
                    uids = await sentFolder.SearchAsync(SearchQuery.HeaderContains("Message-Id", token!), cancellationToken).ConfigureAwait(false);
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
        if (value is null) {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length == 0) {
            return null;
        }
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