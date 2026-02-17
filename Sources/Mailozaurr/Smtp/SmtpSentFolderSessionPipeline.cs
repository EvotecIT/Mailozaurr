using System;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MimeKit;

namespace Mailozaurr;

/// <summary>
/// Provides IMAP sent-folder session execution primitives for SMTP pipeline consumers.
/// </summary>
public static class SmtpSentFolderSessionPipeline {
    /// <summary>
    /// Connects IMAP session, resolves sent folder, probes for duplicate sent copy, and disconnects.
    /// </summary>
    /// <param name="connectAsync">Callback used to create and connect IMAP client.</param>
    /// <param name="resolveSentFolderAsync">Callback used to resolve sent folder from connected client.</param>
    /// <param name="idempotencyHeaderName">Header name used for idempotency lookup.</param>
    /// <param name="idempotencyKey">Idempotency key value.</param>
    /// <param name="idempotentMessageId">Optional deterministic message-id fallback.</param>
    /// <param name="disconnectAsync">Optional disconnect callback override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Duplicate-probe execution result.</returns>
    public static async Task<SmtpDuplicateProbeResult> TryFindExistingSentCopyAsync(
        Func<CancellationToken, Task<ImapClient>> connectAsync,
        Func<ImapClient, CancellationToken, Task<IMailFolder>> resolveSentFolderAsync,
        string idempotencyHeaderName,
        string idempotencyKey,
        string? idempotentMessageId,
        Func<ImapClient, CancellationToken, Task>? disconnectAsync = null,
        CancellationToken cancellationToken = default) {
        if (connectAsync is null) {
            throw new ArgumentNullException(nameof(connectAsync));
        }
        if (resolveSentFolderAsync is null) {
            throw new ArgumentNullException(nameof(resolveSentFolderAsync));
        }
        if (string.IsNullOrWhiteSpace(idempotencyHeaderName)) {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(idempotencyHeaderName));
        }
        if (string.IsNullOrWhiteSpace(idempotencyKey)) {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(idempotencyKey));
        }

        var client = await connectAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Connected IMAP client is required.");

        try {
            var sentFolder = await resolveSentFolderAsync(client, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Resolved sent folder is required.");

            return await SmtpSendPipeline.TryFindExistingSentCopyAsync(
                sentFolder,
                idempotencyHeaderName,
                idempotencyKey,
                idempotentMessageId,
                cancellationToken).ConfigureAwait(false);
        } finally {
            await DisconnectAndDisposeAsync(client, disconnectAsync, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Connects IMAP session, resolves sent folder, appends message, and disconnects.
    /// </summary>
    /// <param name="connectAsync">Callback used to create and connect IMAP client.</param>
    /// <param name="resolveSentFolderAsync">Callback used to resolve sent folder from connected client.</param>
    /// <param name="message">Message to append.</param>
    /// <param name="flags">Message flags used for append operation.</param>
    /// <param name="appendAsync">Optional append callback override for folder append operation.</param>
    /// <param name="disconnectAsync">Optional disconnect callback override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Append execution result.</returns>
    public static async Task<SmtpAppendExecutionResult> TryAppendToSentAsync(
        Func<CancellationToken, Task<ImapClient>> connectAsync,
        Func<ImapClient, CancellationToken, Task<IMailFolder>> resolveSentFolderAsync,
        MimeMessage message,
        MessageFlags flags = MessageFlags.Seen,
        Func<IMailFolder, MimeMessage, MessageFlags, CancellationToken, Task>? appendAsync = null,
        Func<ImapClient, CancellationToken, Task>? disconnectAsync = null,
        CancellationToken cancellationToken = default) {
        if (connectAsync is null) {
            throw new ArgumentNullException(nameof(connectAsync));
        }
        if (resolveSentFolderAsync is null) {
            throw new ArgumentNullException(nameof(resolveSentFolderAsync));
        }
        if (message is null) {
            throw new ArgumentNullException(nameof(message));
        }

        var client = await connectAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Connected IMAP client is required.");

        try {
            var sentFolder = await resolveSentFolderAsync(client, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Resolved sent folder is required.");

            return await SmtpAppendPipeline.TryAppendToSentAsync(
                sentFolder,
                message,
                flags,
                appendAsync,
                cancellationToken).ConfigureAwait(false);
        } finally {
            await DisconnectAndDisposeAsync(client, disconnectAsync, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Connects IMAP session, reads threading metadata for a folder + UID, and disconnects.
    /// </summary>
    /// <param name="connectAsync">Callback used to create and connect IMAP client.</param>
    /// <param name="folder">Folder name containing the message.</param>
    /// <param name="uid">Message UID in the folder.</param>
    /// <param name="getMetadataAsync">Optional metadata callback override.</param>
    /// <param name="disconnectAsync">Optional disconnect callback override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Threading metadata when found; otherwise null.</returns>
    public static async Task<ImapSentMessageOperations.ImapThreadingMetadataResult?> TryGetThreadingMetadataAsync(
        Func<CancellationToken, Task<ImapClient>> connectAsync,
        string folder,
        uint uid,
        Func<ImapClient, string, uint, CancellationToken, Task<ImapSentMessageOperations.ImapThreadingMetadataResult?>>? getMetadataAsync = null,
        Func<ImapClient, CancellationToken, Task>? disconnectAsync = null,
        CancellationToken cancellationToken = default) {
        if (connectAsync is null) {
            throw new ArgumentNullException(nameof(connectAsync));
        }
        if (string.IsNullOrWhiteSpace(folder)) {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(folder));
        }
        if (uid == 0) {
            throw new ArgumentOutOfRangeException(nameof(uid), "uid must be greater than zero.");
        }

        var client = await connectAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Connected IMAP client is required.");

        try {
            if (getMetadataAsync is null) {
                return await ImapSentMessageOperations.GetThreadingMetadataAsync(
                    client,
                    folder,
                    uid,
                    cancellationToken).ConfigureAwait(false);
            }

            return await getMetadataAsync(client, folder, uid, cancellationToken).ConfigureAwait(false);
        } finally {
            await DisconnectAndDisposeAsync(client, disconnectAsync, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task DisconnectAndDisposeAsync(
        ImapClient client,
        Func<ImapClient, CancellationToken, Task>? disconnectAsync,
        CancellationToken cancellationToken) {
        try {
            if (disconnectAsync is not null) {
                await disconnectAsync(client, cancellationToken).ConfigureAwait(false);
            } else if (client.IsConnected) {
                client.Disconnect(true);
            }
        } catch {
            // best-effort cleanup
        } finally {
            client.Dispose();
        }
    }
}
