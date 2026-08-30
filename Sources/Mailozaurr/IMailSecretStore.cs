namespace Mailozaurr;

/// <summary>
/// Stores secrets associated with mail profiles.
/// </summary>
public interface IMailSecretStore {
    /// <summary>Retrieves a secret value by profile id and secret name.</summary>
    Task<string?> GetSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default);

    /// <summary>Stores or replaces a secret value.</summary>
    Task SetSecretAsync(string profileId, string secretName, string secretValue, CancellationToken cancellationToken = default);

    /// <summary>Removes a secret value.</summary>
    Task<bool> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default);
}

internal interface IMailSecretStoreCredentialContextCoordinator {
    Task<TResult> ExecuteWithProfileSecretsLockedAsync<TResult>(
        string profileId,
        Func<bool, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}

/// <summary>Removes every secret associated with a profile, including custom secret names.</summary>
public interface IMailProfileSecretCleanup {
    /// <summary>Returns all secret names and values owned by the profile.</summary>
    Task<IReadOnlyDictionary<string, string>> GetProfileSecretsAsync(
        string profileId,
        CancellationToken cancellationToken = default);

    /// <summary>Removes all secrets owned by the profile.</summary>
    Task RemoveProfileSecretsAsync(string profileId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Removes legacy profile secrets using the complete set of profile ids to disambiguate separator-bearing ids
/// from separator-bearing secret names.
/// </summary>
public interface IMailProfileSecretContextCleanup : IMailProfileSecretCleanup {
    /// <summary>Removes secrets owned by the profile after resolving legacy keys against known profile ids.</summary>
    Task RemoveProfileSecretsAsync(
        string profileId,
        IReadOnlyCollection<string> knownProfileIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an opaque, store-native snapshot of every secret associated with a profile.
/// </summary>
public interface IMailProfileSecretSnapshot {
}

/// <summary>
/// Captures and restores profile secrets without requiring their protected representation to be decoded.
/// </summary>
public interface IMailProfileSecretSnapshotStore {
    /// <summary>Captures every secret currently associated with the profile.</summary>
    Task<IMailProfileSecretSnapshot> CaptureProfileSecretsAsync(
        string profileId,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces the profile's current secrets with the captured snapshot.</summary>
    Task RestoreProfileSecretsAsync(
        string profileId,
        IMailProfileSecretSnapshot snapshot,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Inspects and removes profile-scoped secret sets whose owning profiles no longer exist.
/// </summary>
public interface IMailProfileSecretMaintenanceStore {
    /// <summary>
    /// Reports structured orphan secret sets and ambiguous legacy keys without decrypting secret values.
    /// </summary>
    Task<MailProfileSecretMaintenanceResult> InspectOrphanedSecretsAsync(
        IReadOnlyCollection<string> knownProfileIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes structured orphan secret sets while retaining ambiguous legacy keys that cannot be assigned safely.
    /// </summary>
    Task<MailProfileSecretMaintenanceResult> RemoveOrphanedSecretsAsync(
        IReadOnlyCollection<string> knownProfileIds,
        CancellationToken cancellationToken = default);
}
