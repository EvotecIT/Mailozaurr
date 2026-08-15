using MailKit.Net.Imap;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Describes the parameters required for an authenticated IMAP session.
/// </summary>
public sealed class ImapSessionRequest {
    /// <summary>
    /// Gets or sets the underlying connection request.
    /// </summary>
    public ImapConnectionRequest Connection { get; init; } = new("localhost", 993);

    /// <summary>
    /// Gets or sets the auth username.
    /// </summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the auth secret or token.
    /// </summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the protocol auth mode.
    /// </summary>
    public ProtocolAuthMode AuthMode { get; init; } = ProtocolAuthMode.Basic;

    /// <summary>
    /// Gets or sets an optional authenticate delegate used for testing/custom flows.
    /// </summary>
    public Func<ImapClient, CancellationToken, Task>? AuthenticateAsync { get; init; }
}

/// <summary>
/// Helpers for establishing authenticated IMAP sessions.
/// </summary>
public static class ImapSessionService {
    /// <summary>
    /// Connects and authenticates an IMAP client from a reusable session request.
    /// </summary>
    public static Task<ImapClient> ConnectAsync(ImapSessionRequest request, CancellationToken cancellationToken = default) {
        if (request is null) {
            throw new ArgumentNullException(nameof(request));
        }

        return ImapConnector.ConnectAsync(
            request.Connection,
            request.AuthenticateAsync ?? ((client, ct) => ProtocolAuth.AuthenticateImapAsync(
                client,
                request.UserName,
                request.Secret,
                request.AuthMode,
                ct)),
            cancellationToken);
    }
}