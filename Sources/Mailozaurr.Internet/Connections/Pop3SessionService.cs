using MailKit.Net.Pop3;

namespace Mailozaurr;

/// <summary>
/// Describes the parameters required for an authenticated POP3 session.
/// </summary>
public sealed class Pop3SessionRequest {
    /// <summary>Underlying POP3 transport request.</summary>
    public Pop3ConnectionRequest Connection { get; init; } = new("localhost");

    /// <summary>Authentication username.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Authentication secret or token.</summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>Protocol authentication mode.</summary>
    public ProtocolAuthMode AuthMode { get; init; } = ProtocolAuthMode.Basic;

    /// <summary>Optional authentication override used by custom flows and tests.</summary>
    public Func<Pop3Client, CancellationToken, Task>? AuthenticateAsync { get; init; }
}

/// <summary>
/// Establishes authenticated POP3 sessions from reusable requests.
/// </summary>
public static class Pop3SessionService {
    /// <summary>Connects and authenticates a POP3 client.</summary>
    public static Task<Pop3Client> ConnectAsync(
        Pop3SessionRequest request,
        CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        return Pop3Connector.ConnectAsync(
            request.Connection,
            request.AuthenticateAsync ?? ((client, token) => ProtocolAuth.AuthenticatePop3Async(
                client,
                request.UserName,
                request.Secret,
                request.AuthMode,
                token)),
            cancellationToken);
    }
}
