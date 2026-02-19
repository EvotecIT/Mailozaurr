using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Owns a <see cref="GraphApiClient"/> instance and exposes a <see cref="GraphMailboxBrowser"/> built on top of it.
/// </summary>
public sealed class GraphMailboxBrowserSession : IDisposable {
    private readonly GraphApiClient _graph;

    /// <summary>
    /// Initializes a new session with an internally managed <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="credential">OAuth credential used for Graph requests.</param>
    /// <param name="refreshToken">Optional token refresh delegate.</param>
    /// <param name="baseAddress">Optional Graph API base address.</param>
    public GraphMailboxBrowserSession(
        OAuthCredential credential,
        Func<CancellationToken, Task<string>>? refreshToken = null,
        Uri? baseAddress = null) {
        if (credential == null) {
            throw new ArgumentNullException(nameof(credential));
        }

        _graph = new GraphApiClient(credential, refreshToken, baseAddress);
        Browser = new GraphMailboxBrowser(_graph);
    }

    /// <summary>
    /// Initializes a new session with an externally provided <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="client">HTTP client to use for Graph requests.</param>
    /// <param name="credential">OAuth credential used for Graph requests.</param>
    /// <param name="refreshToken">Optional token refresh delegate.</param>
    /// <param name="baseAddress">Optional Graph API base address used when <paramref name="client"/> has no base address configured.</param>
    public GraphMailboxBrowserSession(
        HttpClient client,
        OAuthCredential credential,
        Func<CancellationToken, Task<string>>? refreshToken = null,
        Uri? baseAddress = null) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (credential == null) {
            throw new ArgumentNullException(nameof(credential));
        }

        _graph = new GraphApiClient(client, refreshToken, credential, baseAddress);
        Browser = new GraphMailboxBrowser(_graph);
    }

    /// <summary>
    /// Gets the high-level mailbox browser.
    /// </summary>
    public GraphMailboxBrowser Browser { get; }

    /// <summary>
    /// Creates a session from a raw OAuth access token.
    /// </summary>
    /// <param name="accessToken">OAuth access token.</param>
    /// <param name="client">Optional HTTP client instance.</param>
    /// <param name="userName">Credential user name (defaults to <c>me</c>).</param>
    /// <param name="expiresOn">Credential expiration time (defaults to <see cref="DateTimeOffset.MaxValue"/>).</param>
    /// <param name="refreshToken">Optional token refresh delegate.</param>
    /// <param name="baseAddress">Optional Graph API base address.</param>
    /// <returns>A new <see cref="GraphMailboxBrowserSession"/>.</returns>
    public static GraphMailboxBrowserSession CreateWithAccessToken(
        string accessToken,
        HttpClient? client = null,
        string userName = "me",
        DateTimeOffset? expiresOn = null,
        Func<CancellationToken, Task<string>>? refreshToken = null,
        Uri? baseAddress = null) {
        if (string.IsNullOrWhiteSpace(accessToken)) {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(accessToken));
        }

        var credential = new OAuthCredential {
            UserName = string.IsNullOrWhiteSpace(userName) ? "me" : userName.Trim(),
            AccessToken = accessToken.Trim(),
            ExpiresOn = expiresOn ?? DateTimeOffset.MaxValue
        };

        return client == null
            ? new GraphMailboxBrowserSession(credential, refreshToken, baseAddress)
            : new GraphMailboxBrowserSession(client, credential, refreshToken, baseAddress);
    }

    /// <inheritdoc />
    public void Dispose() {
        _graph.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Owns a <see cref="GmailApiClient"/> instance and exposes a <see cref="GmailMailboxBrowser"/> built on top of it.
/// </summary>
public sealed class GmailMailboxBrowserSession : IDisposable {
    private readonly GmailApiClient _gmail;

    /// <summary>
    /// Initializes a new session with an internally managed <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="credential">OAuth credential used for Gmail requests.</param>
    /// <param name="userId">Mailbox user id (defaults to <c>me</c>).</param>
    /// <param name="refreshToken">Optional token refresh delegate.</param>
    /// <param name="baseAddress">Optional Gmail API base address.</param>
    public GmailMailboxBrowserSession(
        OAuthCredential credential,
        string userId = "me",
        Func<CancellationToken, Task<string>>? refreshToken = null,
        Uri? baseAddress = null) {
        if (credential == null) {
            throw new ArgumentNullException(nameof(credential));
        }

        _gmail = new GmailApiClient(new HttpClient(), refreshToken, credential, baseAddress);
        Browser = new GmailMailboxBrowser(_gmail, userId);
    }

    /// <summary>
    /// Initializes a new session with an externally provided <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="client">HTTP client to use for Gmail requests.</param>
    /// <param name="credential">OAuth credential used for Gmail requests.</param>
    /// <param name="userId">Mailbox user id (defaults to <c>me</c>).</param>
    /// <param name="refreshToken">Optional token refresh delegate.</param>
    /// <param name="baseAddress">Optional Gmail API base address used when <paramref name="client"/> has no base address configured.</param>
    public GmailMailboxBrowserSession(
        HttpClient client,
        OAuthCredential credential,
        string userId = "me",
        Func<CancellationToken, Task<string>>? refreshToken = null,
        Uri? baseAddress = null) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (credential == null) {
            throw new ArgumentNullException(nameof(credential));
        }

        _gmail = new GmailApiClient(client, refreshToken, credential, baseAddress);
        Browser = new GmailMailboxBrowser(_gmail, userId);
    }

    /// <summary>
    /// Gets the high-level mailbox browser.
    /// </summary>
    public GmailMailboxBrowser Browser { get; }

    /// <summary>
    /// Creates a session from a raw OAuth access token.
    /// </summary>
    /// <param name="accessToken">OAuth access token.</param>
    /// <param name="client">Optional HTTP client instance.</param>
    /// <param name="userId">Mailbox user id (defaults to <c>me</c>).</param>
    /// <param name="expiresOn">Credential expiration time (defaults to <see cref="DateTimeOffset.MaxValue"/>).</param>
    /// <param name="refreshToken">Optional token refresh delegate.</param>
    /// <param name="baseAddress">Optional Gmail API base address.</param>
    /// <returns>A new <see cref="GmailMailboxBrowserSession"/>.</returns>
    public static GmailMailboxBrowserSession CreateWithAccessToken(
        string accessToken,
        HttpClient? client = null,
        string userId = "me",
        DateTimeOffset? expiresOn = null,
        Func<CancellationToken, Task<string>>? refreshToken = null,
        Uri? baseAddress = null) {
        if (string.IsNullOrWhiteSpace(accessToken)) {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(accessToken));
        }

        var normalizedUserId = string.IsNullOrWhiteSpace(userId) ? "me" : userId.Trim();
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
