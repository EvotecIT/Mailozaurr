using MailKit.Net.Imap;
using MailKit.Security;
using System;
using System.Threading;
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
    public static Func<int, CancellationToken, Task>? DelayAsync { get; set; }

    /// <summary>
    /// Connects and authenticates to an IMAP server with retry support.
    /// </summary>
    /// <param name="request">Connection request settings.</param>
    /// <param name="authenticateAsync">Delegate performing authentication.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Authenticated <see cref="ImapClient"/> instance.</returns>
    public static Task<ImapClient> ConnectAsync(
        ImapConnectionRequest request,
        Func<ImapClient, CancellationToken, Task> authenticateAsync,
        CancellationToken cancellationToken = default) {
        if (request is null) {
            throw new ArgumentNullException(nameof(request));
        }
        if (authenticateAsync is null) {
            throw new ArgumentNullException(nameof(authenticateAsync));
        }

        return ConnectAsync(
            request.Server,
            request.Port,
            request.Options,
            request.Timeout,
            request.SkipCertificateRevocation,
            request.SkipCertificateValidation,
            authenticateAsync,
            request.RetryCount,
            request.RetryDelayMilliseconds,
            request.RetryDelayBackoff,
            cancellationToken);
    }

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
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Authenticated <see cref="ImapClient"/> instance.</returns>
    public static Task<ImapClient> ConnectAsync(
        string server,
        int port,
        SecureSocketOptions options,
        int timeout,
        bool skipCertificateRevocation,
        bool skipCertificateValidation,
        Func<ImapClient, CancellationToken, Task> authenticateAsync,
        int retryCount,
        int retryDelayMilliseconds,
        double retryDelayBackoff,
        CancellationToken cancellationToken = default) =>
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
            DelayAsync,
            cancellationToken);

    /// <summary>
    /// Connects and authenticates to an IMAP server using protocol auth settings in one step.
    /// </summary>
    /// <param name="request">Connection request settings.</param>
    /// <param name="userName">IMAP user name.</param>
    /// <param name="secret">IMAP password or OAuth token.</param>
    /// <param name="mode">Authentication mode.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Authenticated <see cref="ImapClient"/> instance.</returns>
    public static Task<ImapClient> ConnectAuthenticatedAsync(
        ImapConnectionRequest request,
        string userName,
        string secret,
        ProtocolAuthMode mode = ProtocolAuthMode.Basic,
        CancellationToken cancellationToken = default) {
        if (request is null) {
            throw new ArgumentNullException(nameof(request));
        }

        return ConnectAsync(
            request,
            (client, ct) => ProtocolAuth.AuthenticateImapAsync(client, userName, secret, mode, ct),
            cancellationToken);
    }
}