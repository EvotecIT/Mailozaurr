namespace Mailozaurr.Application;

/// <summary>
/// Persists reusable mail profiles.
/// </summary>
public interface IMailProfileStore {
    /// <summary>Returns all known profiles.</summary>
    Task<IReadOnlyList<MailProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a profile by identifier.</summary>
    Task<MailProfile?> GetByIdAsync(string profileId, CancellationToken cancellationToken = default);

    /// <summary>Saves or updates a profile.</summary>
    Task SaveAsync(MailProfile profile, CancellationToken cancellationToken = default);

    /// <summary>Removes a profile by identifier.</summary>
    Task<bool> RemoveAsync(string profileId, CancellationToken cancellationToken = default);
}
