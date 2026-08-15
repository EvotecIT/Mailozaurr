namespace Mailozaurr.Hosting;

/// <summary>
/// Stores profile secrets in process memory for transient applications, tests, and one-shot delivery workflows.
/// Values are not persisted or protected outside the current process.
/// </summary>
public sealed class InMemoryMailSecretStore : IMailSecretStore, IMailProfileSecretCleanup {
    private readonly Dictionary<string, Dictionary<string, string>> _profileSecrets =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <inheritdoc />
    public async Task<string?> GetSecretAsync(
        string profileId,
        string secretName,
        CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        ValidateKeyPart(secretName, nameof(secretName));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            return _profileSecrets.TryGetValue(profileId.Trim(), out Dictionary<string, string>? secrets) &&
                   secrets.TryGetValue(secretName.Trim(), out string? value)
                ? value
                : null;
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetSecretAsync(
        string profileId,
        string secretName,
        string secretValue,
        CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        ValidateKeyPart(secretName, nameof(secretName));
        if (secretValue == null) throw new ArgumentNullException(nameof(secretValue));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            string normalizedProfileId = profileId.Trim();
            if (!_profileSecrets.TryGetValue(normalizedProfileId, out Dictionary<string, string>? secrets)) {
                secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _profileSecrets[normalizedProfileId] = secrets;
            }
            secrets[secretName.Trim()] = secretValue;
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveSecretAsync(
        string profileId,
        string secretName,
        CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        ValidateKeyPart(secretName, nameof(secretName));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            string normalizedProfileId = profileId.Trim();
            if (!_profileSecrets.TryGetValue(normalizedProfileId, out Dictionary<string, string>? secrets)) {
                return false;
            }

            bool removed = secrets.Remove(secretName.Trim());
            if (secrets.Count == 0) {
                _profileSecrets.Remove(normalizedProfileId);
            }
            return removed;
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetProfileSecretsAsync(
        string profileId,
        CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            return _profileSecrets.TryGetValue(profileId.Trim(), out Dictionary<string, string>? secrets)
                ? new Dictionary<string, string>(secrets, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task RemoveProfileSecretsAsync(
        string profileId,
        CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            _profileSecrets.Remove(profileId.Trim());
        } finally {
            _gate.Release();
        }
    }

    private static void ValidateKeyPart(string value, string parameterName) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException("Value is required.", parameterName);
        }
    }
}
