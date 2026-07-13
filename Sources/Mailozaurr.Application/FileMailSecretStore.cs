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
            if (!document.Secrets.TryGetValue(CreateKey(profileId, secretName), out var protectedValue)) {
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
            document.Secrets[CreateKey(profileId, secretName)] = _protector.Protect(secretValue);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        ValidateKeyPart(secretName, nameof(secretName));

        return _store.RemoveAsync(document =>
            document.Secrets.Remove(CreateKey(profileId, secretName)), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, string>> GetProfileSecretsAsync(
        string profileId,
        CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        string prefix = profileId.Trim() + "::";
        return _store.ReadAsync<IReadOnlyDictionary<string, string>>(document => {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> secret in document.Secrets) {
                if (secret.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                    result[secret.Key.Substring(prefix.Length)] = _protector.Unprotect(secret.Value);
                }
            }
            return result;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveProfileSecretsAsync(string profileId, CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        string prefix = profileId.Trim() + "::";
        return _store.UpdateAsync(document => {
            string[] keys = document.Secrets.Keys
                .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            foreach (string key in keys) {
                document.Secrets.Remove(key);
            }
        }, cancellationToken);
    }

    private static void NormalizeDocument(MailSecretStoreDocument document) {
        if (document.Secrets.Comparer != StringComparer.OrdinalIgnoreCase) {
            document.Secrets = new Dictionary<string, string>(
                document.Secrets, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string CreateKey(string profileId, string secretName) => $"{profileId.Trim()}::{secretName.Trim()}";

    private static void ValidateKeyPart(string value, string parameterName) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }
    }

}
