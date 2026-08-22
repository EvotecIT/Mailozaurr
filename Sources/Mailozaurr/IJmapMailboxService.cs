namespace Mailozaurr;

/// <summary>Profile-backed JMAP mail application service.</summary>
public interface IJmapMailboxService {
    /// <summary>Gets the authoritative JMAP Session resource.</summary>
    Task<JmapSessionResource> GetSessionAsync(string profileId, CancellationToken cancellationToken = default);
    /// <summary>Lists mailboxes.</summary>
    Task<IReadOnlyList<JmapMailbox>> ListMailboxesAsync(string profileId, CancellationToken cancellationToken = default);
    /// <summary>Queries email identifiers.</summary>
    Task<JmapEmailQueryResult> QueryEmailsAsync(string profileId, JmapEmailFilter? filter = null, IReadOnlyList<JmapComparator>? sort = null, int position = 0, int limit = 100, bool collapseThreads = false, CancellationToken cancellationToken = default);
    /// <summary>Gets email objects.</summary>
    Task<JmapEmailGetResult> GetEmailsAsync(string profileId, IReadOnlyCollection<string> ids, IReadOnlyCollection<string>? properties = null, CancellationToken cancellationToken = default);
    /// <summary>Gets email changes since an opaque state token.</summary>
    Task<JmapEmailChangesResult> GetEmailChangesAsync(string profileId, string sinceState, int maxChanges = 1000, CancellationToken cancellationToken = default);
    /// <summary>Gets thread objects.</summary>
    Task<IReadOnlyList<JmapThread>> GetThreadsAsync(string profileId, IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default);
    /// <summary>Lists sending identities when submission is available.</summary>
    Task<IReadOnlyList<JmapIdentity>> ListIdentitiesAsync(string profileId, CancellationToken cancellationToken = default);
}
