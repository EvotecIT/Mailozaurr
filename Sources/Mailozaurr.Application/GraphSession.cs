namespace Mailozaurr.Application;

/// <summary>
/// Represents an authenticated Graph API session for a resolved mailbox context.
/// </summary>
public sealed class GraphSession : IDisposable {
    /// <summary>
    /// Creates a new Graph session wrapper.
    /// </summary>
    public GraphSession(
        GraphApiClient client,
        string userId,
        OAuthCredential? credential = null,
        GraphCredential? graphCredential = null) {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        UserId = string.IsNullOrWhiteSpace(userId) ? "me" : userId.Trim();
        Credential = credential;
        GraphCredential = graphCredential;
    }

    /// <summary>Graph API client.</summary>
    public GraphApiClient Client { get; }

    /// <summary>Resolved mailbox user id used for Graph requests.</summary>
    public string UserId { get; }

    /// <summary>Resolved OAuth credential used to authenticate the session.</summary>
    public OAuthCredential? Credential { get; }

    /// <summary>Optional Graph credential metadata that can mint a fresh access token later.</summary>
    public GraphCredential? GraphCredential { get; }

    /// <inheritdoc />
    public void Dispose() {
        Client.Dispose();
        GC.SuppressFinalize(this);
    }
}