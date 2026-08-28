using MailKit;
using MailKit.Security;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Provides retry logic for MailKit connections.
/// </summary>
internal static class ConnectionRetrier {
    /// <summary>
    /// Connects and authenticates to a mail server with retry support.
    /// </summary>
    /// <typeparam name="TClient">Type of mail client.</typeparam>
    /// <param name="clientFactory">Factory creating client instances.</param>
    /// <param name="protocolName">Protocol name for logging.</param>
    /// <param name="server">Server hostname.</param>
    /// <param name="port">Server port.</param>
    /// <param name="options">Secure socket options.</param>
    /// <param name="timeout">Connection timeout.</param>
    /// <param name="skipCertificateRevocation">Skip certificate revocation check.</param>
    /// <param name="skipCertificateValidation">Skip certificate validation.</param>
    /// <param name="authenticateAsync">Delegate performing authentication.</param>
    /// <param name="retryCount">Number of retry attempts.</param>
    /// <param name="retryDelayMilliseconds">Initial delay between retries.</param>
    /// <param name="retryDelayBackoff">Multiplier for delay backoff.</param>
    /// <param name="delayAsync">Delegate used to introduce delay between retries.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Authenticated client instance.</returns>
    internal static async Task<TClient> ConnectAsync<TClient>(
        Func<TClient> clientFactory,
        string protocolName,
        string server,
        int port,
        SecureSocketOptions options,
        int timeout,
        bool skipCertificateRevocation,
        bool skipCertificateValidation,
        Func<TClient, CancellationToken, Task> authenticateAsync,
        int retryCount,
        int retryDelayMilliseconds,
        double retryDelayBackoff,
        Func<int, CancellationToken, Task>? delayAsync,
        CancellationToken cancellationToken = default)
        where TClient : MailService {
        int attempts = 0;
        while (true) {
            var client = clientFactory();
            try {
                // These options affect the TLS handshake and must be configured before ConnectAsync.
                if (skipCertificateRevocation) {
                    client.CheckCertificateRevocation = false;
                }
                if (skipCertificateValidation) {
                    client.ServerCertificateValidationCallback = static (_, _, _, _) => true;
                }
                if (timeout > 0 && client.Timeout != timeout) {
                    client.Timeout = timeout;
                }

                await client.ConnectAsync(server, port, options, cancellationToken).ConfigureAwait(false);
                await authenticateAsync(client, cancellationToken).ConfigureAwait(false);
                if (!client.IsAuthenticated) {
                    throw new InvalidOperationException("Authentication failed.");
                }
                return client;
            } catch (Exception ex) {
                LoggingMessages.Logger.WriteWarning($"Connect-{protocolName} - {ex.Message}");
                try {
                    if (client.IsConnected) {
                        await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
                    }
                } catch (Exception ex2) {
                    LoggingMessages.Logger.WriteWarning($"Connect-{protocolName} - {ex2.Message}");
                } finally {
                    client.Dispose();
                }
                if ((!Helpers.IsTransient(ex)) || attempts >= retryCount) {
                    throw;
                }
                var delay = RetryDelayCalculator.Calculate(
                    retryDelayMilliseconds,
                    retryDelayBackoff,
                    attempts,
                    0,
                    0);
                if (delay > TimeSpan.Zero) {
                    if (delayAsync != null) {
                        await delayAsync((int)delay.TotalMilliseconds, cancellationToken).ConfigureAwait(false);
                    } else {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            attempts++;
        }
    }
}
