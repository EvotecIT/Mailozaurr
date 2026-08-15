namespace Mailozaurr.Hosting;

/// <summary>
/// Manages secrets associated with saved mail profiles.
/// </summary>
public interface IMailProfileSecretService {
    /// <summary>Saves or replaces a secret for an existing profile.</summary>
    Task<OperationResult> SetSecretAsync(string profileId, string secretName, string secretValue, CancellationToken cancellationToken = default);

    /// <summary>Saves or replaces a secret for an existing profile, optionally by copying from another stored secret reference.</summary>
    Task<OperationResult> SetSecretAsync(string profileId, string secretName, string? secretValue, string? secretReference, CancellationToken cancellationToken = default);

    /// <summary>Removes a secret from an existing profile.</summary>
    Task<OperationResult> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default);
}