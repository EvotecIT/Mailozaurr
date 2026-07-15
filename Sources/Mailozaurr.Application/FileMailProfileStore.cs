namespace Mailozaurr.Application;

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
        _store.ReadAsync(document => CloneProfiles(document.Profiles), cancellationToken);

    /// <inheritdoc />
    public Task<MailProfile?> GetByIdAsync(string profileId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        return _store.ReadAsync(document => {
            MailProfile? profile = document.Profiles.FirstOrDefault(p =>
                string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase));
            return profile == null ? null : CloneProfile(profile);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveAsync(MailProfile profile, CancellationToken cancellationToken = default) {
        ValidateProfile(profile);

        await _store.UpdateAsync(document => {
            var existingIndex = document.Profiles.FindIndex(p => string.Equals(p.Id, profile.Id, StringComparison.OrdinalIgnoreCase));
            var profileToStore = CloneProfile(profile);

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

    private static void ValidateProfile(MailProfile? profile) {
        if (profile == null) {
            throw new ArgumentNullException(nameof(profile));
        }

        if (string.IsNullOrWhiteSpace(profile.Id)) {
            throw new InvalidOperationException("Profile id is required.");
        }

        if (string.IsNullOrWhiteSpace(profile.DisplayName)) {
            throw new InvalidOperationException("Profile display name is required.");
        }
    }

    private static IReadOnlyList<MailProfile> CloneProfiles(IEnumerable<MailProfile> profiles) =>
        profiles.Select(CloneProfile).ToArray();

    private static MailProfile CloneProfile(MailProfile profile) => new() {
        Id = profile.Id,
        DisplayName = profile.DisplayName,
        Description = profile.Description,
        Kind = profile.Kind,
        DefaultSender = profile.DefaultSender,
        DefaultMailbox = profile.DefaultMailbox,
        IsDefault = profile.IsDefault,
        Settings = new Dictionary<string, string>(profile.Settings, StringComparer.OrdinalIgnoreCase),
        Capabilities = profile.Capabilities == null
            ? null
            : new ProfileCapabilities(profile.Capabilities.Kind, profile.Capabilities.Capabilities)
    };

}