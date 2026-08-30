namespace Mailozaurr;

/// <summary>
/// Stores mail profiles in process memory for transient applications, tests, and one-shot delivery workflows.
/// </summary>
public sealed class InMemoryMailProfileStore :
    IMailProfileStore,
    IMailProfileMaintenanceCoordinator,
    IMailProfileStoreCreateCoordinator,
    IMailProfileStoreCredentialContextCoordinator {
    private readonly Dictionary<string, MailProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <inheritdoc />
    public async Task<IReadOnlyList<MailProfile>> GetAllAsync(CancellationToken cancellationToken = default) {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            return MailProfileCloner.CloneAll(
                _profiles.Values.OrderBy(static profile => profile.Id, StringComparer.OrdinalIgnoreCase));
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<MailProfile?> GetByIdAsync(
        string profileId,
        CancellationToken cancellationToken = default) {
        ValidateProfileId(profileId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            return _profiles.TryGetValue(profileId.Trim(), out MailProfile? profile)
                ? MailProfileCloner.Clone(profile)
                : null;
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(MailProfile profile, CancellationToken cancellationToken = default) {
        MailProfileCloner.Validate(profile);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            SaveLocked(profile, allowCredentialContextChange: false);
        } finally {
            _gate.Release();
        }
    }

    async Task<MailProfileCreateOutcome> IMailProfileStoreCreateCoordinator.TryCreateAsync(
        MailProfile profile,
        CancellationToken cancellationToken) {
        MailProfileCloner.Validate(profile);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if (_profiles.ContainsKey(profile.Id.Trim())) return MailProfileCreateOutcome.AlreadyExists;
            SaveLocked(profile, allowCredentialContextChange: false);
            return MailProfileCreateOutcome.Created;
        } finally {
            _gate.Release();
        }
    }

    async Task<MailProfileCredentialContextSaveOutcome>
        IMailProfileStoreCredentialContextCoordinator.SaveCredentialContextChangeAsync(
            MailProfile profile,
            IMailSecretStore secretStore,
            CancellationToken cancellationToken) {
        MailProfileCloner.Validate(profile);
        if (secretStore is not IMailSecretStoreCredentialContextCoordinator secretCoordinator) {
            return MailProfileCredentialContextSaveOutcome.Unsupported;
        }
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            return await secretCoordinator.ExecuteWithProfileSecretsLockedAsync(
                profile.Id,
                (hasSecrets, _) => {
                    if (hasSecrets) {
                        return Task.FromResult(MailProfileCredentialContextSaveOutcome.HasSecrets);
                    }
                    SaveLocked(profile, allowCredentialContextChange: true);
                    return Task.FromResult(MailProfileCredentialContextSaveOutcome.Saved);
                },
                cancellationToken).ConfigureAwait(false);
        } finally {
            _gate.Release();
        }
    }

    private void SaveLocked(MailProfile profile, bool allowCredentialContextChange) {
        MailProfile profileToStore = MailProfileCloner.Clone(profile);
        profileToStore.Id = profileToStore.Id.Trim();
        profileToStore.DisplayName = profileToStore.DisplayName.Trim();
        if (_profiles.TryGetValue(profileToStore.Id, out MailProfile? existingProfile)) {
            MailProfileKindGuard.EnsureUnchanged(existingProfile, profileToStore);
            if (!allowCredentialContextChange) {
                MailProfileCredentialContextGuard.EnsureUnchanged(existingProfile, profileToStore);
            }
        }

        if (profileToStore.IsDefault) {
            foreach (MailProfile existing in _profiles.Values) existing.IsDefault = false;
        }
        _profiles[profileToStore.Id] = profileToStore;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(
        string profileId,
        CancellationToken cancellationToken = default) {
        ValidateProfileId(profileId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            return _profiles.Remove(profileId.Trim());
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteWithStableProfileIdsAsync<TResult>(
        Func<IReadOnlyCollection<string>, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default) {
        if (operation == null) throw new ArgumentNullException(nameof(operation));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            string[] profileIds = _profiles.Keys
                .OrderBy(static profileId => profileId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return await operation(profileIds, cancellationToken).ConfigureAwait(false);
        } finally {
            _gate.Release();
        }
    }

    private static void ValidateProfileId(string profileId) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }
    }
}
