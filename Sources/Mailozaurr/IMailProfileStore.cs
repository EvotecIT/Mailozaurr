namespace Mailozaurr;

/// <summary>
/// Persists reusable mail profiles.
/// </summary>
public interface IMailProfileStore {
    /// <summary>Returns all known profiles.</summary>
    Task<IReadOnlyList<MailProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a profile by identifier.</summary>
    Task<MailProfile?> GetByIdAsync(string profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates a profile. An existing profile identifier must retain its provider kind;
    /// delete and recreate the profile when changing providers so its secrets are removed first.
    /// </summary>
    Task SaveAsync(MailProfile profile, CancellationToken cancellationToken = default);

    /// <summary>Removes a profile by identifier.</summary>
    Task<bool> RemoveAsync(string profileId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Coordinates maintenance that must observe a profile inventory which cannot change during the operation.
/// </summary>
public interface IMailProfileMaintenanceCoordinator {
    /// <summary>
    /// Invokes an operation while profile saves and removals are blocked, including across processes when the store is shared.
    /// </summary>
    Task<TResult> ExecuteWithStableProfileIdsAsync<TResult>(
        Func<IReadOnlyCollection<string>, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
