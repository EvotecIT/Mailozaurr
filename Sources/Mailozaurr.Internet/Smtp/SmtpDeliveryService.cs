using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>Composes messages for SMTP delivery or an offline pickup directory.</summary>
public static class SmtpDeliveryService {
    /// <summary>Connects, optionally authenticates, composes and sends a message, then releases the supplied session.</summary>
    /// <param name="smtp">The configured message and session whose lifetime this operation owns.</param>
    /// <param name="request">Connection and authentication settings.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when delivery succeeds.</returns>
    /// <exception cref="InvalidOperationException">Delivery failed. Details are deliberately omitted.</exception>
    public static async Task SendAsync(Smtp smtp, SmtpSessionRequest request, CancellationToken cancellationToken = default) {
        if (smtp is null) {
            throw new ArgumentNullException(nameof(smtp));
        }

        try {
            using var diagnostics = new SmtpDiagnosticScope(smtp);
            if (request is null) {
                throw new ArgumentNullException(nameof(request));
            }
            cancellationToken.ThrowIfCancellationRequested();
            var connection = await SmtpSessionService.ConnectAndAuthenticateAsync(smtp, request, cancellationToken).ConfigureAwait(false);
            if (!connection.IsSuccess) {
                throw new InvalidOperationException("SMTP delivery failed.");
            }
            var result = await smtp.CreateAndSendSafelyAsync(cancellationToken).ConfigureAwait(false);
            // Acceptance is irreversible; late receipt cancellation must not invite a duplicate send.
            if (!result.DeliveryAccepted) cancellationToken.ThrowIfCancellationRequested();
            if (!result.Status) {
                throw new InvalidOperationException("SMTP delivery failed.");
            }
        } catch (OperationCanceledException exception) {
            throw new OperationCanceledException("SMTP operation canceled.", exception.CancellationToken);
        } catch {
            throw new InvalidOperationException("SMTP delivery failed.");
        } finally {
            SmtpSessionService.DisposeQuietly(smtp);
        }
    }

    /// <summary>Composes a message and writes a unique .eml file without connecting, authenticating or sending.</summary>
    /// <param name="smtp">The configured message and session whose lifetime this operation owns.</param>
    /// <param name="directory">The pickup directory, created if necessary.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The path of the completed .eml file.</returns>
    /// <remarks>Writes a unique .tmp file beside the destination, then renames it to .eml only after a complete flush and cancellation check.</remarks>
    /// <exception cref="InvalidOperationException">Pickup failed. Partial output is removed when possible and details are deliberately omitted.</exception>
    public static async Task<string> WritePickupAsync(Smtp smtp, string directory, CancellationToken cancellationToken = default) {
        if (smtp is null) {
            throw new ArgumentNullException(nameof(smtp));
        }

        string? createdPath = null;
        try {
            using var diagnostics = new SmtpDiagnosticScope(smtp);
            cancellationToken.ThrowIfCancellationRequested();
            await smtp.CreateMessageAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(directory);
            string stagingPath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            string path = Path.ChangeExtension(stagingPath, ".eml");
            using (var stream = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true)) {
                createdPath = stagingPath;
                await smtp.Message.WriteToAsync(stream, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(stagingPath, path);
            return path;
        } catch (OperationCanceledException exception) {
            DeletePartialQuietly(createdPath);
            throw new OperationCanceledException("SMTP operation canceled.", exception.CancellationToken);
        } catch {
            DeletePartialQuietly(createdPath);
            throw new InvalidOperationException("SMTP pickup failed.");
        } finally {
            SmtpSessionService.DisposeQuietly(smtp);
        }
    }

    /// <summary>Attempts due queued SMTP messages using the configured session, suppresses sensitive diagnostics and releases the session.</summary>
    /// <param name="smtp">The configured session and pending repository whose session lifetime this operation owns.</param>
    /// <param name="cancellationToken">Token used to cancel processing.</param>
    /// <returns>A task that completes when processing finishes. Failed deliveries remain queued according to the existing retry policy.</returns>
    /// <exception cref="InvalidOperationException">Queue processing failed. Details are deliberately omitted.</exception>
    public static async Task ProcessPendingMessagesAsync(Smtp smtp, CancellationToken cancellationToken = default) {
        if (smtp is null) {
            throw new ArgumentNullException(nameof(smtp));
        }
        try {
            using var diagnostics = new SmtpDiagnosticScope(smtp);
            cancellationToken.ThrowIfCancellationRequested();
            await smtp.ProcessPendingMessagesAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        } catch (OperationCanceledException exception) {
            throw new OperationCanceledException("SMTP operation canceled.", exception.CancellationToken);
        } catch {
            throw new InvalidOperationException("SMTP pending-message processing failed.");
        } finally {
            SmtpSessionService.DisposeQuietly(smtp);
        }
    }

    private static void DeletePartialQuietly(string? path) {
        if (path is null) {
            return;
        }
        try {
            File.Delete(path);
        } catch {
            // Preserve the original failure without exposing file-system details.
        }
    }
}
