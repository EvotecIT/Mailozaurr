namespace Mailozaurr;

/// <summary>Default profile-backed JMAP mail application service.</summary>
public sealed class JmapMailboxService : IJmapMailboxService {
    private readonly IMailProfileStore _profileStore;
    private readonly IJmapSessionFactory _sessionFactory;

    /// <summary>Creates a JMAP mailbox service.</summary>
    public JmapMailboxService(IMailProfileStore profileStore, IJmapSessionFactory sessionFactory) {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    }

    /// <inheritdoc />
    public Task<JmapSessionResource> GetSessionAsync(string profileId, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, (session, token) => session.Client.GetSessionAsync(cancellationToken: token), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<JmapMailbox>> ListMailboxesAsync(string profileId, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, (session, token) => session.Client.ListMailboxesAsync(session.AccountId, token), cancellationToken);

    /// <inheritdoc />
    public Task<JmapEmailQueryResult> QueryEmailsAsync(string profileId, JmapEmailFilter? filter = null, IReadOnlyList<JmapComparator>? sort = null, int position = 0, int limit = 100, bool collapseThreads = false, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, (session, token) => session.Client.QueryEmailsAsync(filter, sort, position, limit, collapseThreads, session.AccountId, token), cancellationToken);

    /// <inheritdoc />
    public Task<JmapEmailGetResult> GetEmailsAsync(string profileId, IReadOnlyCollection<string> ids, IReadOnlyCollection<string>? properties = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, (session, token) => session.Client.GetEmailsAsync(ids, properties, session.AccountId, token), cancellationToken);

    /// <inheritdoc />
    public Task<JmapEmailChangesResult> GetEmailChangesAsync(string profileId, string sinceState, int maxChanges = 1000, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, (session, token) => session.Client.GetEmailChangesAsync(sinceState, maxChanges, session.AccountId, token), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<JmapThread>> GetThreadsAsync(string profileId, IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, (session, token) => session.Client.GetThreadsAsync(ids, session.AccountId, token), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<JmapIdentity>> ListIdentitiesAsync(string profileId, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, (session, token) => session.Client.ListIdentitiesAsync(session.AccountId, token), cancellationToken);

    private async Task<T> WithSessionAsync<T>(string profileId, Func<JmapSession, CancellationToken, Task<T>> operation, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("Profile id is required.", nameof(profileId));
        var profile = await _profileStore.GetByIdAsync(profileId.Trim(), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");
        if (profile.Kind != MailProfileKind.Jmap) throw new NotSupportedException($"Profile '{profile.Id}' is not a JMAP profile.");
        using var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await operation(session, cancellationToken).ConfigureAwait(false);
    }
}
