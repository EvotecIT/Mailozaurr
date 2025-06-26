using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Security;
using System.Net;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Utility methods for establishing IMAP and POP3 connections with optional retry logic.
/// </summary>
public static class ConnectionHelpers {
    /// <summary>
    /// Connects and authenticates an <see cref="ImapClient"/> with retry logic.
    /// </summary>
    public static async Task<ImapClient> ConnectImapAsync(
        string server,
        int port,
        SecureSocketOptions options,
        bool skipCertificateRevocation,
        bool skipCertificateValidation,
        int timeout,
        NetworkCredential? credential,
        bool oauth2,
        int retryCount,
        int retryDelayMilliseconds,
        double retryDelayBackoff) {
        var delay = retryDelayMilliseconds;
        Exception? lastError = null;
        for (var attempt = 0; attempt <= retryCount; attempt++) {
            var client = new ImapClient();
            try {
                await client.ConnectAsync(server, port, options);
                if (skipCertificateRevocation) {
                    client.CheckCertificateRevocation = false;
                }
                if (skipCertificateValidation) {
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                }
                if (client.Timeout != timeout) {
                    client.Timeout = timeout;
                }
                if (!client.IsConnected) {
                    throw new InvalidOperationException("Client is not connected after ConnectAsync.");
                }
                if (credential != null) {
                    if (oauth2) {
                        var sasl = new MailKit.Security.SaslMechanismOAuth2(credential.UserName, credential.Password);
                        await client.AuthenticateAsync(sasl);
                    } else {
                        await client.AuthenticateAsync(credential);
                    }
                    if (!client.IsAuthenticated) {
                        throw new InvalidOperationException("Authentication failed.");
                    }
                }
                try {
                    await client.Inbox.OpenAsync(MailKit.FolderAccess.ReadOnly);
                } catch (Exception ex) {
                    LoggingMessages.Logger.WriteWarning($"Connect-IMAP - Failed to open inbox: {ex.Message}");
                }
                return client;
            } catch (Exception ex) {
                lastError = ex;
                if (client.IsConnected) {
                    try { await client.DisconnectAsync(true); } catch { }
                }
            }
            if (attempt < retryCount) {
                if (delay > 0) await Task.Delay(delay);
                delay = (int)(delay * retryDelayBackoff);
            }
        }
        throw lastError ?? new Exception("Unable to connect to IMAP server.");
    }

    /// <summary>
    /// Connects and authenticates a <see cref="Pop3Client"/> with retry logic.
    /// </summary>
    public static async Task<Pop3Client> ConnectPop3Async(
        string server,
        int port,
        SecureSocketOptions options,
        bool skipCertificateRevocation,
        bool skipCertificateValidation,
        int timeout,
        NetworkCredential? credential,
        bool oauth2,
        int retryCount,
        int retryDelayMilliseconds,
        double retryDelayBackoff) {
        var delay = retryDelayMilliseconds;
        Exception? lastError = null;
        for (var attempt = 0; attempt <= retryCount; attempt++) {
            var client = new Pop3Client();
            try {
                await client.ConnectAsync(server, port, options);
                if (skipCertificateRevocation) {
                    client.CheckCertificateRevocation = false;
                }
                if (skipCertificateValidation) {
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                }
                if (client.Timeout != timeout) {
                    client.Timeout = timeout;
                }
                if (!client.IsConnected) {
                    throw new InvalidOperationException("Client is not connected after ConnectAsync.");
                }
                if (credential != null) {
                    if (oauth2) {
                        var sasl = new MailKit.Security.SaslMechanismOAuth2(credential.UserName, credential.Password);
                        await client.AuthenticateAsync(sasl);
                    } else {
                        await client.AuthenticateAsync(credential);
                    }
                    if (!client.IsAuthenticated) {
                        throw new InvalidOperationException("Authentication failed.");
                    }
                }
                return client;
            } catch (Exception ex) {
                lastError = ex;
                if (client.IsConnected) {
                    try { await client.DisconnectAsync(true); } catch { }
                }
            }
            if (attempt < retryCount) {
                if (delay > 0) await Task.Delay(delay);
                delay = (int)(delay * retryDelayBackoff);
            }
        }
        throw lastError ?? new Exception("Unable to connect to POP3 server.");
    }
}
