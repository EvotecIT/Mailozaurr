using MailKit.Net.Pop3;
using MailKit.Security;
using System;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Provides helper methods for connecting to POP3 servers with retry logic.
/// </summary>
public static class Pop3Connector {
    /// <summary>
    /// Factory used to create <see cref="Pop3Client"/> instances.
    /// </summary>
    public static Func<Pop3Client> ClientFactory { get; set; } = () => new Pop3Client();

    /// <summary>
    /// Delegate used to delay between connection retries.
    /// </summary>
    public static Func<int, Task>? DelayAsync { get; set; }
    /// <summary>
    /// Connects and authenticates to a POP3 server with retry support.
    /// </summary>
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
    /// <returns>Authenticated <see cref="Pop3Client"/> instance.</returns>
    public static async Task<Pop3Client> ConnectAsync(
        string server,
        int port,
        SecureSocketOptions options,
        int timeout,
        bool skipCertificateRevocation,
        bool skipCertificateValidation,
        Func<Pop3Client, Task> authenticateAsync,
        int retryCount,
        int retryDelayMilliseconds,
        double retryDelayBackoff) {
        int attempts = 0;
        Exception? lastException = null;
        do {
            var client = ClientFactory();
            try {
                await client.ConnectAsync(server, port, options).ConfigureAwait(false);
                if (skipCertificateRevocation) {
                    client.CheckCertificateRevocation = false;
                }
                if (skipCertificateValidation) {
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                }
                if (client.Timeout != timeout) {
                    client.Timeout = timeout;
                }
                await authenticateAsync(client).ConfigureAwait(false);
                if (!client.IsAuthenticated) {
                    throw new InvalidOperationException("Authentication failed.");
                }
                return client;
            } catch (Exception ex) {
                lastException = ex;
                LoggingMessages.Logger.WriteWarning($"Connect-POP3 - {ex.Message}");
                try {
                    if (client.IsConnected) {
                        await client.DisconnectAsync(true).ConfigureAwait(false);
                    }
                } catch { }
                if ((!Helpers.IsTransient(ex)) || attempts >= retryCount) {
                    throw;
                }
                var delay = (int)Math.Round(retryDelayMilliseconds * Math.Pow(retryDelayBackoff, attempts));
                if (delay > 0) {
                    if (DelayAsync != null) {
                        await DelayAsync(delay).ConfigureAwait(false);
                    } else {
                        await Task.Delay(delay).ConfigureAwait(false);
                    }
                }
            }
            attempts++;
        } while (attempts <= retryCount);
        throw lastException!;
    }
}
