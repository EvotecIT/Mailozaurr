namespace Mailozaurr;

/// <summary>Owns a Gmail API client and its high-level mailbox browser.</summary>
public sealed class GmailMailboxBrowserSession : IDisposable {
    private readonly GmailApiClient _gmail;

    /// <summary>Creates a session with an internally managed HTTP client.</summary>
    public GmailMailboxBrowserSession(OAuthCredential credential, string userId = "me",
        Func<CancellationToken, Task<string>>? refreshToken = null, Uri? baseAddress = null) {
        if (credential == null) throw new ArgumentNullException(nameof(credential));
        _gmail = baseAddress == null
            ? new GmailApiClient(credential, refreshToken)
            : new GmailApiClient(new HttpClient(), refreshToken, credential, baseAddress, ownsHttpClient: true);
        Browser = new GmailMailboxBrowser(_gmail, userId);
    }

    /// <summary>Creates a session over a caller-owned HTTP client.</summary>
    public GmailMailboxBrowserSession(HttpClient client, OAuthCredential credential, string userId = "me",
        Func<CancellationToken, Task<string>>? refreshToken = null, Uri? baseAddress = null) {
        if (client == null) throw new ArgumentNullException(nameof(client));
        if (credential == null) throw new ArgumentNullException(nameof(credential));
        _gmail = new GmailApiClient(client, refreshToken, credential, baseAddress);
        Browser = new GmailMailboxBrowser(_gmail, userId);
    }

    /// <summary>High-level Gmail mailbox browser.</summary>
    public GmailMailboxBrowser Browser { get; }

    /// <summary>Creates a session from an existing OAuth access token.</summary>
    public static GmailMailboxBrowserSession CreateWithAccessToken(string accessToken,
        HttpClient? client = null, string userId = "me", DateTimeOffset? expiresOn = null,
        Func<CancellationToken, Task<string>>? refreshToken = null, Uri? baseAddress = null) {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(accessToken));
        string normalizedUserId = string.IsNullOrWhiteSpace(userId) ? "me" : userId.Trim();
        var credential = new OAuthCredential {
            UserName = normalizedUserId,
            AccessToken = accessToken.Trim(),
            ExpiresOn = expiresOn ?? DateTimeOffset.MaxValue
        };
        return client == null
            ? new GmailMailboxBrowserSession(credential, normalizedUserId, refreshToken, baseAddress)
            : new GmailMailboxBrowserSession(client, credential, normalizedUserId, refreshToken, baseAddress);
    }

    /// <inheritdoc />
    public void Dispose() {
        _gmail.Dispose();
        GC.SuppressFinalize(this);
    }
}
