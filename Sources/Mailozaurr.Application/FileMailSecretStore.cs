namespace Mailozaurr.Application;

/// <summary>
/// Stores protected secrets in a JSON document on disk.
/// </summary>
public sealed class FileMailSecretStore :
    IMailSecretStore,
    IMailProfileSecretContextCleanup,
    IMailProfileSecretSnapshotStore {
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
            string normalizedProfileId = profileId.Trim();
            string normalizedSecretName = secretName.Trim();
            string? protectedValue = null;
            if (TryGetProfileSecrets(document, normalizedProfileId, out Dictionary<string, string> secrets)) {
                secrets.TryGetValue(normalizedSecretName, out protectedValue);
            }
            if (protectedValue == null && document.Secrets != null) {
                document.Secrets.TryGetValue(CreateLegacyKey(normalizedProfileId, normalizedSecretName), out protectedValue);
            }
            if (protectedValue == null) {
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
            string normalizedProfileId = profileId.Trim();
            string normalizedSecretName = secretName.Trim();
            Dictionary<string, string> secrets = GetOrCreateProfileSecrets(document, normalizedProfileId);
            secrets[normalizedSecretName] = _protector.Protect(secretValue);
            document.Secrets?.Remove(CreateLegacyKey(normalizedProfileId, normalizedSecretName));
            RemoveEmptyLegacyStore(document);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        ValidateKeyPart(secretName, nameof(secretName));

        return _store.RemoveAsync(document => {
            string normalizedProfileId = profileId.Trim();
            string normalizedSecretName = secretName.Trim();
            bool removed = false;
            if (TryGetProfileSecrets(document, normalizedProfileId, out Dictionary<string, string> secrets)) {
                removed = secrets.Remove(normalizedSecretName);
                if (secrets.Count == 0) {
                    document.ProfileSecrets.Remove(normalizedProfileId);
                }
            }

            removed = (document.Secrets?.Remove(CreateLegacyKey(normalizedProfileId, normalizedSecretName)) ?? false) || removed;
            RemoveEmptyLegacyStore(document);
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
            string legacyPrefix = string.Concat(profileId.Trim(), "::");
            if (document.Secrets != null) {
                foreach (KeyValuePair<string, string> secret in document.Secrets) {
                    if (!secret.Key.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    string secretName = secret.Key.Substring(legacyPrefix.Length);
                    if (!result.ContainsKey(secretName)) {
                        result[secretName] = _protector.Unprotect(secret.Value);
                    }
                }
            }
            return result;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveProfileSecretsAsync(string profileId, CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        return RemoveProfileSecretsAsync(profileId, new[] { profileId }, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveProfileSecretsAsync(
        string profileId,
        IReadOnlyCollection<string> knownProfileIds,
        CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        if (knownProfileIds == null) throw new ArgumentNullException(nameof(knownProfileIds));

        string normalizedProfileId = profileId.Trim();
        string[] normalizedKnownProfileIds = knownProfileIds
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => candidate.Trim())
            .Append(normalizedProfileId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return _store.UpdateAsync(document => {
            document.ProfileSecrets.Remove(normalizedProfileId);
            if (document.Secrets == null) return;

            foreach (string legacyKey in document.Secrets.Keys.ToArray()) {
                string? ownerProfileId = ResolveLegacyOwner(legacyKey, normalizedKnownProfileIds);
                if (string.Equals(ownerProfileId, normalizedProfileId, StringComparison.OrdinalIgnoreCase)) {
                    document.Secrets.Remove(legacyKey);
                }
            }
            RemoveEmptyLegacyStore(document);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IMailProfileSecretSnapshot> CaptureProfileSecretsAsync(
        string profileId,
        CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        return _store.ReadAsync<IMailProfileSecretSnapshot>(document => {
            var protectedSecrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var legacySecrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (TryGetProfileSecrets(document, profileId, out Dictionary<string, string> secrets)) {
                foreach (KeyValuePair<string, string> secret in secrets) {
                    protectedSecrets[secret.Key] = secret.Value;
                }
            }
            string legacyPrefix = string.Concat(profileId.Trim(), "::");
            if (document.Secrets != null) {
                foreach (KeyValuePair<string, string> secret in document.Secrets) {
                    if (secret.Key.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase)) {
                        legacySecrets[secret.Key] = secret.Value;
                    }
                }
            }
            return new FileMailProfileSecretSnapshot(protectedSecrets, legacySecrets);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task RestoreProfileSecretsAsync(
        string profileId,
        IMailProfileSecretSnapshot snapshot,
        CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        if (snapshot is not FileMailProfileSecretSnapshot fileSnapshot) {
            throw new ArgumentException("The snapshot was not created by this secret store.", nameof(snapshot));
        }

        return _store.UpdateAsync(document => {
            string normalizedProfileId = profileId.Trim();
            if (fileSnapshot.ProtectedSecrets.Count == 0) {
                document.ProfileSecrets.Remove(normalizedProfileId);
                RestoreLegacySecrets(document, fileSnapshot.LegacySecrets);
                return;
            }

            var restoredSecrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> secret in fileSnapshot.ProtectedSecrets) {
                restoredSecrets[secret.Key] = secret.Value;
            }
            document.ProfileSecrets[normalizedProfileId] = restoredSecrets;

            RestoreLegacySecrets(document, fileSnapshot.LegacySecrets);
        }, cancellationToken);
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
            document.Secrets = new Dictionary<string, string>(
                document.Secrets,
                StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> legacySecret in document.Secrets.ToArray()) {
                if (!TryParseUnambiguousLegacyKey(legacySecret.Key, out string? profileId, out string? secretName)) {
                    continue;
                }

                Dictionary<string, string> secrets = GetOrCreateProfileSecrets(document, profileId!);
                if (!secrets.ContainsKey(secretName!)) {
                    secrets[secretName!] = legacySecret.Value;
                }
                document.Secrets.Remove(legacySecret.Key);
            }
            RemoveEmptyLegacyStore(document);
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

    private static bool TryParseUnambiguousLegacyKey(string key, out string? profileId, out string? secretName) {
        int separatorIndex = key.IndexOf("::", StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex + 2 >= key.Length) {
            profileId = null;
            secretName = null;
            return false;
        }
        if (key.IndexOf("::", separatorIndex + 2, StringComparison.Ordinal) >= 0) {
            profileId = null;
            secretName = null;
            return false;
        }

        profileId = key.Substring(0, separatorIndex);
        secretName = key.Substring(separatorIndex + 2);
        return true;
    }

    private static string? ResolveLegacyOwner(string key, IReadOnlyCollection<string> knownProfileIds) {
        string? owner = null;
        foreach (string profileId in knownProfileIds) {
            string prefix = string.Concat(profileId, "::");
            if (key.Length <= prefix.Length ||
                !key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }
            if (owner == null || profileId.Length > owner.Length) {
                owner = profileId;
            }
        }
        return owner;
    }

    private static string CreateLegacyKey(string profileId, string secretName) => $"{profileId}::{secretName}";

    private static void RemoveEmptyLegacyStore(MailSecretStoreDocument document) {
        if (document.Secrets?.Count == 0) {
            document.Secrets = null;
        }
    }

    private static void RestoreLegacySecrets(
        MailSecretStoreDocument document,
        IReadOnlyDictionary<string, string> legacySecrets) {
        if (legacySecrets.Count == 0) return;
        document.Secrets ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> secret in legacySecrets) {
            document.Secrets[secret.Key] = secret.Value;
        }
    }

    private static void ValidateKeyPart(string value, string parameterName) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }
    }

    private sealed class FileMailProfileSecretSnapshot : IMailProfileSecretSnapshot {
        internal FileMailProfileSecretSnapshot(
            IReadOnlyDictionary<string, string> protectedSecrets,
            IReadOnlyDictionary<string, string> legacySecrets) {
            ProtectedSecrets = protectedSecrets;
            LegacySecrets = legacySecrets;
        }

        internal IReadOnlyDictionary<string, string> ProtectedSecrets { get; }
        internal IReadOnlyDictionary<string, string> LegacySecrets { get; }
    }

}
