using MailKit.Security;

namespace Mailozaurr;

/// <summary>
/// Result of connecting and authenticating an SMTP session.
/// </summary>
public sealed class SmtpConnectAuthenticateResult {
    /// <summary>
    /// True when both connect and authenticate succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Effective secure socket options used for the connection.
    /// </summary>
    public SecureSocketOptions SecureSocketOptions { get; init; } = SecureSocketOptions.Auto;

    /// <summary>
    /// Stable error code for connect/auth failures.
    /// </summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable error text.
    /// </summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>
    /// True when the failure is likely transient.
    /// </summary>
    public bool IsTransient { get; init; }
}