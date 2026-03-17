using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Security;

namespace Mailozaurr;

/// <summary>
/// Represents the result of an SMTP connect/auth flow.
/// </summary>
public sealed class SmtpConnectResult {
    /// <summary>
    /// Gets a value indicating whether the connection and authentication succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the secure socket options used during the connection.
    /// </summary>
    public SecureSocketOptions SecureSocketOptions { get; }

    /// <summary>
    /// Gets the error code when the flow failed.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Gets the error message when the flow failed.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Gets a value indicating whether the failure is transient.
    /// </summary>
    public bool IsTransient { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpConnectResult"/> class.
    /// </summary>
    public SmtpConnectResult(bool isSuccess, SecureSocketOptions secureSocketOptions, string? errorCode, string? error, bool isTransient) {
        IsSuccess = isSuccess;
        SecureSocketOptions = secureSocketOptions;
        ErrorCode = errorCode;
        Error = error;
        IsTransient = isTransient;
    }
}

/// <summary>
/// Describes the parameters required for an SMTP session.
/// </summary>
public sealed class SmtpSessionRequest {
    /// <summary>SMTP server address.</summary>
    public string Server { get; init; } = string.Empty;
    /// <summary>SMTP port.</summary>
    public int Port { get; init; } = 587;
    /// <summary>Secure socket options.</summary>
    public SecureSocketOptions SecureSocketOptions { get; init; } = SecureSocketOptions.Auto;
    /// <summary>Force SSL flag.</summary>
    public bool UseSsl { get; init; }
    /// <summary>Connection timeout in milliseconds.</summary>
    public int TimeoutMs { get; init; } = 30_000;
    /// <summary>Retries for connection/auth flows.</summary>
    public int RetryCount { get; init; }
    /// <summary>Base delay for retries.</summary>
    public int RetryDelayMilliseconds { get; init; }
    /// <summary>Backoff multiplier.</summary>
    public double RetryDelayBackoff { get; init; } = 1.0;
    /// <summary>Skip certificate validation.</summary>
    public bool SkipCertificateValidation { get; init; }
    /// <summary>Skip certificate revocation checks.</summary>
    public bool SkipCertificateRevocation { get; init; }
    /// <summary>Implements DryRun (no network).</summary>
    public bool DryRun { get; init; }
    /// <summary>Auth username.</summary>
    public string UserName { get; init; } = string.Empty;
    /// <summary>Auth password.</summary>
    public string Password { get; init; } = string.Empty;
    /// <summary>Protocol auth mode.</summary>
    public ProtocolAuthMode AuthMode { get; init; } = ProtocolAuthMode.Basic;
    /// <summary>Optional connect delegate used for testing.</summary>
    public Func<Smtp, Task<SmtpResult>>? ConnectAsync { get; init; }
    /// <summary>Optional authenticate delegate used for testing.</summary>
    public Func<Smtp, Task<SmtpResult>>? AuthenticateAsync { get; init; }
}

/// <summary>
/// Helpers for establishing SMTP sessions.
/// </summary>
public static class SmtpSessionService {
    /// <summary>
    /// Attempts to connect and authenticate using the provided SMTP client/configuration.
    /// </summary>
    public static async Task<SmtpConnectResult> ConnectAndAuthenticateAsync(Smtp smtp, SmtpSessionRequest request, CancellationToken cancellationToken = default) {
        if (smtp is null) {
            throw new ArgumentNullException(nameof(smtp));
        }
        if (request is null) {
            throw new ArgumentNullException(nameof(request));
        }

        smtp.Timeout = request.TimeoutMs;
        smtp.RetryCount = request.RetryCount;
        smtp.RetryDelayMilliseconds = request.RetryDelayMilliseconds;
        smtp.RetryDelayBackoff = request.RetryDelayBackoff;
        smtp.SkipCertificateValidation = request.SkipCertificateValidation;
        smtp.CheckCertificateRevocation = !request.SkipCertificateRevocation;
        smtp.DryRun = request.DryRun;

        var secureOptions = request.SecureSocketOptions;
        var connectFunc = request.ConnectAsync ?? (_ => smtp.ConnectAsync(request.Server, request.Port, secureOptions, request.UseSsl));
        var connectResult = await connectFunc(smtp).ConfigureAwait(false);
        if (!connectResult.Status) {
            return new SmtpConnectResult(false, secureOptions, "connect_failed", connectResult.Error ?? "Connect failed.", true);
        }

        var authenticateFunc = request.AuthenticateAsync ?? (_ => smtp.AuthenticateAsync(
            new NetworkCredential(request.UserName, request.Password),
            request.AuthMode == ProtocolAuthMode.OAuth2));

        var authResult = await authenticateFunc(smtp).ConfigureAwait(false);
        if (!authResult.Status) {
            return new SmtpConnectResult(false, secureOptions, "auth_failed", authResult.Error ?? "Authentication failed.", false);
        }

        return new SmtpConnectResult(true, secureOptions, null, null, false);
    }

    /// <summary>
    /// Best-effort SMTP disconnect/dispose helper.
    /// </summary>
    public static void DisposeQuietly(Smtp? smtp) {
        if (smtp is null) {
            return;
        }

        try {
            smtp.Disconnect();
        } catch {
            // best-effort
        }

        try {
            smtp.Dispose();
        } catch {
            // best-effort
        }
    }
}
