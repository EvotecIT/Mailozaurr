using MailKit.Net.Pop3;
using MailKit.Security;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Provides helper methods for connecting to POP3 servers with retry logic.
/// </summary>
public static class Pop3Connector {
    private static Pop3Client CreateDefaultClient() => new Pop3Client();

    /// <summary>
    /// Factory used to create <see cref="Pop3Client"/> instances.
    /// </summary>
    public static Func<Pop3Client> ClientFactory { get; set; } = CreateDefaultClient;

    /// <summary>
    /// Restores the default POP3 client factory.
    /// </summary>
    public static void ResetClientFactory() => ClientFactory = CreateDefaultClient;

    /// <summary>
    /// Delegate used to delay between connection retries.
    /// </summary>
    public static Func<int, CancellationToken, Task>? DelayAsync { get; set; }
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
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Authenticated <see cref="Pop3Client"/> instance.</returns>
    public static Task<Pop3Client> ConnectAsync(
        string server,
        int port,
        SecureSocketOptions options,
        int timeout,
        bool skipCertificateRevocation,
        bool skipCertificateValidation,
        Func<Pop3Client, CancellationToken, Task> authenticateAsync,
        int retryCount,
        int retryDelayMilliseconds,
        double retryDelayBackoff,
        CancellationToken cancellationToken = default) =>
        ConnectionRetrier.ConnectAsync(
            ClientFactory,
            "POP3",
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
            DelayAsync,
            cancellationToken);
}