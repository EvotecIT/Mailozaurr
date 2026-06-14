namespace Mailozaurr.Application;

/// <summary>
/// Default implementation of reusable provider bootstrap workflows.
/// </summary>
public sealed class MailProfileBootstrapService : IMailProfileBootstrapService {
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

        var profileId = request.ProfileId.Trim();
        var displayName = request.DisplayName.Trim();
        var mailbox = request.Mailbox.Trim();
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

        if (profileId.Length == 0) {
            return OperationResult.Failure("profile_required", "Profile id is required.");
        }
        if (displayName.Length == 0) {
            return OperationResult.Failure("display_name_required", "Display name is required.");
        }
        if (mailbox.Length == 0) {
            return OperationResult.Failure("mailbox_required", "Mailbox is required.");
        }
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

        var saveResult = await _profiles.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        if (!saveResult.Succeeded) {
            return saveResult;
        }

        if (!string.IsNullOrWhiteSpace(clientSecret)) {
            var clientSecretResult = await _profileSecrets.SetSecretAsync(profile.Id, MailSecretNames.ClientSecret, clientSecret!.Trim(), cancellationToken).ConfigureAwait(false);
            if (!clientSecretResult.Succeeded) {
                return clientSecretResult;
            }
        }
        if (!string.IsNullOrWhiteSpace(accessToken)) {
            var accessTokenResult = await _profileSecrets.SetSecretAsync(profile.Id, MailSecretNames.AccessToken, accessToken!.Trim(), cancellationToken).ConfigureAwait(false);
            if (!accessTokenResult.Succeeded) {
                return accessTokenResult;
            }
        }
        if (!string.IsNullOrWhiteSpace(certificatePassword)) {
            var certificatePasswordResult = await _profileSecrets.SetSecretAsync(profile.Id, MailSecretNames.CertificatePassword, certificatePassword!.Trim(), cancellationToken).ConfigureAwait(false);
            if (!certificatePasswordResult.Succeeded) {
                return certificatePasswordResult;
            }
        }

        return OperationResult.Success("Graph profile saved.");
    }

    /// <inheritdoc />
    public async Task<OperationResult> SaveGmailProfileAsync(GmailProfileBootstrapRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var profileId = request.ProfileId.Trim();
        var displayName = request.DisplayName.Trim();
        var mailbox = string.IsNullOrWhiteSpace(request.Mailbox) ? "me" : request.Mailbox!.Trim();
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

        if (profileId.Length == 0) {
            return OperationResult.Failure("profile_required", "Profile id is required.");
        }
        if (displayName.Length == 0) {
            return OperationResult.Failure("display_name_required", "Display name is required.");
        }
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

        var saveResult = await _profiles.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        if (!saveResult.Succeeded) {
            return saveResult;
        }

        if (!string.IsNullOrWhiteSpace(clientSecret)) {
            var clientSecretResult = await _profileSecrets.SetSecretAsync(profile.Id, MailSecretNames.ClientSecret, clientSecret!.Trim(), cancellationToken).ConfigureAwait(false);
            if (!clientSecretResult.Succeeded) {
                return clientSecretResult;
            }
        }
        if (!string.IsNullOrWhiteSpace(refreshToken)) {
            var refreshTokenResult = await _profileSecrets.SetSecretAsync(profile.Id, MailSecretNames.RefreshToken, refreshToken!.Trim(), cancellationToken).ConfigureAwait(false);
            if (!refreshTokenResult.Succeeded) {
                return refreshTokenResult;
            }
        }
        if (!string.IsNullOrWhiteSpace(accessToken)) {
            var accessTokenResult = await _profileSecrets.SetSecretAsync(profile.Id, MailSecretNames.AccessToken, accessToken!.Trim(), cancellationToken).ConfigureAwait(false);
            if (!accessTokenResult.Succeeded) {
                return accessTokenResult;
            }
        }

        return OperationResult.Success("Gmail profile saved.");
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