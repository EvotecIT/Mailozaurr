using Mailozaurr.Application;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Mailozaurr.Cli.Mcp;

public sealed partial class MailMcpTools {
    [McpServerTool]
    [Description("Lists configured Mailozaurr profiles that can be used for mailbox and send operations.")]
    public Task<IReadOnlyList<MailProfile>> mail_profiles_list(CancellationToken cancellationToken = default) =>
        _application.Profiles.GetProfilesAsync(cancellationToken);

    [McpServerTool]
    [Description("Lists higher-level summaries for all configured Mailozaurr profiles, including kind, capabilities, auth posture, and readiness.")]
    public Task<IReadOnlyList<MailProfileOverview>> mail_profiles_summary_list(
        [Description("Optional provider kind filter, such as imap, graph, gmail, smtp, or pop3.")] string? kind = null,
        [Description("When true, only returns ready profiles.")] bool readyOnly = false,
        [Description("When true, only returns profiles that support reading.")] bool canReadOnly = false,
        [Description("When true, only returns profiles that support sending.")] bool canSendOnly = false,
        [Description("When true, only returns profiles marked as default.")] bool defaultOnly = false,
        [Description("Optional sort key: id, kind, or readiness.")] string? sortBy = null,
        [Description("When true, reverses the selected sort order.")] bool descending = false,
        CancellationToken cancellationToken = default) =>
        _application.ProfileOverview.GetOverviewsAsync(new MailProfileOverviewQuery {
            Kind = string.IsNullOrWhiteSpace(kind) ? null : MailProfileKindParser.Parse(kind),
            SortBy = string.IsNullOrWhiteSpace(sortBy)
                ? MailProfileOverviewSortBy.Id
                : Enum.Parse<MailProfileOverviewSortBy>(sortBy, ignoreCase: true),
            Descending = descending,
            ReadyOnly = readyOnly,
            CanReadOnly = canReadOnly,
            CanSendOnly = canSendOnly,
            DefaultOnly = defaultOnly
        }, cancellationToken);

    [McpServerTool]
    [Description("Lists lightweight profile summaries for all configured Mailozaurr profiles.")]
    public Task<IReadOnlyList<MailProfileOverviewCompact>> mail_profiles_summary_compact_list(
        [Description("Optional provider kind filter, such as imap, graph, gmail, smtp, or pop3.")] string? kind = null,
        [Description("When true, only returns ready profiles.")] bool readyOnly = false,
        [Description("When true, only returns profiles that support reading.")] bool canReadOnly = false,
        [Description("When true, only returns profiles that support sending.")] bool canSendOnly = false,
        [Description("When true, only returns profiles marked as default.")] bool defaultOnly = false,
        [Description("Optional sort key: id, kind, or readiness.")] string? sortBy = null,
        [Description("When true, reverses the selected sort order.")] bool descending = false,
        CancellationToken cancellationToken = default) =>
        _application.ProfileOverview.GetCompactOverviewsAsync(new MailProfileOverviewQuery {
            Kind = string.IsNullOrWhiteSpace(kind) ? null : MailProfileKindParser.Parse(kind),
            SortBy = string.IsNullOrWhiteSpace(sortBy)
                ? MailProfileOverviewSortBy.Id
                : Enum.Parse<MailProfileOverviewSortBy>(sortBy, ignoreCase: true),
            Descending = descending,
            ReadyOnly = readyOnly,
            CanReadOnly = canReadOnly,
            CanSendOnly = canSendOnly,
            DefaultOnly = defaultOnly
        }, cancellationToken);

    [McpServerTool]
    [Description("Returns the effective capabilities for a configured Mailozaurr profile.")]
    public async Task<ProfileCapabilities> mail_capabilities_get(
        [Description("The profile identifier to inspect.")] string profileId,
        CancellationToken cancellationToken = default) {
        var capabilities = await _application.Profiles.GetCapabilitiesAsync(profileId, cancellationToken).ConfigureAwait(false);
        return capabilities ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");
    }

    [McpServerTool]
    [Description("Gets a configured Mailozaurr profile by identifier.")]
    public async Task<MailProfile> mail_profile_get(
        [Description("The profile identifier to retrieve.")] string profileId,
        CancellationToken cancellationToken = default) {
        var profile = await _application.Profiles.GetProfileAsync(profileId, cancellationToken).ConfigureAwait(false);
        return profile ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");
    }

    [McpServerTool]
    [Description("Inspects whether a configured Mailozaurr profile is ready to use, including provider-specific auth prerequisites.")]
    public Task<MailProfileValidationResult> mail_profile_doctor(
        [Description("The profile identifier to inspect.")] string profileId,
        CancellationToken cancellationToken = default) =>
        _application.Profiles.DiagnoseAsync(profileId, cancellationToken);

    [McpServerTool]
    [Description("Returns a higher-level summary for a configured Mailozaurr profile, combining kind, capabilities, auth posture, and readiness.")]
    public async Task<MailProfileOverview> mail_profile_summary(
        [Description("The profile identifier to summarize.")] string profileId,
        CancellationToken cancellationToken = default) {
        var overview = await _application.ProfileOverview.GetOverviewAsync(profileId, cancellationToken).ConfigureAwait(false);
        return overview ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");
    }

    [McpServerTool]
    [Description("Returns a lightweight summary for a configured Mailozaurr profile.")]
    public async Task<MailProfileOverviewCompact> mail_profile_summary_compact(
        [Description("The profile identifier to summarize.")] string profileId,
        CancellationToken cancellationToken = default) {
        var overview = await _application.ProfileOverview.GetCompactOverviewAsync(profileId, cancellationToken).ConfigureAwait(false);
        return overview ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");
    }

    [McpServerTool]
    [Description("Runs structural validation for a configured Mailozaurr profile without provider-specific readiness checks.")]
    public async Task<MailProfileValidationResult> mail_profile_validate(
        [Description("The profile identifier to validate.")] string profileId,
        CancellationToken cancellationToken = default) {
        var profile = await _application.Profiles.GetProfileAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (profile == null) {
            throw new InvalidOperationException($"Profile '{profileId}' was not found.");
        }

        return await _application.Profiles.ValidateAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    [McpServerTool]
    [Description("Creates or updates a Mailozaurr profile using shared profile storage.")]
    public async Task<MailProfile> mail_profile_save(
        [Description("The stable profile identifier to create or update.")] string profileId,
        [Description("The provider kind, such as imap, graph, gmail, smtp, or pop3.")] string kind,
        [Description("The human-readable display name for the profile.")] string displayName,
        [Description("Optional description for operators.")] string? description = null,
        [Description("Optional default sender email address.")] string? defaultSender = null,
        [Description("Optional default mailbox identifier or address.")] string? defaultMailbox = null,
        [Description("When true, marks this profile as the default profile.")] bool isDefault = false,
        [Description("Optional non-secret settings to persist with the profile.")] Dictionary<string, string>? settings = null,
        CancellationToken cancellationToken = default) {
        var profile = new MailProfile {
            Id = profileId,
            DisplayName = displayName,
            Kind = MailProfileKindParser.Parse(kind),
            Description = description,
            DefaultSender = defaultSender,
            DefaultMailbox = defaultMailbox,
            IsDefault = isDefault,
            Settings = settings == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase)
        };

        var result = await _application.Profiles.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded) {
            throw new InvalidOperationException(result.Message ?? $"Profile '{profileId}' could not be saved.");
        }

        return await mail_profile_get(profileId, cancellationToken).ConfigureAwait(false);
    }

    [McpServerTool]
    [Description("Creates or updates a Microsoft Graph profile using the shared Graph bootstrap workflow.")]
    public async Task<MailProfile> mail_profile_graph_bootstrap(
        [Description("The stable profile identifier to create or update.")] string profileId,
        [Description("The human-readable display name for the profile.")] string displayName,
        [Description("The mailbox address or user principal name for Graph operations.")] string mailbox,
        [Description("Optional description for operators.")] string? description = null,
        [Description("Optional default sender email address. Defaults to the mailbox when omitted.")] string? defaultSender = null,
        [Description("When true, marks this profile as the default profile.")] bool isDefault = false,
        [Description("Optional Graph client/application identifier.")] string? clientId = null,
        [Description("Optional Graph tenant/directory identifier.")] string? tenantId = null,
        [Description("Optional confidential client secret to store securely. Prefer using clientSecretReference for MCP hosts.")] string? clientSecret = null,
        [Description("Optional secret reference in the form '<profile-id>:<secret-name>' or '<secret-name>' for the Graph client secret.")] string? clientSecretReference = null,
        [Description("Optional explicit access token to store securely. Prefer using accessTokenReference for MCP hosts.")] string? accessToken = null,
        [Description("Optional secret reference in the form '<profile-id>:<secret-name>' or '<secret-name>' for the Graph access token.")] string? accessTokenReference = null,
        [Description("Optional certificate path for certificate-based auth.")] string? certificatePath = null,
        [Description("Optional certificate password to store securely. Prefer using certificatePasswordReference for MCP hosts.")] string? certificatePassword = null,
        [Description("Optional secret reference in the form '<profile-id>:<secret-name>' or '<secret-name>' for the certificate password.")] string? certificatePasswordReference = null,
        CancellationToken cancellationToken = default) {
        var result = await _application.ProfileBootstrap.SaveGraphProfileAsync(new GraphProfileBootstrapRequest {
            ProfileId = profileId,
            DisplayName = displayName,
            Description = description,
            Mailbox = mailbox,
            DefaultSender = defaultSender,
            IsDefault = isDefault,
            ClientId = clientId,
            TenantId = tenantId,
            ClientSecret = clientSecret,
            ClientSecretReference = clientSecretReference,
            AccessToken = accessToken,
            AccessTokenReference = accessTokenReference,
            CertificatePath = certificatePath,
            CertificatePassword = certificatePassword,
            CertificatePasswordReference = certificatePasswordReference
        }, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded) {
            throw new InvalidOperationException(result.Message ?? $"Graph profile '{profileId}' could not be saved.");
        }

        return await mail_profile_get(profileId, cancellationToken).ConfigureAwait(false);
    }

    [McpServerTool]
    [Description("Creates or updates a Gmail profile using the shared Gmail bootstrap workflow.")]
    public async Task<MailProfile> mail_profile_gmail_bootstrap(
        [Description("The stable profile identifier to create or update.")] string profileId,
        [Description("The human-readable display name for the profile.")] string displayName,
        [Description("Optional Gmail mailbox address or user id. Defaults to 'me' when omitted.")] string? mailbox = null,
        [Description("Optional description for operators.")] string? description = null,
        [Description("Optional default sender email address. Defaults to the mailbox when appropriate.")] string? defaultSender = null,
        [Description("When true, marks this profile as the default profile.")] bool isDefault = false,
        [Description("Optional Google OAuth client identifier.")] string? clientId = null,
        [Description("Optional Google OAuth client secret to store securely. Prefer using clientSecretReference for MCP hosts.")] string? clientSecret = null,
        [Description("Optional secret reference in the form '<profile-id>:<secret-name>' or '<secret-name>' for the Gmail client secret.")] string? clientSecretReference = null,
        [Description("Optional Google OAuth refresh token to store securely. Prefer using refreshTokenReference for MCP hosts.")] string? refreshToken = null,
        [Description("Optional secret reference in the form '<profile-id>:<secret-name>' or '<secret-name>' for the Gmail refresh token.")] string? refreshTokenReference = null,
        [Description("Optional explicit access token to store securely. Prefer using accessTokenReference for MCP hosts.")] string? accessToken = null,
        [Description("Optional secret reference in the form '<profile-id>:<secret-name>' or '<secret-name>' for the Gmail access token.")] string? accessTokenReference = null,
        CancellationToken cancellationToken = default) {
        var result = await _application.ProfileBootstrap.SaveGmailProfileAsync(new GmailProfileBootstrapRequest {
            ProfileId = profileId,
            DisplayName = displayName,
            Description = description,
            Mailbox = mailbox,
            DefaultSender = defaultSender,
            IsDefault = isDefault,
            ClientId = clientId,
            ClientSecret = clientSecret,
            ClientSecretReference = clientSecretReference,
            RefreshToken = refreshToken,
            RefreshTokenReference = refreshTokenReference,
            AccessToken = accessToken,
            AccessTokenReference = accessTokenReference
        }, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded) {
            throw new InvalidOperationException(result.Message ?? $"Gmail profile '{profileId}' could not be saved.");
        }

        return await mail_profile_get(profileId, cancellationToken).ConfigureAwait(false);
    }

    [McpServerTool]
    [Description("Authenticates a saved Microsoft Graph profile using the shared interactive login workflow and persists the resulting token.")]
    public Task<MailProfileAuthenticationResult> mail_profile_graph_login(
        [Description("The saved Graph profile identifier to authenticate.")] string profileId,
        [Description("Optional login hint for the interactive flow.")] string? login = null,
        [Description("Optional mailbox override to persist on the profile.")] string? mailbox = null,
        [Description("Optional client/application identifier override.")] string? clientId = null,
        [Description("Optional tenant/directory identifier override.")] string? tenantId = null,
        [Description("Optional redirect URI override for the interactive flow.")] string? redirectUri = null,
        [Description("Optional scopes override.")] string[]? scopes = null,
        CancellationToken cancellationToken = default) =>
        _application.ProfileAuth.LoginGraphAsync(new GraphProfileLoginRequest {
            ProfileId = profileId,
            Login = login,
            Mailbox = mailbox,
            ClientId = clientId,
            TenantId = tenantId,
            RedirectUri = redirectUri,
            Scopes = scopes
        }, cancellationToken);

    [McpServerTool]
    [Description("Authenticates a saved Gmail profile using the shared interactive login workflow and persists the resulting tokens.")]
    public Task<MailProfileAuthenticationResult> mail_profile_gmail_login(
        [Description("The saved Gmail profile identifier to authenticate.")] string profileId,
        [Description("Optional Gmail account override used for the login flow.")] string? mailbox = null,
        [Description("Optional OAuth client identifier override.")] string? clientId = null,
        [Description("Optional OAuth client secret override. Prefer using clientSecretReference for MCP hosts.")] string? clientSecret = null,
        [Description("Optional secret reference in the form '<profile-id>:<secret-name>' or '<secret-name>' for the Gmail client secret override.")] string? clientSecretReference = null,
        [Description("Optional scopes override.")] string[]? scopes = null,
        CancellationToken cancellationToken = default) =>
        _application.ProfileAuth.LoginGmailAsync(new GmailProfileLoginRequest {
            ProfileId = profileId,
            GmailAccount = mailbox,
            ClientId = clientId,
            ClientSecret = clientSecret,
            ClientSecretReference = clientSecretReference,
            Scopes = scopes
        }, cancellationToken);

    [McpServerTool]
    [Description("Refreshes or reauthenticates a saved profile using its persisted Mailozaurr auth metadata.")]
    public Task<MailProfileAuthenticationResult> mail_profile_refresh_auth(
        [Description("The saved profile identifier to refresh.")] string profileId,
        CancellationToken cancellationToken = default) =>
        _application.ProfileAuth.RefreshAsync(profileId, cancellationToken);

    [McpServerTool]
    [Description("Returns the persisted authentication status for a saved Mailozaurr profile, including auth mode, token presence, and refreshability.")]
    public async Task<MailProfileAuthStatus> mail_profile_auth_status(
        [Description("The saved profile identifier to inspect.")] string profileId,
        CancellationToken cancellationToken = default) {
        var status = await _application.ProfileAuth.GetStatusAsync(profileId, cancellationToken).ConfigureAwait(false);
        return status ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");
    }

    [McpServerTool]
    [Description("Runs a live provider connection test for a saved Mailozaurr profile.")]
    public Task<MailProfileConnectionTestResult> mail_profile_test(
        [Description("The saved profile identifier to test.")] string profileId,
        [Description("Optional test scope: auto, auth, mailbox, or send.")] string? scope = null,
        CancellationToken cancellationToken = default) =>
        _application.ProfileConnections.TestAsync(profileId, ParseConnectionTestScope(scope), cancellationToken);

    [McpServerTool]
    [Description("Deletes a Mailozaurr profile from shared profile storage.")]
    public Task<OperationResult> mail_profile_delete(
        [Description("The profile identifier to delete.")] string profileId,
        CancellationToken cancellationToken = default) =>
        _application.Profiles.DeleteAsync(profileId, cancellationToken);

    [McpServerTool]
    [Description("Marks a Mailozaurr profile as the shared default profile.")]
    public Task<OperationResult> mail_profile_set_default(
        [Description("The profile identifier to mark as default.")] string profileId,
        CancellationToken cancellationToken = default) =>
        _application.Profiles.SetDefaultAsync(profileId, cancellationToken);

    [McpServerTool]
    [Description("Stores or replaces a secret for an existing Mailozaurr profile.")]
    public Task<OperationResult> mail_profile_secret_set(
        [Description("The profile identifier that owns the secret.")] string profileId,
        [Description("The stable secret name, such as password, client-secret, or refresh-token.")] string secretName,
        [Description("The secret value to store. Prefer using secretReference for MCP hosts when the secret already exists in the shared store.")] string? secretValue = null,
        [Description("Optional secret reference in the form '<profile-id>:<secret-name>' or '<secret-name>' to copy without re-exposing the secret value.")] string? secretReference = null,
        CancellationToken cancellationToken = default) =>
        _application.ProfileSecrets.SetSecretAsync(
            profileId,
            secretName,
            secretValue,
            secretReference,
            cancellationToken);

    [McpServerTool]
    [Description("Removes a secret associated with an existing Mailozaurr profile.")]
    public Task<OperationResult> mail_profile_secret_remove(
        [Description("The profile identifier that owns the secret.")] string profileId,
        [Description("The stable secret name to remove.")] string secretName,
        CancellationToken cancellationToken = default) =>
        _application.ProfileSecrets.RemoveSecretAsync(profileId, secretName, cancellationToken);
}