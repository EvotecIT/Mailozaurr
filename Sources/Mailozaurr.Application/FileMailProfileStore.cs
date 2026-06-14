using System.Text.Json;

namespace Mailozaurr.Application;

/// <summary>
/// Stores profiles in a JSON document on disk.
/// </summary>
public sealed class FileMailProfileStore : IMailProfileStore {
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
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
        _filePath = Path.GetFullPath(filePath ?? throw new ArgumentNullException(nameof(filePath)));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MailProfile>> GetAllAsync(CancellationToken cancellationToken = default) {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            return CloneProfiles(document.Profiles);
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<MailProfile?> GetByIdAsync(string profileId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var profile = document.Profiles.FirstOrDefault(p => string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase));
            return profile == null ? null : CloneProfile(profile);
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(MailProfile profile, CancellationToken cancellationToken = default) {
        ValidateProfile(profile);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
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

            await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(string profileId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var removed = document.Profiles.RemoveAll(p => string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase)) > 0;
            if (!removed) {
                return false;
            }

            await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            return true;
        } finally {
            _gate.Release();
        }
    }

    private async Task<MailProfileStoreDocument> LoadDocumentAsync(CancellationToken cancellationToken) {
        if (!File.Exists(_filePath)) {
            return new MailProfileStoreDocument();
        }

        using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            var document = await JsonSerializer.DeserializeAsync(stream, ApplicationJsonContext.Default.MailProfileStoreDocument, cancellationToken).ConfigureAwait(false);
            return document ?? new MailProfileStoreDocument();
        }
    }

    private async Task SaveDocumentAsync(MailProfileStoreDocument document, CancellationToken cancellationToken) {
        var directory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrWhiteSpace(directory)) {
            throw new InvalidOperationException("Profile store path is invalid.");
        }

        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, Path.GetRandomFileName());
        try {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                await JsonSerializer.SerializeAsync(stream, document, ApplicationJsonContext.Default.MailProfileStoreDocument, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(_filePath)) {
                File.Delete(_filePath);
            }

            File.Move(tempPath, _filePath);
            tempPath = string.Empty;
        } finally {
            if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath)) {
                File.Delete(tempPath);
            }
        }
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