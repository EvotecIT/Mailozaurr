namespace Mailozaurr;

/// <summary>
/// Stores profiles in a JSON document on disk.
/// </summary>
public sealed class FileMailProfileStore : IMailProfileStore, IMailProfileMaintenanceCoordinator {
    private readonly JsonFileDocumentStore<MailProfileStoreDocument> _store;
    /// <summary>
    /// Creates a new store using the provided options.
    /// </summary>
    public FileMailProfileStore(MailProfileStoreOptions? options = null)
        : this((options ?? new MailProfileStoreOptions()).GetFilePath()) {
    }

    /// <summary>
    /// Creates a new store using the specified file path.
    /// </summary>
    public FileMailProfileStore(string filePath) {
        _store = new JsonFileDocumentStore<MailProfileStoreDocument>(
            filePath,
            "Profile store path is invalid.",
            ApplicationJsonContext.Default.MailProfileStoreDocument,
            static () => new MailProfileStoreDocument());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MailProfile>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _store.ReadAsync(document => MailProfileCloner.CloneAll(document.Profiles), cancellationToken);

    /// <inheritdoc />
    public Task<MailProfile?> GetByIdAsync(string profileId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        return _store.ReadAsync(document => {
            MailProfile? profile = document.Profiles.FirstOrDefault(p =>
                string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase));
            return profile == null ? null : MailProfileCloner.Clone(profile);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveAsync(MailProfile profile, CancellationToken cancellationToken = default) {
        MailProfileCloner.Validate(profile);

        await _store.UpdateAsync(document => {
            var existingIndex = document.Profiles.FindIndex(p => string.Equals(p.Id, profile.Id, StringComparison.OrdinalIgnoreCase));
            var profileToStore = MailProfileCloner.Clone(profile);
            if (existingIndex >= 0) {
                MailProfileKindGuard.EnsureUnchanged(document.Profiles[existingIndex], profileToStore);
                MailProfileCredentialContextGuard.EnsureUnchanged(document.Profiles[existingIndex], profileToStore);
            }

            if (profileToStore.IsDefault) {
                foreach (var existing in document.Profiles) {
                    existing.IsDefault = false;
                }
            }

            if (existingIndex >= 0) {
                document.Profiles[existingIndex] = profileToStore;
            } else {
                document.Profiles.Add(profileToStore);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(string profileId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        return _store.RemoveAsync(document => document.Profiles.RemoveAll(p =>
            string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase)) > 0, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult> ExecuteWithStableProfileIdsAsync<TResult>(
        Func<IReadOnlyCollection<string>, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default) {
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        return _store.ExecuteUnderWriterLockAsync(document => {
            string[] profileIds = document.Profiles
                .Where(profile => !string.IsNullOrWhiteSpace(profile.Id))
                .Select(profile => profile.Id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return operation(profileIds, cancellationToken);
        }, cancellationToken);
    }

}
