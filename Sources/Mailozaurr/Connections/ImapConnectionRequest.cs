using MailKit.Security;
using System;

namespace Mailozaurr;

/// <summary>
/// Represents connection settings used by <see cref="ImapConnector"/>.
/// </summary>
public sealed class ImapConnectionRequest {
    /// <summary>
    /// Initializes a new instance of the <see cref="ImapConnectionRequest"/> class.
    /// </summary>
    /// <param name="server">IMAP server hostname.</param>
    /// <param name="port">IMAP server port.</param>
    /// <param name="options">Secure socket options.</param>
    /// <param name="timeout">Connection timeout in milliseconds.</param>
    /// <param name="skipCertificateRevocation">Whether to skip certificate revocation checks.</param>
    /// <param name="skipCertificateValidation">Whether to skip certificate validation checks.</param>
    /// <param name="retryCount">Retry count for transient failures.</param>
    /// <param name="retryDelayMilliseconds">Initial retry delay in milliseconds.</param>
    /// <param name="retryDelayBackoff">Retry delay backoff multiplier.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="server"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a numeric argument is outside supported range.</exception>
    public ImapConnectionRequest(
        string server,
        int port,
        SecureSocketOptions options = SecureSocketOptions.Auto,
        int timeout = 30000,
        bool skipCertificateRevocation = false,
        bool skipCertificateValidation = false,
        int retryCount = 3,
        int retryDelayMilliseconds = 500,
        double retryDelayBackoff = 2.0) {
        if (string.IsNullOrWhiteSpace(server)) {
            throw new ArgumentException("Server cannot be null or whitespace.", nameof(server));
        }
        if (port <= 0 || port > 65535) {
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
        }
        if (timeout < 0) {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be >= 0.");
        }
        if (retryCount < 0) {
            throw new ArgumentOutOfRangeException(nameof(retryCount), "Retry count must be >= 0.");
        }
        if (retryDelayMilliseconds < 0) {
            throw new ArgumentOutOfRangeException(nameof(retryDelayMilliseconds), "Retry delay must be >= 0.");
        }
        if (retryDelayBackoff <= 0) {
            throw new ArgumentOutOfRangeException(nameof(retryDelayBackoff), "Retry backoff must be > 0.");
        }

        Server = server;
        Port = port;
        Options = options;
        Timeout = timeout;
        SkipCertificateRevocation = skipCertificateRevocation;
        SkipCertificateValidation = skipCertificateValidation;
        RetryCount = retryCount;
        RetryDelayMilliseconds = retryDelayMilliseconds;
        RetryDelayBackoff = retryDelayBackoff;
    }

    /// <summary>
    /// Gets the IMAP server hostname.
    /// </summary>
    public string Server { get; }

    /// <summary>
    /// Gets the IMAP server port.
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Gets secure socket options.
    /// </summary>
    public SecureSocketOptions Options { get; }

    /// <summary>
    /// Gets connection timeout in milliseconds.
    /// </summary>
    public int Timeout { get; }

    /// <summary>
    /// Gets a value indicating whether certificate revocation checks are skipped.
    /// </summary>
    public bool SkipCertificateRevocation { get; }

    /// <summary>
    /// Gets a value indicating whether certificate validation checks are skipped.
    /// </summary>
    public bool SkipCertificateValidation { get; }

    /// <summary>
    /// Gets retry count for transient failures.
    /// </summary>
    public int RetryCount { get; }

    /// <summary>
    /// Gets initial retry delay in milliseconds.
    /// </summary>
    public int RetryDelayMilliseconds { get; }

    /// <summary>
    /// Gets retry delay backoff multiplier.
    /// </summary>
    public double RetryDelayBackoff { get; }
}
