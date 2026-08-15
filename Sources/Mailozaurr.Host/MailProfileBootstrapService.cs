namespace Mailozaurr.Hosting;

/// <summary>
/// Default implementation of reusable provider bootstrap workflows.
/// </summary>
public sealed class MailProfileBootstrapService : IMailProfileBootstrapService {
    private static readonly string[] KnownSecretNames = {
        MailSecretNames.Password,
        MailSecretNames.ClientSecret,
        MailSecretNames.AccessToken,
        MailSecretNames.RefreshToken,
        MailSecretNames.CertificatePassword,
        MailSecretNames.ApiKey,
        MailSecretNames.AccessKeyId,
        MailSecretNames.SecretAccessKey
    };
    private readonly IMailProfileService _profiles;
    private readonly IMailProfileSecretService _profileSecrets;
    private readonly IMailSecretStore _secretStore;

    /// <summary>
    /// Creates a new profile bootstrap service.
    /// </summary>
    public MailProfileBootstrapService(IMailProfileService profiles, IMailProfileSecretService profileSecrets, IMailSecretStore secretStore) {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _profileSecrets = profileSecrets ?? throw new ArgumentNullException(nameof(profileSecrets));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    }

    /// <inheritdoc />
    public async Task<OperationResult> SaveGraphProfileAsync(GraphProfileBootstrapRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var profileId = request.ProfileId?.Trim() ?? string.Empty;
        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        var mailbox = request.Mailbox?.Trim() ?? string.Empty;
        if (profileId.Length == 0) {
            return OperationResult.Failure("profile_required", "Profile id is required.");
        }
        if (displayName.Length == 0) {
            return OperationResult.Failure("display_name_required", "Display name is required.");
        }
        if (mailbox.Length == 0) {
            return OperationResult.Failure("mailbox_required", "Mailbox is required.");
        }

        var defaultSender = string.IsNullOrWhiteSpace(request.DefaultSender) ? mailbox : request.DefaultSender!.Trim();
        var existing = await _profiles.GetProfileAsync(profileId, cancellationToken).ConfigureAwait(false);
        string? clientSecret;
        string? accessToken;
        string? certificatePassword;
        try {
            clientSecret = await MailSecretReferenceResolver.ResolveAsync(
                _secretStore,
                profileId,
                MailSecretNames.ClientSecret,
                request.ClientSecret,
                request.ClientSecretReference,
                cancellationToken).ConfigureAwait(false);
            accessToken = await MailSecretReferenceResolver.ResolveAsync(
                _secretStore,
                profileId,
                MailSecretNames.AccessToken,
                request.AccessToken,
                request.AccessTokenReference,
                cancellationToken).ConfigureAwait(false);
            certificatePassword = await MailSecretReferenceResolver.ResolveAsync(
                _secretStore,
                profileId,
                MailSecretNames.CertificatePassword,
                request.CertificatePassword,
                request.CertificatePasswordReference,
                cancellationToken).ConfigureAwait(false);
        } catch (InvalidOperationException ex) {
            return OperationResult.Failure("secret_reference_invalid", ex.Message);
        }

        var effectiveClientId = FirstNonEmpty(request.ClientId, existing?.Settings.TryGetValue(MailProfileSettingsKeys.ClientId, out var existingClientId) == true ? existingClientId : null);
        var effectiveTenantId = FirstNonEmpty(request.TenantId, existing?.Settings.TryGetValue(MailProfileSettingsKeys.TenantId, out var existingTenantId) == true ? existingTenantId : null);
        var effectiveCertificatePath = FirstNonEmpty(request.CertificatePath, existing?.Settings.TryGetValue(MailProfileSettingsKeys.CertificatePath, out var existingCertificatePath) == true ? existingCertificatePath : null);
        var hasAccessToken = !string.IsNullOrWhiteSpace(accessToken) ||
                             await HasStoredSecretAsync(profileId, MailSecretNames.AccessToken, cancellationToken).ConfigureAwait(false);
        var hasClientSecret = !string.IsNullOrWhiteSpace(clientSecret) ||
                              await HasStoredSecretAsync(profileId, MailSecretNames.ClientSecret, cancellationToken).ConfigureAwait(false);
        var hasCertificate = !string.IsNullOrWhiteSpace(effectiveCertificatePath);

        if (!hasAccessToken &&
            (string.IsNullOrWhiteSpace(effectiveClientId) || string.IsNullOrWhiteSpace(effectiveTenantId) || (!hasClientSecret && !hasCertificate))) {
            return OperationResult.Failure(
                "graph_auth_required",
                "Graph profiles require either an access token or a client-id and tenant-id with a client secret or certificate path.");
        }
        if (!string.IsNullOrWhiteSpace(certificatePassword) && !hasCertificate) {
            return OperationResult.Failure("certificate_path_required", "Certificate password requires a certificate path.");
        }

        var profile = existing == null
            ? new MailProfile {
                Id = profileId,
                Kind = MailProfileKind.Graph
            }
            : CloneProfile(existing);

        profile.DisplayName = displayName;
        profile.Description = request.Description ?? profile.Description;
        profile.Kind = MailProfileKind.Graph;
        profile.DefaultMailbox = mailbox;
        profile.DefaultSender = defaultSender;
        profile.IsDefault = request.IsDefault;
        profile.Settings[MailProfileSettingsKeys.Mailbox] = mailbox;

        UpsertSetting(profile.Settings, MailProfileSettingsKeys.ClientId, effectiveClientId);
        UpsertSetting(profile.Settings, MailProfileSettingsKeys.TenantId, effectiveTenantId);
        UpsertSetting(profile.Settings, MailProfileSettingsKeys.CertificatePath, effectiveCertificatePath);

        return await SaveProfileAndSecretsAsync(
            profile,
            existing,
            new[] {
                new SecretUpdate(MailSecretNames.ClientSecret, clientSecret),
                new SecretUpdate(MailSecretNames.AccessToken, accessToken),
                new SecretUpdate(MailSecretNames.CertificatePassword, certificatePassword)
            },
            "Graph profile saved.",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> SaveGmailProfileAsync(GmailProfileBootstrapRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var profileId = request.ProfileId?.Trim() ?? string.Empty;
        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        var mailbox = string.IsNullOrWhiteSpace(request.Mailbox) ? "me" : request.Mailbox!.Trim();
        if (profileId.Length == 0) {
            return OperationResult.Failure("profile_required", "Profile id is required.");
        }
        if (displayName.Length == 0) {
            return OperationResult.Failure("display_name_required", "Display name is required.");
        }

        var existing = await _profiles.GetProfileAsync(profileId, cancellationToken).ConfigureAwait(false);
        string? clientSecret;
        string? refreshToken;
        string? accessToken;
        try {
            clientSecret = await MailSecretReferenceResolver.ResolveAsync(
                _secretStore,
                profileId,
                MailSecretNames.ClientSecret,
                request.ClientSecret,
                request.ClientSecretReference,
                cancellationToken).ConfigureAwait(false);
            refreshToken = await MailSecretReferenceResolver.ResolveAsync(
                _secretStore,
                profileId,
                MailSecretNames.RefreshToken,
                request.RefreshToken,
                request.RefreshTokenReference,
                cancellationToken).ConfigureAwait(false);
            accessToken = await MailSecretReferenceResolver.ResolveAsync(
                _secretStore,
                profileId,
                MailSecretNames.AccessToken,
                request.AccessToken,
                request.AccessTokenReference,
                cancellationToken).ConfigureAwait(false);
        } catch (InvalidOperationException ex) {
            return OperationResult.Failure("secret_reference_invalid", ex.Message);
        }
        var effectiveClientId = FirstNonEmpty(request.ClientId, existing?.Settings.TryGetValue(MailProfileSettingsKeys.ClientId, out var existingClientId) == true ? existingClientId : null);
        var hasAccessToken = !string.IsNullOrWhiteSpace(accessToken) ||
                             await HasStoredSecretAsync(profileId, MailSecretNames.AccessToken, cancellationToken).ConfigureAwait(false);
        var hasRefreshToken = !string.IsNullOrWhiteSpace(refreshToken) ||
                              await HasStoredSecretAsync(profileId, MailSecretNames.RefreshToken, cancellationToken).ConfigureAwait(false);
        var hasClientSecret = !string.IsNullOrWhiteSpace(clientSecret) ||
                              await HasStoredSecretAsync(profileId, MailSecretNames.ClientSecret, cancellationToken).ConfigureAwait(false);

        if (!hasAccessToken && (!hasRefreshToken || string.IsNullOrWhiteSpace(effectiveClientId) || !hasClientSecret)) {
            return OperationResult.Failure(
                "gmail_auth_required",
                "Gmail profiles require either an access token or a refresh token with a client id and client secret.");
        }

        var defaultSender = string.IsNullOrWhiteSpace(request.DefaultSender)
            ? (string.Equals(mailbox, "me", StringComparison.OrdinalIgnoreCase) ? existing?.DefaultSender : mailbox)
            : request.DefaultSender!.Trim();

        var profile = existing == null
            ? new MailProfile {
                Id = profileId,
                Kind = MailProfileKind.Gmail
            }
            : CloneProfile(existing);

        profile.DisplayName = displayName;
        profile.Description = request.Description ?? profile.Description;
        profile.Kind = MailProfileKind.Gmail;
        profile.DefaultMailbox = mailbox;
        profile.DefaultSender = defaultSender;
        profile.IsDefault = request.IsDefault;
        profile.Settings[MailProfileSettingsKeys.Mailbox] = mailbox;

        UpsertSetting(profile.Settings, MailProfileSettingsKeys.ClientId, effectiveClientId);

        return await SaveProfileAndSecretsAsync(
            profile,
            existing,
            new[] {
                new SecretUpdate(MailSecretNames.ClientSecret, clientSecret),
                new SecretUpdate(MailSecretNames.RefreshToken, refreshToken),
                new SecretUpdate(MailSecretNames.AccessToken, accessToken)
            },
            "Gmail profile saved.",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<OperationResult> SaveProfileAndSecretsAsync(
        MailProfile profile,
        MailProfile? existing,
        IReadOnlyList<SecretUpdate> updates,
        string successMessage,
        CancellationToken cancellationToken) {
        SecretUpdate[] effectiveUpdates = updates
            .Where(update => !string.IsNullOrWhiteSpace(update.Value))
            .ToArray();
        MailSecretRollbackSnapshot previousSecrets = await MailSecretRollbackSnapshot.CaptureAsync(
            _secretStore,
            profile.Id,
            KnownSecretNames,
            effectiveUpdates.Select(update => update.Name).ToArray(),
            cancellationToken).ConfigureAwait(false);
        string? previousDefaultProfileId = await CapturePreviousDefaultProfileIdAsync(
            profile, cancellationToken).ConfigureAwait(false);

        OperationResult saveResult = await _profiles.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        if (!saveResult.Succeeded) {
            return saveResult;
        }

        foreach (SecretUpdate update in effectiveUpdates) {
            OperationResult secretResult;
            try {
                secretResult = await _profileSecrets.SetSecretAsync(
                    profile.Id,
                    update.Name,
                    update.Value!.Trim(),
                    cancellationToken).ConfigureAwait(false);
            } catch (Exception bootstrapException) {
                try {
                    await RollBackBootstrapAsync(
                        profile.Id, existing, previousDefaultProfileId, previousSecrets).ConfigureAwait(false);
                } catch (Exception rollbackException) {
                    throw new InvalidOperationException(
                        "Profile bootstrap failed and its rollback was incomplete.",
                        new AggregateException(bootstrapException, rollbackException));
                }
                throw;
            }

            if (!secretResult.Succeeded) {
                try {
                    await RollBackBootstrapAsync(
                        profile.Id, existing, previousDefaultProfileId, previousSecrets).ConfigureAwait(false);
                } catch (Exception rollbackException) {
                    throw new InvalidOperationException(
                        "Profile bootstrap was rejected and its rollback was incomplete.",
                        rollbackException);
                }
                return secretResult;
            }
        }

        return OperationResult.Success(successMessage);
    }

    private async Task RollBackBootstrapAsync(
        string profileId,
        MailProfile? existing,
        string? previousDefaultProfileId,
        MailSecretRollbackSnapshot previousSecrets) {
        if (existing == null) {
            OperationResult deleteResult = await _profiles.DeleteAsync(
                profileId, CancellationToken.None).ConfigureAwait(false);
            if (!deleteResult.Succeeded) {
                throw new InvalidOperationException(deleteResult.Message ?? "The new profile could not be rolled back.");
            }
        } else {
            OperationResult restoreProfile = await _profiles.SaveAsync(existing, CancellationToken.None).ConfigureAwait(false);
            if (!restoreProfile.Succeeded) {
                throw new InvalidOperationException(restoreProfile.Message ?? "The previous profile could not be restored.");
            }
        }

        if (!string.IsNullOrWhiteSpace(previousDefaultProfileId)) {
            OperationResult restoreDefault = await _profiles.SetDefaultAsync(
                previousDefaultProfileId!, CancellationToken.None).ConfigureAwait(false);
            if (!restoreDefault.Succeeded) {
                throw new InvalidOperationException(
                    restoreDefault.Message ?? "The previous default profile could not be restored.");
            }
        }

        await previousSecrets.RestoreAsync(
            _secretStore,
            profileId,
            CancellationToken.None).ConfigureAwait(false);
    }

    private async Task<string?> CapturePreviousDefaultProfileIdAsync(
        MailProfile profile,
        CancellationToken cancellationToken) {
        if (!profile.IsDefault) {
            return null;
        }

        IReadOnlyList<MailProfile> profiles = await _profiles.GetProfilesAsync(cancellationToken)
            .ConfigureAwait(false);
        return profiles.FirstOrDefault(candidate =>
            candidate.IsDefault &&
            !string.Equals(candidate.Id, profile.Id, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private sealed class SecretUpdate {
        internal SecretUpdate(string name, string? value) {
            Name = name;
            Value = value;
        }

        internal string Name { get; }
        internal string? Value { get; }
    }

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

    private static void UpsertSetting(IDictionary<string, string> settings, string key, string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            settings.Remove(key);
            return;
        }

        var normalizedValue = value!.Trim();
        settings[key] = normalizedValue;
    }

    private static string? FirstNonEmpty(params string?[] values) {
        foreach (var value in values) {
            if (!string.IsNullOrWhiteSpace(value)) {
                var normalizedValue = value!.Trim();
                return normalizedValue;
            }
        }

        return null;
    }

    private async Task<bool> HasStoredSecretAsync(string profileId, string secretName, CancellationToken cancellationToken) =>
        !string.IsNullOrWhiteSpace(await _secretStore.GetSecretAsync(profileId, secretName, cancellationToken).ConfigureAwait(false));
}
