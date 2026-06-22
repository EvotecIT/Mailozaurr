using System.Text.Json;

namespace Mailozaurr.Application;

/// <summary>
/// Stores protected secrets in a JSON document on disk.
/// </summary>
public sealed class FileMailSecretStore : IMailSecretStore {
    private readonly string _filePath;
    private readonly ICredentialProtector _protector;
    private readonly SemaphoreSlim _gate = new(1, 1);
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
        _filePath = Path.GetFullPath(filePath ?? throw new ArgumentNullException(nameof(filePath)));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    /// <inheritdoc />
    public async Task<string?> GetSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        ValidateKeyPart(secretName, nameof(secretName));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            if (!document.Secrets.TryGetValue(CreateKey(profileId, secretName), out var protectedValue)) {
                return null;
            }

            return _protector.Unprotect(protectedValue);
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetSecretAsync(string profileId, string secretName, string secretValue, CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        ValidateKeyPart(secretName, nameof(secretName));
        if (secretValue == null) {
            throw new ArgumentNullException(nameof(secretValue));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            document.Secrets[CreateKey(profileId, secretName)] = _protector.Protect(secretValue);
            await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
        ValidateKeyPart(profileId, nameof(profileId));
        ValidateKeyPart(secretName, nameof(secretName));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var removed = document.Secrets.Remove(CreateKey(profileId, secretName));
            if (!removed) {
                return false;
            }

            await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            return true;
        } finally {
            _gate.Release();
        }
    }

    private async Task<MailSecretStoreDocument> LoadDocumentAsync(CancellationToken cancellationToken) {
        if (!File.Exists(_filePath)) {
            return new MailSecretStoreDocument();
        }

        using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            var document = await JsonSerializer.DeserializeAsync(stream, ApplicationJsonContext.Default.MailSecretStoreDocument, cancellationToken).ConfigureAwait(false);
            return document ?? new MailSecretStoreDocument();
        }
    }

    private Task SaveDocumentAsync(MailSecretStoreDocument document, CancellationToken cancellationToken) =>
        AtomicFileWriter.WriteAsync(
            _filePath,
            "Secret store path is invalid.",
            async (stream, token) => {
                await JsonSerializer.SerializeAsync(stream, document, ApplicationJsonContext.Default.MailSecretStoreDocument, token).ConfigureAwait(false);
            },
            cancellationToken);

    private static string CreateKey(string profileId, string secretName) => $"{profileId.Trim()}::{secretName.Trim()}";

    private static void ValidateKeyPart(string value, string parameterName) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }
    }

}
