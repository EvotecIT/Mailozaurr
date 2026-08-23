namespace Mailozaurr;

/// <summary>Default profile-backed JMAP session factory.</summary>
public sealed class JmapSessionFactory : IJmapSessionFactory {
    private readonly IMailSecretStore _secretStore;
    private readonly Func<JmapSessionRequest, CancellationToken, Task<JmapSession>> _connectAsync;

    /// <summary>Creates a JMAP session factory.</summary>
    public JmapSessionFactory(
        IMailSecretStore secretStore,
        Func<JmapSessionRequest, CancellationToken, Task<JmapSession>>? connectAsync = null) {
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _connectAsync = connectAsync ?? DefaultConnectAsync;
    }

    /// <inheritdoc />
    public async Task<JmapSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (profile.Kind != MailProfileKind.Jmap) throw new NotSupportedException("The profile is not a JMAP profile.");
        if (!profile.Settings.TryGetValue(MailProfileSettingsKeys.JmapSessionUrl, out var sessionUrlValue) ||
            !Uri.TryCreate(sessionUrlValue, UriKind.Absolute, out var sessionUrl)) {
            throw new InvalidOperationException("JMAP profiles require an absolute HTTPS session URL without user information or a fragment.");
        }
        try {
            sessionUrl = JmapApiClient.ValidateSessionUrl(sessionUrl);
        } catch (ArgumentException ex) {
            throw new InvalidOperationException("JMAP profiles require an absolute HTTPS session URL without user information or a fragment.", ex);
        }
        var accessToken = await _secretStore.GetSecretAsync(profile.Id, MailSecretNames.AccessToken, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(accessToken)) throw new InvalidOperationException("JMAP profiles require a stored bearer access token.");
        profile.Settings.TryGetValue(MailProfileSettingsKeys.JmapAccountId, out var accountId);
        var allowCrossOriginApiUrl = false;
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.JmapAllowCrossOriginApiUrl, out var crossOriginValue) &&
            !bool.TryParse(crossOriginValue, out allowCrossOriginApiUrl)) {
            throw new InvalidOperationException($"JMAP setting '{MailProfileSettingsKeys.JmapAllowCrossOriginApiUrl}' must be true or false.");
        }
        return await _connectAsync(new JmapSessionRequest {
            SessionUrl = sessionUrl!,
            AccessToken = accessToken!.Trim(),
            AccountId = accountId,
            AllowCrossOriginApiUrl = allowCrossOriginApiUrl
        }, cancellationToken).ConfigureAwait(false);
    }

    private static Task<JmapSession> DefaultConnectAsync(JmapSessionRequest request, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new JmapSession(
            new JmapApiClient(request.SessionUrl, request.AccessToken, allowCrossOriginApiUrl: request.AllowCrossOriginApiUrl),
            request.AccountId));
    }
}
