#pragma warning disable CS1591
#pragma warning disable CS8600,CS8601,CS8602,CS8603,CS8604,CS8618,CS8625
#nullable enable
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace Mailozaurr;

public enum ProtocolAuthMode {
    Basic = 0,
    OAuth2 = 1
}

public static class ProtocolAuth {
    public static ProtocolAuthMode ParseMode(string? raw) {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0) {
            return ProtocolAuthMode.Basic;
        }

        if (value.Equals("basic", StringComparison.OrdinalIgnoreCase)) {
            return ProtocolAuthMode.Basic;
        }

        if (value.Equals("oauth2", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("xoauth2", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("oauth", StringComparison.OrdinalIgnoreCase)) {
            return ProtocolAuthMode.OAuth2;
        }

        return ProtocolAuthMode.Basic;
    }

    public static Task AuthenticateImapAsync(
        ImapClient client,
        string userName,
        string secret,
        ProtocolAuthMode mode,
        CancellationToken ct) {
        if (client is null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (string.IsNullOrWhiteSpace(userName)) {
            throw new InvalidOperationException("IMAP username is required.");
        }
        if (string.IsNullOrWhiteSpace(secret)) {
            throw new InvalidOperationException("IMAP secret/token is required.");
        }

        if (mode == ProtocolAuthMode.OAuth2) {
            return client.AuthenticateAsync(new SaslMechanismOAuth2(userName.Trim(), secret.Trim()), ct);
        }

        return client.AuthenticateAsync(new NetworkCredential(userName.Trim(), secret), ct);
    }

    public static Task AuthenticateSmtpAsync(
        SmtpClient client,
        string userName,
        string secret,
        ProtocolAuthMode mode,
        CancellationToken ct) {
        if (client is null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (string.IsNullOrWhiteSpace(userName)) {
            throw new InvalidOperationException("SMTP username is required.");
        }
        if (string.IsNullOrWhiteSpace(secret)) {
            throw new InvalidOperationException("SMTP secret/token is required.");
        }

        if (mode == ProtocolAuthMode.OAuth2) {
            return client.AuthenticateAsync(new SaslMechanismOAuth2(userName.Trim(), secret.Trim()), ct);
        }

        return client.AuthenticateAsync(userName.Trim(), secret, ct);
    }

    public static Task AuthenticatePop3Async(
        Pop3Client client,
        string userName,
        string secret,
        ProtocolAuthMode mode,
        CancellationToken ct) {
        if (client is null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (string.IsNullOrWhiteSpace(userName)) {
            throw new InvalidOperationException("POP3 username is required.");
        }
        if (string.IsNullOrWhiteSpace(secret)) {
            throw new InvalidOperationException("POP3 secret/token is required.");
        }

        if (mode == ProtocolAuthMode.OAuth2) {
            return client.AuthenticateAsync(new SaslMechanismOAuth2(userName.Trim(), secret.Trim()), ct);
        }

        return client.AuthenticateAsync(userName.Trim(), secret, ct);
    }
}
