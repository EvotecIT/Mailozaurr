namespace Mailozaurr.Application;

/// <summary>
/// Stores protected secrets in a JSON document on disk.
/// </summary>
public sealed class FileMailSecretStore : IMailSecretStore, IMailProfileSecretCleanup {
    private readonly JsonFileDocumentStore<MailSecretStoreDocument> _store;
    private readonly ICredentialProtector _protector;
    /// <summary>
    /// Creates a new store using the default credential protector.
    /// </summary>
    public FileMailSecretStore(MailSecretStoreOptions? options = null)
        : this((options ?? new MailSecretStoreOptions()).GetFilePath(), CredentialProtection.Default) {
    }

    /// <summary>
    /// Creates a new store using the specified file path and protector.
    /// </summary>
    public FileMailSecretStore(string filePath, ICredentialProtector protector) {
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _store = new JsonFileDocumentStore<MailSecretStoreDocument>(
            filePath,
            "Secret store path is invalid.",
            ApplicationJsonContext.Default.MailSecretStoreDocument,
            static () => new MailSecretStoreDocument(),
            NormalizeDocument);
    }

    /// <inheritdoc />
    public Task<string?> GetSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        ValidateKeyPart(secretName, nameof(secretName));

        return _store.ReadAsync(document => {
            if (!TryGetProfileSecrets(document, profileId, out Dictionary<string, string> secrets) ||
                !secrets.TryGetValue(secretName.Trim(), out string? protectedValue)) {
                return null;
            }

            return _protector.Unprotect(protectedValue);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetSecretAsync(string profileId, string secretName, string secretValue, CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        ValidateKeyPart(secretName, nameof(secretName));
        if (secretValue == null) {
            throw new ArgumentNullException(nameof(secretValue));
        }

        await _store.UpdateAsync(document => {
            Dictionary<string, string> secrets = GetOrCreateProfileSecrets(document, profileId);
            secrets[secretName.Trim()] = _protector.Protect(secretValue);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        ValidateKeyPart(secretName, nameof(secretName));

        return _store.RemoveAsync(document => {
            if (!TryGetProfileSecrets(document, profileId, out Dictionary<string, string> secrets)) {
                return false;
            }

            bool removed = secrets.Remove(secretName.Trim());
            if (removed && secrets.Count == 0) {
                document.ProfileSecrets.Remove(profileId.Trim());
            }
            return removed;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, string>> GetProfileSecretsAsync(
        string profileId,
        CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        return _store.ReadAsync<IReadOnlyDictionary<string, string>>(document => {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (TryGetProfileSecrets(document, profileId, out Dictionary<string, string> secrets)) {
                foreach (KeyValuePair<string, string> secret in secrets) {
                    result[secret.Key] = _protector.Unprotect(secret.Value);
                }
            }
            return result;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveProfileSecretsAsync(string profileId, CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        return _store.UpdateAsync(document => document.ProfileSecrets.Remove(profileId.Trim()), cancellationToken);
    }

    private static void NormalizeDocument(MailSecretStoreDocument document) {
        if (document.ProfileSecrets == null) {
            document.ProfileSecrets = new Dictionary<string, Dictionary<string, string>>(
                StringComparer.OrdinalIgnoreCase);
        } else if (document.ProfileSecrets.Comparer != StringComparer.OrdinalIgnoreCase) {
            document.ProfileSecrets = new Dictionary<string, Dictionary<string, string>>(
                document.ProfileSecrets, StringComparer.OrdinalIgnoreCase);
        }

        foreach (string profileId in document.ProfileSecrets.Keys.ToArray()) {
            Dictionary<string, string>? secrets = document.ProfileSecrets[profileId];
            if (secrets == null) {
                document.ProfileSecrets[profileId] = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);
            } else if (secrets.Comparer != StringComparer.OrdinalIgnoreCase) {
                document.ProfileSecrets[profileId] = new Dictionary<string, string>(
                    secrets, StringComparer.OrdinalIgnoreCase);
            }
        }

        if (document.Secrets != null) {
            foreach (KeyValuePair<string, string> legacySecret in document.Secrets) {
                if (!TryParseLegacyKey(legacySecret.Key, out string? profileId, out string? secretName)) {
                    continue;
                }

                Dictionary<string, string> secrets = GetOrCreateProfileSecrets(document, profileId!);
                if (!secrets.ContainsKey(secretName!)) {
                    secrets[secretName!] = legacySecret.Value;
                }
            }
            document.Secrets = null;
        }

        document.Version = 2;
    }

    private static Dictionary<string, string> GetOrCreateProfileSecrets(
        MailSecretStoreDocument document,
        string profileId) {
        string normalizedProfileId = profileId.Trim();
        if (!document.ProfileSecrets.TryGetValue(normalizedProfileId, out Dictionary<string, string>? secrets)) {
            secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            document.ProfileSecrets[normalizedProfileId] = secrets;
        }
        return secrets;
    }

    private static bool TryGetProfileSecrets(
        MailSecretStoreDocument document,
        string profileId,
        out Dictionary<string, string> secrets) {
        if (document.ProfileSecrets.TryGetValue(
            profileId.Trim(), out Dictionary<string, string>? storedSecrets) && storedSecrets != null) {
            secrets = storedSecrets;
            return true;
        }

        secrets = null!;
        return false;
    }

    private static bool TryParseLegacyKey(string key, out string? profileId, out string? secretName) {
        int separatorIndex = key.LastIndexOf("::", StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex + 2 >= key.Length) {
            profileId = null;
            secretName = null;
            return false;
        }

        profileId = key.Substring(0, separatorIndex);
        secretName = key.Substring(separatorIndex + 2);
        return true;
    }

    private static void ValidateKeyPart(string value, string parameterName) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }
    }

}
