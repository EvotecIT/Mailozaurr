namespace Mailozaurr.Application;

/// <summary>
/// Default implementation of profile lifecycle operations.
/// </summary>
public sealed class MailProfileService : IMailProfileService {
    private static readonly string[] KnownSecretNames = {
        MailSecretNames.Password,
        MailSecretNames.ClientSecret,
        MailSecretNames.AccessToken,
        MailSecretNames.RefreshToken,
        MailSecretNames.CertificatePassword
    };
    private readonly IMailProfileStore _profileStore;
    private readonly IMailSecretStore? _secretStore;
    private readonly IReadOnlyDictionary<MailProfileKind, MailCapability>? _availableCapabilities;

    /// <summary>
    /// Creates a new profile service.
    /// </summary>
    public MailProfileService(IMailProfileStore profileStore, IMailSecretStore? secretStore = null,
        IReadOnlyDictionary<MailProfileKind, MailCapability>? availableCapabilities = null) {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        _secretStore = secretStore;
        _availableCapabilities = availableCapabilities;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MailProfile>> GetProfilesAsync(CancellationToken cancellationToken = default) =>
        _profileStore.GetAllAsync(cancellationToken);

    /// <inheritdoc />
    public Task<MailProfile?> GetProfileAsync(string profileId, CancellationToken cancellationToken = default) =>
        _profileStore.GetByIdAsync(profileId, cancellationToken);

    /// <inheritdoc />
    public Task<MailProfileValidationResult> ValidateAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
        Task.FromResult(MailProfileValidator.Validate(profile));

    /// <inheritdoc />
    public async Task<MailProfileValidationResult> DiagnoseAsync(string profileId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            return CreateFailure("profile_required", "Profile id is required.");
        }

        var profile = await _profileStore.GetByIdAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (profile == null) {
            return CreateFailure("profile_not_found", "Profile was not found.");
        }

        var result = MailProfileValidator.Validate(profile);
        await AddProviderReadinessChecksAsync(profile, result, cancellationToken).ConfigureAwait(false);
        result.Succeeded = result.Errors.Count == 0;
        result.Code = result.Succeeded ? null : "profile_not_ready";
        result.Message = result.Succeeded
            ? (result.Warnings.Count == 0 ? "Profile is ready." : "Profile is ready with warnings.")
            : result.Errors[0];
        return result;
    }

    /// <inheritdoc />
    public async Task<OperationResult> SaveAsync(MailProfile profile, CancellationToken cancellationToken = default) {
        var validation = MailProfileValidator.Validate(profile);
        if (!validation.Succeeded) {
            return validation;
        }

        await _profileStore.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success("Profile saved.");
    }

    /// <inheritdoc />
    public async Task<OperationResult> DeleteAsync(string profileId, CancellationToken cancellationToken = default) {
        var profile = await _profileStore.GetByIdAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (profile == null) {
            return OperationResult.Failure("profile_not_found", "Profile was not found.");
        }

        MailSecretRollbackSnapshot? secretSnapshot = _secretStore == null
            ? null
            : await MailSecretRollbackSnapshot.CaptureAsync(
                _secretStore,
                profileId,
                KnownSecretNames,
                requiredSecretNames: null,
                cancellationToken).ConfigureAwait(false);
        IReadOnlyCollection<string>? knownProfileIds = null;
        if (_secretStore is IMailProfileSecretContextCleanup) {
            IReadOnlyList<MailProfile> knownProfiles = await _profileStore.GetAllAsync(cancellationToken)
                .ConfigureAwait(false);
            knownProfileIds = knownProfiles.Select(candidate => candidate.Id).ToArray();
        }

        var removed = await _profileStore.RemoveAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (!removed) {
            return OperationResult.Failure("profile_not_found", "Profile was not found.");
        }

        try {
            if (_secretStore is IMailProfileSecretContextCleanup contextCleanup) {
                await contextCleanup.RemoveProfileSecretsAsync(
                    profileId,
                    knownProfileIds!,
                    cancellationToken).ConfigureAwait(false);
            } else if (_secretStore is IMailProfileSecretCleanup cleanup) {
                await cleanup.RemoveProfileSecretsAsync(profileId, cancellationToken).ConfigureAwait(false);
            } else if (_secretStore != null) {
                await RemoveKnownSecretsAsync(profileId, cancellationToken).ConfigureAwait(false);
            }
        } catch (Exception deleteException) {
            try {
                await _profileStore.SaveAsync(profile, CancellationToken.None).ConfigureAwait(false);
                if (secretSnapshot != null) {
                    await secretSnapshot.RestoreAsync(
                        _secretStore!,
                        profileId,
                        CancellationToken.None).ConfigureAwait(false);
                }
            } catch (Exception rollbackException) {
                throw new InvalidOperationException(
                    "Profile deletion failed and its rollback was incomplete.",
                    new AggregateException(deleteException, rollbackException));
            }
            throw;
        }

        return OperationResult.Success("Profile deleted.");
    }

    /// <inheritdoc />
    public async Task<OperationResult> SetDefaultAsync(string profileId, CancellationToken cancellationToken = default) {
        var profiles = await _profileStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var profile = profiles.FirstOrDefault(p => string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase));
        if (profile == null) {
            return OperationResult.Failure("profile_not_found", "Profile was not found.");
        }

        profile.IsDefault = true;
        await _profileStore.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success("Default profile updated.");
    }

    /// <inheritdoc />
    public async Task<ProfileCapabilities?> GetCapabilitiesAsync(string profileId, CancellationToken cancellationToken = default) {
        var profile = await _profileStore.GetByIdAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (profile == null) {
            return null;
        }

        if (_availableCapabilities == null) {
            return profile.GetCapabilities();
        }

        _availableCapabilities.TryGetValue(profile.Kind, out MailCapability available);
        return profile.GetCapabilities(available);
    }

    private async Task AddProviderReadinessChecksAsync(MailProfile profile, MailProfileValidationResult result, CancellationToken cancellationToken) {
        if (_secretStore == null) {
            result.Warnings.Add("Secret store is unavailable, so authentication readiness could not be fully verified.");
            return;
        }

        switch (profile.Kind) {
            case MailProfileKind.Graph:
                var hasGraphAccessToken = await HasSecretAsync(profile.Id, MailSecretNames.AccessToken, cancellationToken).ConfigureAwait(false);
                var hasGraphClientSecret = await HasSecretAsync(profile.Id, MailSecretNames.ClientSecret, cancellationToken).ConfigureAwait(false);
                var hasGraphClientId = HasSetting(profile, MailProfileSettingsKeys.ClientId);
                var hasGraphTenantId = HasSetting(profile, MailProfileSettingsKeys.TenantId);
                var hasGraphCertificate = HasSetting(profile, MailProfileSettingsKeys.CertificatePath);
                if (!hasGraphAccessToken && (!hasGraphClientId || !hasGraphTenantId || (!hasGraphClientSecret && !hasGraphCertificate))) {
                    result.Errors.Add("Graph profiles need an access token or a client id and tenant id with a client secret or certificate path.");
                }
                break;
            case MailProfileKind.Gmail:
                var hasGmailAccessToken = await HasSecretAsync(profile.Id, MailSecretNames.AccessToken, cancellationToken).ConfigureAwait(false);
                var hasGmailRefreshToken = await HasSecretAsync(profile.Id, MailSecretNames.RefreshToken, cancellationToken).ConfigureAwait(false);
                var hasGmailClientSecret = await HasSecretAsync(profile.Id, MailSecretNames.ClientSecret, cancellationToken).ConfigureAwait(false);
                var hasGmailClientId = HasSetting(profile, MailProfileSettingsKeys.ClientId);
                if (!hasGmailAccessToken && (!hasGmailRefreshToken || !hasGmailClientId || !hasGmailClientSecret)) {
                    result.Errors.Add("Gmail profiles need an access token or a refresh token with a client id and client secret.");
                }
                break;
        }
    }

    private async Task RemoveKnownSecretsAsync(string profileId, CancellationToken cancellationToken) {
        foreach (string secretName in KnownSecretNames) {
            await _secretStore!.RemoveSecretAsync(profileId, secretName, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> HasSecretAsync(string profileId, string secretName, CancellationToken cancellationToken) =>
        !string.IsNullOrWhiteSpace(await _secretStore!.GetSecretAsync(profileId, secretName, cancellationToken).ConfigureAwait(false));

    private static bool HasSetting(MailProfile profile, string key) =>
        profile.Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);

    private static MailProfileValidationResult CreateFailure(string code, string message) {
        var result = new MailProfileValidationResult {
            Succeeded = false,
            Code = code,
            Message = message
        };
        result.Errors.Add(message);
        return result;
    }
}
