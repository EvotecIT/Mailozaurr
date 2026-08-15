namespace Mailozaurr.Hosting;

/// <summary>
/// Default implementation of reusable profile authentication workflows.
/// </summary>
public sealed class MailProfileAuthService : IMailProfileAuthService {
    private readonly IMailProfileService _profiles;
    private readonly IMailProfileSecretService _profileSecrets;
    private readonly IMailSecretStore _secretStore;
    private readonly Func<GmailProfileLoginRequest, CancellationToken, Task<OAuthCredential>> _loginGmailAsync;
    private readonly Func<GraphProfileLoginRequest, CancellationToken, Task<OAuthCredential>> _loginGraphAsync;

    /// <summary>
    /// Creates a new profile authentication service.
    /// </summary>
    public MailProfileAuthService(
        IMailProfileService profiles,
        IMailProfileSecretService profileSecrets,
        IMailSecretStore secretStore,
        Func<GmailProfileLoginRequest, CancellationToken, Task<OAuthCredential>>? loginGmailAsync = null,
        Func<GraphProfileLoginRequest, CancellationToken, Task<OAuthCredential>>? loginGraphAsync = null) {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _profileSecrets = profileSecrets ?? throw new ArgumentNullException(nameof(profileSecrets));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _loginGmailAsync = loginGmailAsync ?? DefaultLoginGmailAsync;
        _loginGraphAsync = loginGraphAsync ?? DefaultLoginGraphAsync;
    }

    /// <inheritdoc />
    public async Task<MailProfileAuthStatus?> GetStatusAsync(string profileId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        var profile = await _profiles.GetProfileAsync(profileId.Trim(), cancellationToken).ConfigureAwait(false);
        if (profile == null) {
            return null;
        }

        profile.Settings.TryGetValue(MailProfileSettingsKeys.AuthFlow, out var authFlow);
        profile.Settings.TryGetValue(MailProfileSettingsKeys.LoginHint, out var loginHint);
        profile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out var mailboxSetting);
        profile.Settings.TryGetValue(MailProfileSettingsKeys.ClientId, out var clientId);
        profile.Settings.TryGetValue(MailProfileSettingsKeys.TenantId, out var tenantId);
        profile.Settings.TryGetValue(MailProfileSettingsKeys.CertificatePath, out var certificatePath);
        profile.Settings.TryGetValue(MailProfileSettingsKeys.AuthMode, out var authMode);

        var accessToken = await TryReadSecretAsync(profile.Id, MailSecretNames.AccessToken, cancellationToken).ConfigureAwait(false);
        var refreshToken = await TryReadSecretAsync(profile.Id, MailSecretNames.RefreshToken, cancellationToken).ConfigureAwait(false);
        var clientSecret = await TryReadSecretAsync(profile.Id, MailSecretNames.ClientSecret, cancellationToken).ConfigureAwait(false);
        var certificatePassword = await TryReadSecretAsync(profile.Id, MailSecretNames.CertificatePassword, cancellationToken).ConfigureAwait(false);
        var password = await TryReadSecretAsync(profile.Id, MailSecretNames.Password, cancellationToken).ConfigureAwait(false);

        var tokenExpiresOn = TryParseTimestamp(profile.Settings.TryGetValue(MailProfileSettingsKeys.TokenExpiresOn, out var rawTokenExpiresOn) ? rawTokenExpiresOn : null);
        var mailbox = FirstNonEmpty(mailboxSetting, profile.DefaultMailbox, profile.DefaultSender);
        var hasAccessToken = !string.IsNullOrWhiteSpace(accessToken);
        var hasRefreshToken = !string.IsNullOrWhiteSpace(refreshToken);
        var hasClientSecret = !string.IsNullOrWhiteSpace(clientSecret);
        var hasCertificatePath = !string.IsNullOrWhiteSpace(certificatePath);
        var hasCertificatePassword = !string.IsNullOrWhiteSpace(certificatePassword);
        var hasPassword = !string.IsNullOrWhiteSpace(password);
        var hasClientId = !string.IsNullOrWhiteSpace(clientId);
        var hasTenantId = !string.IsNullOrWhiteSpace(tenantId);
        var isTokenExpired = tokenExpiresOn.HasValue && tokenExpiresOn.Value <= DateTimeOffset.UtcNow;

        var mode = DetermineMode(profile, authFlow, authMode, hasAccessToken, hasRefreshToken, hasClientSecret, hasCertificatePath, hasPassword);
        var canLoginInteractively = DetermineCanLoginInteractively(profile.Kind, hasClientId, hasTenantId, hasClientSecret, mailbox);
        var canRefresh = DetermineCanRefresh(profile.Kind, authFlow, hasClientId, hasTenantId, hasClientSecret, mailbox);

        return new MailProfileAuthStatus {
            ProfileId = profile.Id,
            ProfileKind = profile.Kind,
            AuthFlow = authFlow,
            Mode = mode,
            LoginHint = loginHint,
            Mailbox = mailbox,
            TokenExpiresOn = tokenExpiresOn,
            HasAccessToken = hasAccessToken,
            HasRefreshToken = hasRefreshToken,
            HasClientSecret = hasClientSecret,
            HasCertificatePath = hasCertificatePath,
            HasCertificatePassword = hasCertificatePassword,
            HasPassword = hasPassword,
            HasClientId = hasClientId,
            HasTenantId = hasTenantId,
            IsTokenExpired = isTokenExpired,
            CanRefresh = canRefresh,
            CanLoginInteractively = canLoginInteractively,
            Summary = BuildSummary(profile, mode, mailbox, hasAccessToken, tokenExpiresOn, isTokenExpired, canRefresh)
        };
    }

    /// <inheritdoc />
    public async Task<MailProfileAuthenticationResult> LoginGmailAsync(GmailProfileLoginRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var profileResult = await LoadProfileAsync(request.ProfileId, MailProfileKind.Gmail, cancellationToken).ConfigureAwait(false);
        if (profileResult.Error != null) {
            return profileResult.Error;
        }

        var profile = profileResult.Profile!;
        string? requestClientSecret;
        try {
            requestClientSecret = await MailSecretReferenceResolver.ResolveAsync(
                _secretStore,
                profile.Id,
                MailSecretNames.ClientSecret,
                request.ClientSecret,
                request.ClientSecretReference,
                cancellationToken).ConfigureAwait(false);
        } catch (InvalidOperationException ex) {
            return Failure("secret_reference_invalid", ex.Message, profile.Id, MailProfileKind.Gmail);
        }
        var gmailAccount = FirstNonEmpty(
            request.GmailAccount,
            profile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out var mailboxSetting) ? mailboxSetting : null,
            profile.DefaultMailbox);
        var clientId = FirstNonEmpty(
            request.ClientId,
            profile.Settings.TryGetValue(MailProfileSettingsKeys.ClientId, out var clientIdSetting) ? clientIdSetting : null);
        var clientSecret = FirstNonEmpty(
            requestClientSecret,
            await TryReadSecretAsync(profile.Id, MailSecretNames.ClientSecret, cancellationToken).ConfigureAwait(false));

        if (string.IsNullOrWhiteSpace(gmailAccount)) {
            return Failure("gmail_account_required", "Gmail login requires an account address.", profile.Id, MailProfileKind.Gmail);
        }
        if (string.IsNullOrWhiteSpace(clientId)) {
            return Failure("client_id_required", "Gmail login requires a client id.", profile.Id, MailProfileKind.Gmail);
        }
        if (string.IsNullOrWhiteSpace(clientSecret)) {
            return Failure("client_secret_required", "Gmail login requires a client secret.", profile.Id, MailProfileKind.Gmail);
        }

        var effectiveRequest = new GmailProfileLoginRequest {
            ProfileId = profile.Id,
            GmailAccount = gmailAccount,
            ClientId = clientId,
            ClientSecret = clientSecret,
            ClientSecretReference = null,
            Scopes = NormalizeScopes(request.Scopes, MailProfileAuthDefaults.GmailScopes)
        };
        var credential = await _loginGmailAsync(effectiveRequest, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(credential.AccessToken)) {
            return Failure("access_token_missing", "Gmail authentication did not return an access token.", profile.Id, MailProfileKind.Gmail);
        }

        profile.Settings[MailProfileSettingsKeys.Mailbox] = gmailAccount!;
        profile.Settings[MailProfileSettingsKeys.ClientId] = clientId!;
        profile.Settings[MailProfileSettingsKeys.AuthFlow] = MailProfileAuthFlowNames.Interactive;
        profile.Settings[MailProfileSettingsKeys.LoginHint] = gmailAccount!;
        UpsertTokenExpiration(profile, credential.ExpiresOn);
        profile.DefaultMailbox ??= gmailAccount;
        profile.DefaultSender ??= gmailAccount;

        var saveResult = await _profiles.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        if (!saveResult.Succeeded) {
            return FromOperation(saveResult, profile.Id, MailProfileKind.Gmail);
        }

        var secretResult = await PersistSecretAsync(profile.Id, MailSecretNames.ClientSecret, clientSecret, cancellationToken).ConfigureAwait(false);
        if (secretResult != null) {
            return secretResult;
        }
        secretResult = await PersistSecretAsync(profile.Id, MailSecretNames.AccessToken, credential.AccessToken, cancellationToken).ConfigureAwait(false);
        if (secretResult != null) {
            return secretResult;
        }
        if (!string.IsNullOrWhiteSpace(credential.RefreshToken)) {
            secretResult = await PersistSecretAsync(profile.Id, MailSecretNames.RefreshToken, credential.RefreshToken, cancellationToken).ConfigureAwait(false);
            if (secretResult != null) {
                return secretResult;
            }
        }

        return Success("Gmail login completed.", profile.Id, MailProfileKind.Gmail, credential);
    }

    /// <inheritdoc />
    public async Task<MailProfileAuthenticationResult> LoginGraphAsync(GraphProfileLoginRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var profileResult = await LoadProfileAsync(request.ProfileId, MailProfileKind.Graph, cancellationToken).ConfigureAwait(false);
        if (profileResult.Error != null) {
            return profileResult.Error;
        }

        var profile = profileResult.Profile!;
        var login = FirstNonEmpty(request.Login, profile.DefaultMailbox, profile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out var mailboxSetting) ? mailboxSetting : null);
        var mailbox = FirstNonEmpty(request.Mailbox, profile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out var existingMailbox) ? existingMailbox : null, profile.DefaultMailbox, login);
        var clientId = FirstNonEmpty(request.ClientId, profile.Settings.TryGetValue(MailProfileSettingsKeys.ClientId, out var clientIdSetting) ? clientIdSetting : null);
        var tenantId = FirstNonEmpty(request.TenantId, profile.Settings.TryGetValue(MailProfileSettingsKeys.TenantId, out var tenantIdSetting) ? tenantIdSetting : null);
        var redirectUri = FirstNonEmpty(request.RedirectUri, profile.Settings.TryGetValue(MailProfileSettingsKeys.RedirectUri, out var redirectUriSetting) ? redirectUriSetting : null)
            ?? MailProfileAuthDefaults.GraphRedirectUri;

        if (string.IsNullOrWhiteSpace(clientId)) {
            return Failure("client_id_required", "Graph login requires a client id.", profile.Id, MailProfileKind.Graph);
        }
        if (string.IsNullOrWhiteSpace(tenantId)) {
            return Failure("tenant_id_required", "Graph login requires a tenant id.", profile.Id, MailProfileKind.Graph);
        }

        var effectiveRequest = new GraphProfileLoginRequest {
            ProfileId = profile.Id,
            Login = login,
            Mailbox = mailbox,
            ClientId = clientId,
            TenantId = tenantId,
            RedirectUri = redirectUri,
            Scopes = NormalizeScopes(request.Scopes, MailProfileAuthDefaults.GraphScopes)
        };
        var credential = await _loginGraphAsync(effectiveRequest, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(credential.AccessToken)) {
            return Failure("access_token_missing", "Graph authentication did not return an access token.", profile.Id, MailProfileKind.Graph);
        }

        profile.Settings[MailProfileSettingsKeys.ClientId] = clientId!;
        profile.Settings[MailProfileSettingsKeys.TenantId] = tenantId!;
        profile.Settings[MailProfileSettingsKeys.RedirectUri] = redirectUri;
        profile.Settings[MailProfileSettingsKeys.AuthFlow] = MailProfileAuthFlowNames.Interactive;
        UpsertSetting(profile.Settings, MailProfileSettingsKeys.LoginHint, login);
        UpsertTokenExpiration(profile, credential.ExpiresOn);
        if (!string.IsNullOrWhiteSpace(mailbox)) {
            profile.Settings[MailProfileSettingsKeys.Mailbox] = mailbox!;
            profile.DefaultMailbox ??= mailbox;
            profile.DefaultSender ??= mailbox;
        } else if (!string.IsNullOrWhiteSpace(credential.UserName)) {
            profile.DefaultMailbox ??= credential.UserName;
        }

        var saveResult = await _profiles.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        if (!saveResult.Succeeded) {
            return FromOperation(saveResult, profile.Id, MailProfileKind.Graph);
        }

        var secretResult = await PersistSecretAsync(profile.Id, MailSecretNames.AccessToken, credential.AccessToken, cancellationToken).ConfigureAwait(false);
        if (secretResult != null) {
            return secretResult;
        }

        return Success("Graph login completed.", profile.Id, MailProfileKind.Graph, credential);
    }

    /// <inheritdoc />
    public async Task<MailProfileAuthenticationResult> RefreshAsync(string profileId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            return Failure("profile_required", "Profile id is required.", profileId, MailProfileKind.Unknown);
        }

        var profile = await _profiles.GetProfileAsync(profileId.Trim(), cancellationToken).ConfigureAwait(false);
        if (profile == null) {
            return Failure("profile_not_found", "Profile was not found.", profileId, MailProfileKind.Unknown);
        }

        switch (profile.Kind) {
            case MailProfileKind.Gmail:
                return await LoginGmailAsync(new GmailProfileLoginRequest {
                    ProfileId = profile.Id
                }, cancellationToken).ConfigureAwait(false);
            case MailProfileKind.Graph:
                return await LoginGraphAsync(new GraphProfileLoginRequest {
                    ProfileId = profile.Id
                }, cancellationToken).ConfigureAwait(false);
            default:
                return Failure(
                    "refresh_not_supported",
                    $"Profile kind '{profile.Kind}' does not support shared refresh-auth.",
                    profile.Id,
                    profile.Kind);
        }
    }

    private async Task<(MailProfile? Profile, MailProfileAuthenticationResult? Error)> LoadProfileAsync(string profileId, MailProfileKind expectedKind, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            return (null, Failure("profile_required", "Profile id is required.", profileId, expectedKind));
        }

        var profile = await _profiles.GetProfileAsync(profileId.Trim(), cancellationToken).ConfigureAwait(false);
        if (profile == null) {
            return (null, Failure("profile_not_found", "Profile was not found.", profileId, expectedKind));
        }
        if (profile.Kind != expectedKind) {
            return (null, Failure("profile_kind_mismatch", $"Profile '{profile.Id}' is not a {expectedKind} profile.", profile.Id, profile.Kind));
        }

        return (CloneProfile(profile), null);
    }

    private Task<string?> TryReadSecretAsync(string profileId, string secretName, CancellationToken cancellationToken) =>
        _secretStore.GetSecretAsync(profileId, secretName, cancellationToken);

    private async Task<MailProfileAuthenticationResult?> PersistSecretAsync(string profileId, string secretName, string? secretValue, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(secretValue)) {
            return null;
        }

        var normalizedSecretValue = secretValue!.Trim();
        var result = await _profileSecrets.SetSecretAsync(profileId, secretName, normalizedSecretValue, cancellationToken).ConfigureAwait(false);
        return result.Succeeded
            ? null
            : FromOperation(result, profileId, MailProfileKind.Unknown);
    }

    private static Task<OAuthCredential> DefaultLoginGmailAsync(GmailProfileLoginRequest request, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return GmailOAuthHelpers.AcquireTokenCachedAsync(
            request.GmailAccount!,
            request.ClientId!,
            request.ClientSecret!,
            request.Scopes ?? MailProfileAuthDefaults.GmailScopes);
    }

    private static Task<OAuthCredential> DefaultLoginGraphAsync(GraphProfileLoginRequest request, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return OAuthHelpers.AcquireO365TokenCachedAsync(
            request.Login,
            request.ClientId!,
            request.TenantId!,
            request.RedirectUri!,
            request.Scopes ?? MailProfileAuthDefaults.GraphScopes);
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

    private static IReadOnlyList<string> NormalizeScopes(IReadOnlyList<string>? scopes, IReadOnlyList<string> fallback) =>
        (scopes == null || scopes.Count == 0
            ? fallback
            : scopes.Where(scope => !string.IsNullOrWhiteSpace(scope)).Select(scope => scope.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray())!;

    private static string? FirstNonEmpty(params string?[] values) {
        foreach (var value in values) {
            if (!string.IsNullOrWhiteSpace(value)) {
                return value!.Trim();
            }
        }

        return null;
    }

    private static DateTimeOffset? TryParseTimestamp(string? value) =>
        DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var timestamp)
            ? timestamp
            : null;

    private static string DetermineMode(
        MailProfile profile,
        string? authFlow,
        string? authMode,
        bool hasAccessToken,
        bool hasRefreshToken,
        bool hasClientSecret,
        bool hasCertificatePath,
        bool hasPassword) {
        if (string.Equals(authFlow, MailProfileAuthFlowNames.Interactive, StringComparison.OrdinalIgnoreCase)) {
            return "interactive";
        }

        if (string.Equals(authFlow, MailProfileAuthFlowNames.ManualToken, StringComparison.OrdinalIgnoreCase)) {
            return "manualToken";
        }

        return profile.Kind switch {
            MailProfileKind.Graph when hasClientSecret || hasCertificatePath => "appOnly",
            MailProfileKind.Graph when hasAccessToken => "manualToken",
            MailProfileKind.Gmail when hasRefreshToken && hasClientSecret => "interactive",
            MailProfileKind.Gmail when hasAccessToken => "manualToken",
            MailProfileKind.Imap or MailProfileKind.Pop3 or MailProfileKind.Smtp
                when string.Equals(authMode, "oauth2", StringComparison.OrdinalIgnoreCase) => "oauth2",
            MailProfileKind.Imap or MailProfileKind.Pop3 or MailProfileKind.Smtp
                when hasPassword => "basic",
            _ => "unknown"
        };
    }

    private static bool DetermineCanLoginInteractively(
        MailProfileKind kind,
        bool hasClientId,
        bool hasTenantId,
        bool hasClientSecret,
        string? mailbox) =>
        kind switch {
            MailProfileKind.Gmail => hasClientId && hasClientSecret && !string.IsNullOrWhiteSpace(mailbox),
            MailProfileKind.Graph => hasClientId && hasTenantId,
            _ => false
        };

    private static bool DetermineCanRefresh(
        MailProfileKind kind,
        string? authFlow,
        bool hasClientId,
        bool hasTenantId,
        bool hasClientSecret,
        string? mailbox) =>
        kind switch {
            MailProfileKind.Gmail => hasClientId && hasClientSecret && !string.IsNullOrWhiteSpace(mailbox),
            MailProfileKind.Graph => string.Equals(authFlow, MailProfileAuthFlowNames.Interactive, StringComparison.OrdinalIgnoreCase) && hasClientId && hasTenantId,
            _ => false
        };

    private static string BuildSummary(
        MailProfile profile,
        string mode,
        string? mailbox,
        bool hasAccessToken,
        DateTimeOffset? tokenExpiresOn,
        bool isTokenExpired,
        bool canRefresh) {
        var mailboxSegment = string.IsNullOrWhiteSpace(mailbox) ? "mailbox not set" : $"mailbox={mailbox}";
        var tokenSegment = !hasAccessToken
            ? "token missing"
            : tokenExpiresOn == null
                ? "token present"
                : isTokenExpired
                    ? $"token expired {tokenExpiresOn:O}"
                    : $"token expires {tokenExpiresOn:O}";
        var refreshSegment = canRefresh ? "refresh available" : "refresh unavailable";
        return $"{profile.Id} [{profile.Kind}] auth={mode}, {mailboxSegment}, {tokenSegment}, {refreshSegment}.";
    }

    private static void UpsertSetting(IDictionary<string, string> settings, string key, string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            settings.Remove(key);
            return;
        }

        settings[key] = value!.Trim();
    }

    private static void UpsertTokenExpiration(MailProfile profile, DateTimeOffset expiresOn) {
        if (expiresOn == default || expiresOn == DateTimeOffset.MaxValue) {
            profile.Settings.Remove(MailProfileSettingsKeys.TokenExpiresOn);
            return;
        }

        profile.Settings[MailProfileSettingsKeys.TokenExpiresOn] = expiresOn.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static MailProfileAuthenticationResult Success(string message, string profileId, MailProfileKind kind, OAuthCredential credential) => new() {
        Succeeded = true,
        Message = message,
        ProfileId = profileId,
        ProfileKind = kind,
        UserName = credential.UserName,
        ExpiresOn = credential.ExpiresOn == default ? null : credential.ExpiresOn
    };

    private static MailProfileAuthenticationResult Failure(string code, string message, string? profileId, MailProfileKind kind) => new() {
        Succeeded = false,
        Code = code,
        Message = message,
        ProfileId = string.IsNullOrWhiteSpace(profileId) ? null : profileId,
        ProfileKind = kind
    };

    private static MailProfileAuthenticationResult FromOperation(OperationResult result, string profileId, MailProfileKind kind) => new() {
        Succeeded = result.Succeeded,
        Code = result.Code,
        Message = result.Message,
        ProfileId = profileId,
        ProfileKind = kind
    };
}