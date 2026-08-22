namespace Mailozaurr;

/// <summary>
/// Builds Graph API sessions from reusable profile and secret stores.
/// </summary>
public sealed class GraphSessionFactory : IGraphSessionFactory {
    private readonly IMailSecretStore _secretStore;
    private readonly Func<MailProfile, GraphCredential, CancellationToken, Task<OAuthCredential>> _acquireCredentialAsync;
    private readonly Func<MailProfile, CancellationToken, Task<OAuthCredential?>> _acquireSilentCredentialAsync;
    private readonly Func<GraphSessionRequest, CancellationToken, Task<GraphSession>> _connectAsync;

    /// <summary>
    /// Creates a new Graph session factory.
    /// </summary>
    public GraphSessionFactory(
        IMailSecretStore secretStore,
        Func<MailProfile, GraphCredential, CancellationToken, Task<OAuthCredential>>? acquireCredentialAsync = null,
        Func<MailProfile, CancellationToken, Task<OAuthCredential?>>? acquireSilentCredentialAsync = null,
        Func<GraphSessionRequest, CancellationToken, Task<GraphSession>>? connectAsync = null) {
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _acquireCredentialAsync = acquireCredentialAsync ?? DefaultAcquireCredentialAsync;
        _acquireSilentCredentialAsync = acquireSilentCredentialAsync ?? DefaultAcquireSilentCredentialAsync;
        _connectAsync = connectAsync ?? DefaultConnectAsync;
    }

    /// <inheritdoc />
    public async Task<GraphSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) {
        if (profile == null) {
            throw new ArgumentNullException(nameof(profile));
        }

        var userId = ResolveUserId(profile);
        var resolvedCredential = await ResolveCredentialAsync(profile, userId, cancellationToken).ConfigureAwait(false);
        var credential = resolvedCredential.Credential;
        var graphCredential = await TryBuildGraphCredentialAsync(profile, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(credential.UserName)) {
            credential.UserName = userId;
        }
        if (credential.ExpiresOn == default) {
            credential.ExpiresOn = DateTimeOffset.MaxValue;
        }

        return await _connectAsync(new GraphSessionRequest {
            UserId = userId,
            Credential = credential,
            GraphCredential = graphCredential,
            AuthenticationMode = resolvedCredential.AuthenticationMode
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ResolvedGraphCredential> ResolveCredentialAsync(MailProfile profile, string userId, CancellationToken cancellationToken) {
        var accessToken = await _secretStore.GetSecretAsync(profile.Id, MailSecretNames.AccessToken, cancellationToken).ConfigureAwait(false);
        var tokenExpiresOn = TryResolveTokenExpiration(profile);
        if (!string.IsNullOrWhiteSpace(accessToken)) {
            if (!ShouldRefreshToken(tokenExpiresOn)) {
                return new ResolvedGraphCredential(
                    await CreateStoredTokenCredentialAsync(profile, userId, accessToken!, tokenExpiresOn, cancellationToken).ConfigureAwait(false),
                    ResolveStoredTokenMode(profile));
            }
        }

        if (CanUseInteractiveSilentRefresh(profile)) {
            var refreshedCredential = await _acquireSilentCredentialAsync(profile, cancellationToken).ConfigureAwait(false);
            if (refreshedCredential != null && !string.IsNullOrWhiteSpace(refreshedCredential.AccessToken)) {
                refreshedCredential.UserName = string.IsNullOrWhiteSpace(refreshedCredential.UserName) ? userId : refreshedCredential.UserName;
                if (refreshedCredential.ExpiresOn == default) {
                    refreshedCredential.ExpiresOn = DateTimeOffset.MaxValue;
                }
                return new ResolvedGraphCredential(refreshedCredential, GraphSessionAuthenticationMode.Delegated);
            }
        }

        if (!string.IsNullOrWhiteSpace(accessToken)) {
            return new ResolvedGraphCredential(
                await CreateStoredTokenCredentialAsync(profile, userId, accessToken!, tokenExpiresOn, cancellationToken).ConfigureAwait(false),
                ResolveStoredTokenMode(profile));
        }

        var graphCredential = await BuildGraphCredentialAsync(profile, cancellationToken).ConfigureAwait(false);
        var credential = await _acquireCredentialAsync(profile, graphCredential, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(credential.AccessToken)) {
            throw new InvalidOperationException($"Graph profile '{profile.Id}' did not produce an access token.");
        }

        return new ResolvedGraphCredential(credential, GraphSessionAuthenticationMode.Application);
    }

    private async Task<OAuthCredential> CreateStoredTokenCredentialAsync(
        MailProfile profile,
        string userId,
        string accessToken,
        DateTimeOffset? tokenExpiresOn,
        CancellationToken cancellationToken) => new() {
        UserName = userId,
        AccessToken = accessToken.Trim(),
        RefreshToken = await _secretStore.GetSecretAsync(profile.Id, MailSecretNames.RefreshToken, cancellationToken).ConfigureAwait(false),
        ClientId = profile.Settings.TryGetValue(MailProfileSettingsKeys.ClientId, out var clientId) ? clientId : null,
        ClientSecret = await _secretStore.GetSecretAsync(profile.Id, MailSecretNames.ClientSecret, cancellationToken).ConfigureAwait(false),
        ExpiresOn = tokenExpiresOn ?? DateTimeOffset.MaxValue
    };

    private static GraphSessionAuthenticationMode ResolveStoredTokenMode(MailProfile profile) =>
        profile.Settings.TryGetValue(MailProfileSettingsKeys.AuthFlow, out var authFlow) &&
        string.Equals(authFlow, MailProfileAuthFlowNames.Interactive, StringComparison.OrdinalIgnoreCase)
            ? GraphSessionAuthenticationMode.Delegated
            : GraphSessionAuthenticationMode.Unknown;

    private async Task<GraphCredential> BuildGraphCredentialAsync(MailProfile profile, CancellationToken cancellationToken) {
        var clientId = GetRequiredSetting(profile, MailProfileSettingsKeys.ClientId);
        var tenantId = GetRequiredSetting(profile, MailProfileSettingsKeys.TenantId);
        var clientSecret = await _secretStore.GetSecretAsync(profile.Id, MailSecretNames.ClientSecret, cancellationToken).ConfigureAwait(false);
        var certificatePassword = await _secretStore.GetSecretAsync(profile.Id, MailSecretNames.CertificatePassword, cancellationToken).ConfigureAwait(false);
        profile.Settings.TryGetValue(MailProfileSettingsKeys.CertificatePath, out var certificatePath);

        if (string.IsNullOrWhiteSpace(clientSecret) && string.IsNullOrWhiteSpace(certificatePath)) {
            throw new InvalidOperationException(
                $"Graph profile '{profile.Id}' requires either secret '{MailSecretNames.AccessToken}', secret '{MailSecretNames.ClientSecret}', or setting '{MailProfileSettingsKeys.CertificatePath}'.");
        }

        return new GraphCredential {
            ClientId = clientId,
            DirectoryId = tenantId,
            ClientSecret = string.IsNullOrWhiteSpace(clientSecret) ? null : clientSecret!.Trim(),
            CertificatePath = string.IsNullOrWhiteSpace(certificatePath) ? null : certificatePath!.Trim(),
            CertificatePassword = string.IsNullOrWhiteSpace(certificatePassword) ? null : certificatePassword!.Trim()
        };
    }

    private async Task<GraphCredential?> TryBuildGraphCredentialAsync(MailProfile profile, CancellationToken cancellationToken) {
        if (!profile.Settings.TryGetValue(MailProfileSettingsKeys.ClientId, out var clientId) ||
            string.IsNullOrWhiteSpace(clientId) ||
            !profile.Settings.TryGetValue(MailProfileSettingsKeys.TenantId, out var tenantId) ||
            string.IsNullOrWhiteSpace(tenantId)) {
            return null;
        }

        var clientSecret = await _secretStore.GetSecretAsync(profile.Id, MailSecretNames.ClientSecret, cancellationToken).ConfigureAwait(false);
        var certificatePassword = await _secretStore.GetSecretAsync(profile.Id, MailSecretNames.CertificatePassword, cancellationToken).ConfigureAwait(false);
        profile.Settings.TryGetValue(MailProfileSettingsKeys.CertificatePath, out var certificatePath);

        if (string.IsNullOrWhiteSpace(clientSecret) && string.IsNullOrWhiteSpace(certificatePath)) {
            return null;
        }

        return new GraphCredential {
            ClientId = clientId.Trim(),
            DirectoryId = tenantId.Trim(),
            ClientSecret = string.IsNullOrWhiteSpace(clientSecret) ? null : clientSecret!.Trim(),
            CertificatePath = string.IsNullOrWhiteSpace(certificatePath) ? null : certificatePath!.Trim(),
            CertificatePassword = string.IsNullOrWhiteSpace(certificatePassword) ? null : certificatePassword!.Trim()
        };
    }

    private static async Task<OAuthCredential> DefaultAcquireCredentialAsync(
        MailProfile profile,
        GraphCredential graphCredential,
        CancellationToken cancellationToken) {
        var authorization = await MicrosoftGraphUtils.ConnectO365GraphAsync(
            graphCredential,
            graphCredential.DirectoryId,
            "https://graph.microsoft.com",
            cancellationToken).ConfigureAwait(false);
        var accessToken = NormalizeAccessToken(authorization);
        return new OAuthCredential {
            UserName = ResolveUserId(profile),
            AccessToken = accessToken,
            ClientId = graphCredential.ClientId,
            ClientSecret = graphCredential.ClientSecret,
            ExpiresOn = DateTimeOffset.MaxValue
        };
    }

    private static Task<OAuthCredential?> DefaultAcquireSilentCredentialAsync(MailProfile profile, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!profile.Settings.TryGetValue(MailProfileSettingsKeys.ClientId, out var clientId) ||
            string.IsNullOrWhiteSpace(clientId) ||
            !profile.Settings.TryGetValue(MailProfileSettingsKeys.TenantId, out var tenantId) ||
            string.IsNullOrWhiteSpace(tenantId)) {
            return Task.FromResult<OAuthCredential?>(null);
        }

        var redirectUri = profile.Settings.TryGetValue(MailProfileSettingsKeys.RedirectUri, out var storedRedirectUri) &&
                          !string.IsNullOrWhiteSpace(storedRedirectUri)
            ? storedRedirectUri.Trim()
            : MailProfileAuthDefaults.GraphRedirectUri;
        var login = ResolveLoginHint(profile);
        return OAuthHelpers.TryAcquireO365TokenSilentAsync(
            login,
            clientId.Trim(),
            tenantId.Trim(),
            redirectUri,
            MailProfileAuthDefaults.GraphScopes);
    }

    private static Task<GraphSession> DefaultConnectAsync(GraphSessionRequest request, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new GraphSession(
            new GraphApiClient(request.Credential),
            request.UserId,
            request.Credential,
            request.GraphCredential,
            request.AuthenticationMode));
    }

    private static string ResolveUserId(MailProfile profile) {
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out var mailbox) &&
            !string.IsNullOrWhiteSpace(mailbox)) {
            return mailbox.Trim();
        }
        if (!string.IsNullOrWhiteSpace(profile.DefaultMailbox)) {
            return profile.DefaultMailbox!.Trim();
        }

        return "me";
    }

    private static string? ResolveLoginHint(MailProfile profile) {
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.LoginHint, out var loginHint) &&
            !string.IsNullOrWhiteSpace(loginHint)) {
            return loginHint.Trim();
        }

        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out var mailbox) &&
            !string.IsNullOrWhiteSpace(mailbox)) {
            return mailbox.Trim();
        }

        if (!string.IsNullOrWhiteSpace(profile.DefaultMailbox)) {
            return profile.DefaultMailbox!.Trim();
        }

        return null;
    }

    private static DateTimeOffset? TryResolveTokenExpiration(MailProfile profile) {
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.TokenExpiresOn, out var expiresOnValue) &&
            !string.IsNullOrWhiteSpace(expiresOnValue) &&
            DateTimeOffset.TryParse(expiresOnValue, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var expiresOn)) {
            return expiresOn;
        }

        return null;
    }

    private static bool ShouldRefreshToken(DateTimeOffset? expiresOn) =>
        expiresOn.HasValue && expiresOn.Value <= DateTimeOffset.UtcNow.Add(MailProfileAuthDefaults.TokenRefreshWindow);

    private static bool CanUseInteractiveSilentRefresh(MailProfile profile) {
        if (!profile.Settings.TryGetValue(MailProfileSettingsKeys.AuthFlow, out var authFlow) ||
            !string.Equals(authFlow, MailProfileAuthFlowNames.Interactive, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        return profile.Settings.TryGetValue(MailProfileSettingsKeys.ClientId, out var clientId) &&
               !string.IsNullOrWhiteSpace(clientId) &&
               profile.Settings.TryGetValue(MailProfileSettingsKeys.TenantId, out var tenantId) &&
               !string.IsNullOrWhiteSpace(tenantId);
    }

    private static string GetRequiredSetting(MailProfile profile, string key) {
        if (profile.Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) {
            return value.Trim();
        }

        throw new InvalidOperationException($"Graph profile '{profile.Id}' is missing required setting '{key}'.");
    }

    private static string NormalizeAccessToken(string authorizationHeaderValue) {
        if (string.IsNullOrWhiteSpace(authorizationHeaderValue)) {
            throw new InvalidOperationException("Graph authorization did not return a token.");
        }

        var trimmed = authorizationHeaderValue.Trim();
        const string bearerPrefix = "Bearer ";
        return trimmed.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed.Substring(bearerPrefix.Length).Trim()
            : trimmed;
    }

    private sealed class ResolvedGraphCredential {
        internal ResolvedGraphCredential(
            OAuthCredential credential,
            GraphSessionAuthenticationMode authenticationMode) {
            Credential = credential;
            AuthenticationMode = authenticationMode;
        }

        internal OAuthCredential Credential { get; }
        internal GraphSessionAuthenticationMode AuthenticationMode { get; }
    }
}
