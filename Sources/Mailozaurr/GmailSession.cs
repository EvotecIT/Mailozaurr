namespace Mailozaurr;

/// <summary>
/// Represents an authenticated Gmail API session for a resolved mailbox context.
/// </summary>
public sealed class GmailSession : IDisposable {
    /// <summary>
    /// Creates a new Gmail session wrapper.
    /// </summary>
    public GmailSession(GmailApiClient client, string userId) {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        UserId = string.IsNullOrWhiteSpace(userId) ? "me" : userId.Trim();
        Browser = new GmailMailboxBrowser(Client, UserId);
    }

    /// <summary>Gmail API client.</summary>
    public GmailApiClient Client { get; }

    /// <summary>Resolved mailbox user id used for Gmail requests.</summary>
    public string UserId { get; }

    /// <summary>High-level Gmail mailbox browser.</summary>
    public GmailMailboxBrowser Browser { get; }

    /// <inheritdoc />
    public void Dispose() {
        Client.Dispose();
        GC.SuppressFinalize(this);
    }
}