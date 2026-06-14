using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Net.Smtp;
using MailKit.Security;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Protocol authentication mode used by IMAP/POP3/SMTP connectors.
/// </summary>
public enum ProtocolAuthMode {
    /// <summary>Username/password authentication.</summary>
    Basic = 0,

    /// <summary>OAuth2 (XOAUTH2) token authentication.</summary>
    OAuth2 = 1
}

/// <summary>
/// Shared protocol authentication helpers for IMAP/POP3/SMTP clients.
/// </summary>
public static class ProtocolAuth {
    /// <summary>
    /// Parses protocol auth mode text.
    /// </summary>
    public static ProtocolAuthMode ParseMode(string? raw, ProtocolAuthMode fallback = ProtocolAuthMode.Basic) {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0) {
            return fallback;
        }
        if (value.Equals("basic", StringComparison.OrdinalIgnoreCase)) {
            return ProtocolAuthMode.Basic;
        }
        if (value.Equals("oauth2", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("xoauth2", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("oauth", StringComparison.OrdinalIgnoreCase)) {
            return ProtocolAuthMode.OAuth2;
        }

        return fallback;
    }

    /// <summary>
    /// Authenticates an IMAP client using selected auth mode.
    /// </summary>
    public static Task AuthenticateImapAsync(
        ImapClient client,
        string userName,
        string secret,
        ProtocolAuthMode mode,
        CancellationToken cancellationToken = default) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (string.IsNullOrWhiteSpace(userName)) {
            throw new InvalidOperationException("IMAP username is required.");
        }
        if (string.IsNullOrWhiteSpace(secret)) {
            throw new InvalidOperationException("IMAP secret/token is required.");
        }

        var normalizedUser = userName.Trim();
        var normalizedSecret = secret.Trim();
        if (mode == ProtocolAuthMode.OAuth2) {
            return client.AuthenticateAsync(new SaslMechanismOAuth2(normalizedUser, normalizedSecret), cancellationToken);
        }

        return client.AuthenticateAsync(new NetworkCredential(normalizedUser, normalizedSecret), cancellationToken);
    }

    /// <summary>
    /// Authenticates an SMTP client using selected auth mode.
    /// </summary>
    public static Task AuthenticateSmtpAsync(
        SmtpClient client,
        string userName,
        string secret,
        ProtocolAuthMode mode,
        CancellationToken cancellationToken = default) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (string.IsNullOrWhiteSpace(userName)) {
            throw new InvalidOperationException("SMTP username is required.");
        }
        if (string.IsNullOrWhiteSpace(secret)) {
            throw new InvalidOperationException("SMTP secret/token is required.");
        }

        var normalizedUser = userName.Trim();
        if (mode == ProtocolAuthMode.OAuth2) {
            return client.AuthenticateAsync(new SaslMechanismOAuth2(normalizedUser, secret.Trim()), cancellationToken);
        }

        return client.AuthenticateAsync(normalizedUser, secret, cancellationToken);
    }

    /// <summary>
    /// Authenticates a POP3 client using selected auth mode.
    /// </summary>
    public static Task AuthenticatePop3Async(
        Pop3Client client,
        string userName,
        string secret,
        ProtocolAuthMode mode,
        CancellationToken cancellationToken = default) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (string.IsNullOrWhiteSpace(userName)) {
            throw new InvalidOperationException("POP3 username is required.");
        }
        if (string.IsNullOrWhiteSpace(secret)) {
            throw new InvalidOperationException("POP3 secret/token is required.");
        }

        var normalizedUser = userName.Trim();
        if (mode == ProtocolAuthMode.OAuth2) {
            return client.AuthenticateAsync(new SaslMechanismOAuth2(normalizedUser, secret.Trim()), cancellationToken);
        }

        return client.AuthenticateAsync(normalizedUser, secret, cancellationToken);
    }
}