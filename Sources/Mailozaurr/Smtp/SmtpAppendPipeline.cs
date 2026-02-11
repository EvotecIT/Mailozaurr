using System;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MimeKit;

namespace Mailozaurr;

/// <summary>
/// Provides append-to-sent execution primitives for SMTP send pipelines.
/// </summary>
public static class SmtpAppendPipeline {
    /// <summary>
    /// Appends a message to a sent folder and returns normalized append outcome.
    /// </summary>
    /// <param name="sentFolder">Target sent folder.</param>
    /// <param name="message">Message to append.</param>
    /// <param name="flags">Message flags used for appended copy.</param>
    /// <param name="appendAsync">Optional append callback override (for testing/customization).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Append execution result.</returns>
    public static async Task<SmtpAppendExecutionResult> TryAppendToSentAsync(
        IMailFolder sentFolder,
        MimeMessage message,
        MessageFlags flags = MessageFlags.Seen,
        Func<IMailFolder, MimeMessage, MessageFlags, CancellationToken, Task>? appendAsync = null,
        CancellationToken cancellationToken = default) {
        if (sentFolder is null) {
            throw new ArgumentNullException(nameof(sentFolder));
        }
        if (message is null) {
            throw new ArgumentNullException(nameof(message));
        }

        try {
            if (sentFolder.IsOpen && sentFolder.Access != FolderAccess.ReadWrite) {
                await sentFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
            }
            if (!sentFolder.IsOpen || sentFolder.Access != FolderAccess.ReadWrite) {
                await sentFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
            }

            if (appendAsync is null) {
                await sentFolder.AppendAsync(message, flags, cancellationToken).ConfigureAwait(false);
            } else {
                await appendAsync(sentFolder, message, flags, cancellationToken).ConfigureAwait(false);
            }
            return new SmtpAppendExecutionResult {
                Appended = true,
                Folder = sentFolder.FullName
            };
        } catch (Exception ex) {
            return new SmtpAppendExecutionResult {
                Appended = false,
                Folder = sentFolder.FullName,
                Error = ex.Message
            };
        }
    }
}
