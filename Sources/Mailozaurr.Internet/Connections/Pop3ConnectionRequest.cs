using MailKit.Security;

namespace Mailozaurr;

/// <summary>
/// Describes the transport settings used to establish a POP3 connection.
/// </summary>
public sealed class Pop3ConnectionRequest {
    /// <summary>Creates a POP3 connection request.</summary>
    public Pop3ConnectionRequest(
        string server,
        int port = 995,
        SecureSocketOptions options = SecureSocketOptions.Auto,
        int timeout = 30000,
        bool skipCertificateRevocation = false,
        bool skipCertificateValidation = false,
        int retryCount = 3,
        int retryDelayMilliseconds = 500,
        double retryDelayBackoff = 2.0) {
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

    /// <summary>POP3 server hostname.</summary>
    public string Server { get; }

    /// <summary>POP3 server port.</summary>
    public int Port { get; }

    /// <summary>TLS mode used for the connection.</summary>
    public SecureSocketOptions Options { get; }

    /// <summary>Connection timeout in milliseconds.</summary>
    public int Timeout { get; }

    /// <summary>Whether certificate revocation checks are skipped.</summary>
    public bool SkipCertificateRevocation { get; }

    /// <summary>Whether certificate validation is skipped.</summary>
    public bool SkipCertificateValidation { get; }

    /// <summary>Number of connection attempts.</summary>
    public int RetryCount { get; }

    /// <summary>Initial retry delay in milliseconds.</summary>
    public int RetryDelayMilliseconds { get; }

    /// <summary>Retry-delay backoff multiplier.</summary>
    public double RetryDelayBackoff { get; }
}
