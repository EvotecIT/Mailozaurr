namespace Mailozaurr.Application;

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

/// <summary>Removes every secret associated with a profile, including custom secret names.</summary>
public interface IMailProfileSecretCleanup {
    /// <summary>Returns all secret names and values owned by the profile.</summary>
    Task<IReadOnlyDictionary<string, string>> GetProfileSecretsAsync(
        string profileId,
        CancellationToken cancellationToken = default);

    /// <summary>Removes all secrets owned by the profile.</summary>
    Task RemoveProfileSecretsAsync(string profileId, CancellationToken cancellationToken = default);
}
