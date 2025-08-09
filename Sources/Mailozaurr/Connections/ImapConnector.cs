using MailKit.Net.Imap;
using MailKit.Security;
using System;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Provides helper methods for connecting to IMAP servers with retry logic.
/// </summary>
public static class ImapConnector {
    /// <summary>
    /// Factory used to create <see cref="ImapClient"/> instances.
    /// </summary>
    public static Func<ImapClient> ClientFactory { get; set; } = () => new ImapClient();

    /// <summary>
    /// Delegate used to introduce a delay between connection retries.
    /// </summary>
    public static Func<int, Task>? DelayAsync { get; set; }

    /// <summary>
    /// Connects and authenticates to an IMAP server with retry support.
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
    /// <returns>Authenticated <see cref="ImapClient"/> instance.</returns>
    public static Task<ImapClient> ConnectAsync(
        string server,
        int port,
        SecureSocketOptions options,
        int timeout,
        bool skipCertificateRevocation,
        bool skipCertificateValidation,
        Func<ImapClient, Task> authenticateAsync,
        int retryCount,
        int retryDelayMilliseconds,
        double retryDelayBackoff) =>
        ConnectionRetrier.ConnectAsync(
            ClientFactory,
            "IMAP",
            server,
            port,
            options,
            timeout,
            skipCertificateRevocation,
            skipCertificateValidation,
            authenticateAsync,
            retryCount,
            retryDelayMilliseconds,
            retryDelayBackoff,
            DelayAsync);
}
