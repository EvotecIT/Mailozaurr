using System.ComponentModel;
using Mailozaurr.Application;
using ModelContextProtocol.Server;

namespace Mailozaurr.Cli.Mcp;

[McpServerToolType]
public sealed class MailMcpTools {
    private readonly MailApplication _application;

    public MailMcpTools(MailApplication application) {
        _application = application ?? throw new ArgumentNullException(nameof(application));
    }

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

    [McpServerTool]
    [Description("Lists folders or folder-like mailbox containers for a profile.")]
    public Task<IReadOnlyList<FolderRef>> mail_folders_list(
        [Description("The profile identifier to query.")] string profileId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional parent folder identifier to scope the listing.")] string? parentFolderId = null,
        [Description("When true, limits the result to root-level folders only.")] bool rootOnly = false,
        CancellationToken cancellationToken = default) =>
        _application.Read.GetFoldersAsync(new MailFolderQuery {
            ProfileId = profileId,
            MailboxId = mailboxId,
            ParentFolderId = parentFolderId,
            RootOnly = rootOnly
        }, cancellationToken);

    [McpServerTool]
    [Description("Lists folders or folder-like mailbox containers for a profile using a lightweight projection.")]
    public Task<IReadOnlyList<FolderRefCompact>> mail_folders_compact_list(
        [Description("The profile identifier to query.")] string profileId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional parent folder identifier to scope the listing.")] string? parentFolderId = null,
        [Description("When true, limits the result to root-level folders only.")] bool rootOnly = false,
        CancellationToken cancellationToken = default) =>
        _application.Read.GetFoldersCompactAsync(new MailFolderQuery {
            ProfileId = profileId,
            MailboxId = mailboxId,
            ParentFolderId = parentFolderId,
            RootOnly = rootOnly
        }, cancellationToken);

    [McpServerTool]
    [Description("Lists provider-neutral folder aliases for a profile, resolving them to provider folders when possible.")]
    public Task<IReadOnlyList<MailFolderAliasSummary>> mail_folder_aliases_list(
        [Description("The profile identifier to query.")] string profileId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        CancellationToken cancellationToken = default) =>
        _application.FolderAliases.GetAliasesAsync(profileId, mailboxId, cancellationToken);

    [McpServerTool]
    [Description("Resolves a requested folder target to a provider-neutral alias or an effective provider folder destination.")]
    public Task<MailFolderTargetResolution> mail_folder_resolve(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The requested target folder identifier or alias.")] string targetFolderId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        CancellationToken cancellationToken = default) =>
        _application.FolderAliases.ResolveAsync(profileId, targetFolderId, mailboxId, cancellationToken);

    [McpServerTool]
    [Description("Creates a normalized execution plan for a selected message action, with optional preview-token validation and resolved destinations.")]
    public Task<MessageActionExecutionPlan> mail_action_plan(
        [Description("The selected action, such as mark-read, mark-unread, flag, unflag, archive, trash, move, or delete.")] string action,
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to normalize.")] string[] messageIds,
        [Description("Optional custom destination folder identifier or alias when the action is move.")] string? destinationFolderId = null,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional source folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional confirmation token returned by a matching preview call.")] string? confirmationToken = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlans.CreatePlanAsync(new MessageActionExecutionPlanRequest {
            Action = action,
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            DestinationFolderId = destinationFolderId,
            ConfirmationToken = confirmationToken
        }, cancellationToken);

    [McpServerTool]
    [Description("Lists persisted reusable message action plan batches.")]
    public Task<IReadOnlyList<MailMessageActionPlanBatch>> mail_action_batch_store_list(
        [Description("Optional human-readable plan names that must exist in the returned batch.")] IReadOnlyList<string>? planNames = null,
        [Description("Optional profile identifiers that must be referenced by the returned batch.")] IReadOnlyList<string>? profileIds = null,
        [Description("Optional normalized action names that must be referenced by the returned batch.")] IReadOnlyList<string>? actions = null,
        [Description("Optional sort key: id, name, plans, ready, updated, or actions.")] string? sortBy = null,
        [Description("When true, reverses the selected sort order.")] bool descending = false,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.GetBatchesAsync(BuildBatchQuery(planNames, profileIds, actions, sortBy, descending), cancellationToken);

    [McpServerTool]
    [Description("Lists persisted reusable message action plan batches using a lightweight projection.")]
    public Task<IReadOnlyList<MailMessageActionPlanBatchCompact>> mail_action_batch_store_compact_list(
        [Description("Optional human-readable plan names that must exist in the returned batch.")] IReadOnlyList<string>? planNames = null,
        [Description("Optional profile identifiers that must be referenced by the returned batch.")] IReadOnlyList<string>? profileIds = null,
        [Description("Optional normalized action names that must be referenced by the returned batch.")] IReadOnlyList<string>? actions = null,
        [Description("Optional sort key: id, name, plans, ready, updated, or actions.")] string? sortBy = null,
        [Description("When true, reverses the selected sort order.")] bool descending = false,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.GetBatchesCompactAsync(BuildBatchQuery(planNames, profileIds, actions, sortBy, descending), cancellationToken);

    [McpServerTool]
    [Description("Lists persisted reusable message action plan batches using a richer summary projection.")]
    public Task<IReadOnlyList<MailMessageActionPlanBatchSummary>> mail_action_batch_store_summary_list(
        [Description("Optional human-readable plan names that must exist in the returned batch.")] IReadOnlyList<string>? planNames = null,
        [Description("Optional profile identifiers that must be referenced by the returned batch.")] IReadOnlyList<string>? profileIds = null,
        [Description("Optional normalized action names that must be referenced by the returned batch.")] IReadOnlyList<string>? actions = null,
        [Description("Optional sort key: id, name, plans, ready, updated, or actions.")] string? sortBy = null,
        [Description("When true, reverses the selected sort order.")] bool descending = false,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.GetBatchesSummaryAsync(BuildBatchQuery(planNames, profileIds, actions, sortBy, descending), cancellationToken);

    [McpServerTool]
    [Description("Gets one persisted reusable message action plan batch by identifier.")]
    public async Task<MailMessageActionPlanBatch> mail_action_batch_store_get(
        [Description("The persisted batch identifier to retrieve.")] string batchId,
        CancellationToken cancellationToken = default) {
        var batch = await _application.MessageActionPlanRegistry.GetBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        return batch ?? throw new InvalidOperationException($"Action plan batch '{batchId}' was not found.");
    }

    [McpServerTool]
    [Description("Gets one persisted reusable message action plan batch by identifier using a lightweight projection.")]
    public async Task<MailMessageActionPlanBatchCompact> mail_action_batch_store_compact_get(
        [Description("The persisted batch identifier to retrieve.")] string batchId,
        CancellationToken cancellationToken = default) {
        var batch = await _application.MessageActionPlanRegistry.GetBatchCompactAsync(batchId, cancellationToken).ConfigureAwait(false);
        return batch ?? throw new InvalidOperationException($"Action plan batch '{batchId}' was not found.");
    }

    [McpServerTool]
    [Description("Gets one persisted reusable message action plan batch by identifier using a richer summary projection.")]
    public async Task<MailMessageActionPlanBatchSummary> mail_action_batch_store_summary_get(
        [Description("The persisted batch identifier to retrieve.")] string batchId,
        CancellationToken cancellationToken = default) {
        var batch = await _application.MessageActionPlanRegistry.GetBatchSummaryAsync(batchId, cancellationToken).ConfigureAwait(false);
        return batch ?? throw new InvalidOperationException($"Action plan batch '{batchId}' was not found.");
    }

    [McpServerTool]
    [Description("Imports a persisted reusable message action plan batch from an external batch file.")]
    public Task<OperationResult> mail_action_batch_store_import(
        [Description("The persisted batch identifier to create or update.")] string batchId,
        [Description("The human-readable batch name.")] string name,
        [Description("The source batch file path on the server filesystem.")] string path,
        [Description("Optional operator-facing description.")] string? description = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.ImportAsync(batchId, name, path, description, cancellationToken);

    [McpServerTool]
    [Description("Exports a persisted reusable message action plan batch to an external batch file.")]
    public Task<OperationResult> mail_action_batch_store_export(
        [Description("The persisted batch identifier to export.")] string batchId,
        [Description("The destination batch file path on the server filesystem.")] string path,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.ExportAsync(batchId, path, cancellationToken);

    [McpServerTool]
    [Description("Builds and stores a reusable action plan batch from a common message selection and selected common actions.")]
    public Task<OperationResult> mail_action_batch_store_create_common(
        [Description("The persisted batch identifier to create or update.")] string batchId,
        [Description("The human-readable batch name.")] string name,
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to include.")] string[] messageIds,
        [Description("Optional subset of common actions to include, such as mark-read, archive, delete, or move. When omitted, all supported common actions are considered.")] string[]? actions = null,
        [Description("Optional custom destination folder identifier or alias when a generic move action should be included.")] string? destinationFolderId = null,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional source folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional operator-facing description.")] string? description = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.CreateCommonBatchAsync(
            batchId,
            name,
            new CommonMessageActionsPreviewRequest {
                ProfileId = profileId,
                MailboxId = mailboxId,
                FolderId = folderId,
                MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
                DestinationFolderId = destinationFolderId
            },
            actions?.Where(action => !string.IsNullOrWhiteSpace(action)).Select(action => action.Trim()).ToArray(),
            description,
            cancellationToken);

    [McpServerTool]
    [Description("Builds and stores a reusable action plan batch from an existing common action preview bundle and optional selected previewed actions.")]
    public Task<OperationResult> mail_action_batch_store_create_from_preview(
        [Description("The persisted batch identifier to create or update.")] string batchId,
        [Description("The human-readable batch name.")] string name,
        [Description("The previously generated common action preview bundle.")] CommonMessageActionsPreview preview,
        [Description("Optional subset of previewed actions to persist. When omitted, all supported previewed actions are used.")] string[]? actions = null,
        [Description("Optional operator-facing description.")] string? description = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.CreateCommonBatchFromPreviewAsync(
            batchId,
            name,
            preview,
            actions?.Where(action => !string.IsNullOrWhiteSpace(action)).Select(action => action.Trim()).ToArray(),
            description,
            cancellationToken);

    [McpServerTool]
    [Description("Clones an existing persisted reusable message action plan batch to a new identifier and name.")]
    public Task<OperationResult> mail_action_batch_store_clone(
        [Description("The persisted source batch identifier to clone.")] string sourceBatchId,
        [Description("The persisted target batch identifier to create.")] string targetBatchId,
        [Description("The human-readable name for the cloned batch.")] string name,
        [Description("Optional operator-facing description.")] string? description = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.CloneAsync(sourceBatchId, targetBatchId, name, description, cancellationToken);

    [McpServerTool]
    [Description("Clones an existing persisted reusable message action plan batch while applying shared profile, mailbox, folder, or destination transforms.")]
    public Task<OperationResult> mail_action_batch_store_transform_clone(
        [Description("The persisted source batch identifier to clone.")] string sourceBatchId,
        [Description("The persisted target batch identifier to create.")] string targetBatchId,
        [Description("The human-readable name for the transformed clone.")] string name,
        [Description("Optional zero-based plan indexes to include from the source batch. When omitted, all plans are included.")] int[]? indexes = null,
        [Description("Optional stored plan names to include from the source batch. When omitted, all names are included.")] string[]? planNames = null,
        [Description("Optional replacement profile identifier for every transformed plan.")] string? profileId = null,
        [Description("Optional replacement mailbox identifier for every transformed plan.")] string? mailboxId = null,
        [Description("Optional replacement source folder identifier for every transformed plan.")] string? folderId = null,
        [Description("Optional replacement destination folder identifier for move-like plans.")] string? destinationFolderId = null,
        [Description("Optional operator-facing description.")] string? description = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.TransformCloneAsync(
            sourceBatchId,
            targetBatchId,
            name,
            new MessageActionPlanBatchTransformRequest {
                PlanIndexes = indexes?.Distinct().ToList() ?? new List<int>(),
                PlanNames = planNames?.Where(nameValue => !string.IsNullOrWhiteSpace(nameValue)).Select(nameValue => nameValue.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>(),
                ProfileId = profileId,
                MailboxId = mailboxId,
                FolderId = folderId,
                DestinationFolderId = destinationFolderId
            },
            description,
            cancellationToken);

    [McpServerTool]
    [Description("Previews a stored action plan batch transform before cloning and saving it.")]
    public Task<MailMessageActionPlanBatchTransformPreview> mail_action_batch_store_transform_preview(
        [Description("The persisted source batch identifier to preview.")] string sourceBatchId,
        [Description("Optional zero-based plan indexes to include from the source batch. When omitted, all plans are included.")] int[]? indexes = null,
        [Description("Optional stored plan names to include from the source batch. When omitted, all names are included.")] string[]? planNames = null,
        [Description("Optional replacement profile identifier for every transformed plan.")] string? profileId = null,
        [Description("Optional replacement mailbox identifier for every transformed plan.")] string? mailboxId = null,
        [Description("Optional replacement source folder identifier for every transformed plan.")] string? folderId = null,
        [Description("Optional replacement destination folder identifier for move-like plans.")] string? destinationFolderId = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.PreviewTransformCloneAsync(
            sourceBatchId,
            new MessageActionPlanBatchTransformRequest {
                PlanIndexes = indexes?.Distinct().ToList() ?? new List<int>(),
                PlanNames = planNames?.Where(nameValue => !string.IsNullOrWhiteSpace(nameValue)).Select(nameValue => nameValue.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>(),
                ProfileId = profileId,
                MailboxId = mailboxId,
                FolderId = folderId,
                DestinationFolderId = destinationFolderId
            },
            cancellationToken);

    [McpServerTool]
    [Description("Appends one newly planned normalized action to an existing persisted batch.")]
    public async Task<OperationResult> mail_action_batch_store_append_plan(
        [Description("The persisted batch identifier to update.")] string batchId,
        [Description("The selected action, such as mark-read, mark-unread, flag, unflag, archive, trash, move, or delete.")] string action,
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to normalize.")] string[] messageIds,
        [Description("Optional custom destination folder identifier or alias when the action is move.")] string? destinationFolderId = null,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional source folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional confirmation token returned by a matching preview call.")] string? confirmationToken = null,
        CancellationToken cancellationToken = default) {
        var plan = await mail_action_plan(
            action,
            profileId,
            messageIds,
            destinationFolderId,
            mailboxId,
            folderId,
            confirmationToken,
            cancellationToken).ConfigureAwait(false);

        return await _application.MessageActionPlanRegistry.AppendPlanAsync(batchId, plan, cancellationToken).ConfigureAwait(false);
    }

    [McpServerTool]
    [Description("Loads one normalized action plan from a file and appends it to an existing persisted batch.")]
    public Task<OperationResult> mail_action_batch_store_append_imported_plan(
        [Description("The persisted batch identifier to update.")] string batchId,
        [Description("The source plan file path on the server filesystem.")] string path,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.AppendImportedPlanAsync(batchId, path, cancellationToken);

    [McpServerTool]
    [Description("Replaces one stored plan in an existing persisted batch using a newly planned normalized action.")]
    public async Task<OperationResult> mail_action_batch_store_replace_plan(
        [Description("The persisted batch identifier to update.")] string batchId,
        [Description("The zero-based plan index to replace.")] int index,
        [Description("The selected action, such as mark-read, mark-unread, flag, unflag, archive, trash, move, or delete.")] string action,
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to normalize.")] string[] messageIds,
        [Description("Optional custom destination folder identifier or alias when the action is move.")] string? destinationFolderId = null,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional source folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional confirmation token returned by a matching preview call.")] string? confirmationToken = null,
        CancellationToken cancellationToken = default) {
        var plan = await mail_action_plan(
            action,
            profileId,
            messageIds,
            destinationFolderId,
            mailboxId,
            folderId,
            confirmationToken,
            cancellationToken).ConfigureAwait(false);

        return await _application.MessageActionPlanRegistry.ReplacePlanAtAsync(batchId, index, plan, cancellationToken).ConfigureAwait(false);
    }

    [McpServerTool]
    [Description("Loads one normalized action plan from a file and replaces a stored plan by zero-based index.")]
    public Task<OperationResult> mail_action_batch_store_replace_imported_plan(
        [Description("The persisted batch identifier to update.")] string batchId,
        [Description("The zero-based plan index to replace.")] int index,
        [Description("The source plan file path on the server filesystem.")] string path,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.ReplaceImportedPlanAtAsync(batchId, index, path, cancellationToken);

    [McpServerTool]
    [Description("Removes one plan from an existing persisted batch by zero-based index.")]
    public Task<OperationResult> mail_action_batch_store_remove_plan(
        [Description("The persisted batch identifier to update.")] string batchId,
        [Description("The zero-based plan index to remove.")] int index,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.RemovePlanAtAsync(batchId, index, cancellationToken);

    [McpServerTool]
    [Description("Deletes a persisted reusable message action plan batch.")]
    public Task<OperationResult> mail_action_batch_store_delete(
        [Description("The persisted batch identifier to delete.")] string batchId,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.DeleteAsync(batchId, cancellationToken);

    [McpServerTool]
    [Description("Executes a persisted reusable message action plan batch through the shared batch-execution service.")]
    public Task<MessageActionBatchExecutionResult> mail_action_batch_store_execute(
        [Description("The persisted batch identifier to execute.")] string batchId,
        [Description("When true, continues after failures. When false, later plans are skipped after the first failure.")] bool continueOnError = true,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.ExecuteAsync(batchId, continueOnError, cancellationToken);

    [McpServerTool]
    [Description("Creates a normalized action plan and exports it to a file through the shared Mailozaurr plan-exchange service.")]
    public async Task<OperationResult> mail_action_plan_export(
        [Description("The selected action, such as mark-read, mark-unread, flag, unflag, archive, trash, move, or delete.")] string action,
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to normalize.")] string[] messageIds,
        [Description("The destination file path on the server filesystem.")] string path,
        [Description("Optional custom destination folder identifier or alias when the action is move.")] string? destinationFolderId = null,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional source folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional confirmation token returned by a matching preview call.")] string? confirmationToken = null,
        CancellationToken cancellationToken = default) {
        var plan = await mail_action_plan(
            action,
            profileId,
            messageIds,
            destinationFolderId,
            mailboxId,
            folderId,
            confirmationToken,
            cancellationToken).ConfigureAwait(false);

        await _application.MessageActionPlanExchange.SaveAsync(path, plan, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success("Action plan exported.");
    }

    [McpServerTool]
    [Description("Loads one normalized action plan from a file through the shared Mailozaurr plan-exchange service.")]
    public Task<MessageActionExecutionPlan> mail_action_plan_import(
        [Description("The source file path on the server filesystem.")] string path,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanExchange.LoadAsync(path, cancellationToken);

    [McpServerTool]
    [Description("Exports a batch of normalized action plans to a file through the shared Mailozaurr plan-exchange service.")]
    public async Task<OperationResult> mail_action_batch_export(
        [Description("The destination file path on the server filesystem.")] string path,
        [Description("The normalized action plans to export.")] MessageActionExecutionPlan[] plans,
        CancellationToken cancellationToken = default) {
        await _application.MessageActionPlanExchange.SaveBatchAsync(
            path,
            plans?.ToList() ?? new List<MessageActionExecutionPlan>(),
            cancellationToken).ConfigureAwait(false);
        return OperationResult.Success("Action plan batch exported.");
    }

    [McpServerTool]
    [Description("Loads a batch of normalized action plans from a file through the shared Mailozaurr plan-exchange service.")]
    public Task<IReadOnlyList<MessageActionExecutionPlan>> mail_action_batch_import(
        [Description("The source file path on the server filesystem.")] string path,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanExchange.LoadBatchAsync(path, cancellationToken);

    [McpServerTool]
    [Description("Creates and executes one normalized message action plan through the shared Mailozaurr planning and batch-execution services.")]
    public async Task<MessageActionBatchExecutionResult> mail_action_execute(
        [Description("The selected action, such as mark-read, mark-unread, flag, unflag, archive, trash, move, or delete.")] string action,
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to normalize and execute.")] string[] messageIds,
        [Description("Optional custom destination folder identifier or alias when the action is move.")] string? destinationFolderId = null,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional source folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional confirmation token returned by a matching preview call.")] string? confirmationToken = null,
        CancellationToken cancellationToken = default) {
        var plan = await _application.MessageActionPlans.CreatePlanAsync(new MessageActionExecutionPlanRequest {
            Action = action,
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            DestinationFolderId = destinationFolderId,
            ConfirmationToken = confirmationToken
        }, cancellationToken).ConfigureAwait(false);

        return await _application.MessageActionBatch.ExecuteAsync(new[] { plan }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    [McpServerTool]
    [Description("Executes a batch of prebuilt normalized message action plans through the shared Mailozaurr batch-execution service.")]
    public Task<MessageActionBatchExecutionResult> mail_action_batch_execute(
        [Description("The normalized action plans to execute.")] MessageActionExecutionPlan[] plans,
        [Description("When true, continues after failures. When false, later plans are skipped after the first failure.")] bool continueOnError = true,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionBatch.ExecuteAsync(
            plans?.ToList() ?? new List<MessageActionExecutionPlan>(),
            continueOnError,
            cancellationToken);

    [McpServerTool]
    [Description("Builds a dry-run bundle of common message actions, including read/unread, flag/unflag, archive, trash, delete, and an optional custom move target.")]
    public Task<CommonMessageActionsPreview> mail_actions_bundle_preview(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to preview.")] string[] messageIds,
        [Description("Optional custom destination folder identifier or alias to include as a generic move preview.")] string? destinationFolderId = null,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional source folder identifier when the provider requires folder scoping.")] string? folderId = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPreview.PreviewCommonActionsAsync(new CommonMessageActionsPreviewRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            DestinationFolderId = destinationFolderId
        }, cancellationToken);

    [McpServerTool]
    [Description("Builds a dry-run comparison for standard message actions such as archive, trash, delete, and an optional custom move target.")]
    public Task<StandardMessageActionsPreview> mail_actions_preview(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to preview.")] string[] messageIds,
        [Description("Optional custom destination folder identifier or alias to include as a generic move preview.")] string? destinationFolderId = null,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional source folder identifier when the provider requires folder scoping.")] string? folderId = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPreview.PreviewStandardActionsAsync(new StandardMessageActionsPreviewRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            DestinationFolderId = destinationFolderId
        }, cancellationToken);

    [McpServerTool]
    [Description("Builds a dry-run preview for moving messages, including normalized message ids and the effective destination folder target.")]
    public Task<MoveMessagesPreview> mail_move_preview(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to preview.")] string[] messageIds,
        [Description("The requested destination folder identifier or alias.")] string destinationFolderId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional source folder identifier when the provider requires folder scoping.")] string? folderId = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPreview.PreviewMoveAsync(new MoveMessagesPreviewRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            DestinationFolderId = destinationFolderId
        }, cancellationToken);

    [McpServerTool]
    [Description("Builds a dry-run preview for deleting messages, including normalized message ids before execution.")]
    public Task<DeleteMessagesPreview> mail_delete_preview(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to preview.")] string[] messageIds,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional source folder identifier when the provider requires folder scoping.")] string? folderId = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPreview.PreviewDeleteAsync(new DeleteMessagesPreviewRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>()
        }, cancellationToken);

    [McpServerTool]
    [Description("Searches messages in a mailbox using normalized Mailozaurr filters.")]
    public Task<IReadOnlyList<MessageSummary>> mail_search(
        [Description("The profile identifier to query.")] string profileId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier to scope the search.")] string? folderId = null,
        [Description("Optional free-text query to match across provider-specific searchable content.")] string? queryText = null,
        [Description("Optional subject text filter.")] string? subjectContains = null,
        [Description("Optional sender text filter.")] string? fromContains = null,
        [Description("Optional recipient text filter.")] string? toContains = null,
        [Description("When true, only returns messages with attachments.")] bool hasAttachments = false,
        [Description("Optional maximum number of messages to return.")] int? limit = null,
        CancellationToken cancellationToken = default) =>
        _application.Read.SearchAsync(new MailSearchRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            QueryText = queryText,
            SubjectContains = subjectContains,
            FromContains = fromContains,
            ToContains = toContains,
            HasAttachments = hasAttachments,
            Limit = limit
        }, cancellationToken);

    [McpServerTool]
    [Description("Searches messages in a mailbox using a lightweight Mailozaurr message projection.")]
    public Task<IReadOnlyList<MessageSummaryCompact>> mail_search_compact(
        [Description("The profile identifier to query.")] string profileId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier to scope the search.")] string? folderId = null,
        [Description("Optional free-text query to match across provider-specific searchable content.")] string? queryText = null,
        [Description("Optional subject text filter.")] string? subjectContains = null,
        [Description("Optional sender text filter.")] string? fromContains = null,
        [Description("Optional recipient text filter.")] string? toContains = null,
        [Description("When true, only returns messages with attachments.")] bool hasAttachments = false,
        [Description("Optional maximum number of messages to return.")] int? limit = null,
        CancellationToken cancellationToken = default) =>
        _application.Read.SearchCompactAsync(new MailSearchRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            QueryText = queryText,
            SubjectContains = subjectContains,
            FromContains = fromContains,
            ToContains = toContains,
            HasAttachments = hasAttachments,
            Limit = limit
        }, cancellationToken);

    [McpServerTool]
    [Description("Gets a detailed message view for a specific message identifier.")]
    public async Task<MessageDetail> mail_get(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifier to retrieve.")] string messageId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("When true, includes raw provider content where supported.")] bool includeRawContent = false,
        CancellationToken cancellationToken = default) {
        var message = await _application.Read.GetMessageAsync(new GetMessageRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageId = messageId,
            IncludeRawContent = includeRawContent
        }, cancellationToken).ConfigureAwait(false);

        return message ?? throw new InvalidOperationException($"Message '{messageId}' was not found.");
    }

    [McpServerTool]
    [Description("Gets a lightweight detailed message view for a specific message identifier.")]
    public async Task<MessageDetailCompact> mail_get_compact(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifier to retrieve.")] string messageId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("When true, allows the provider to include raw-content presence in the projection.")] bool includeRawContent = false,
        CancellationToken cancellationToken = default) {
        var message = await _application.Read.GetMessageCompactAsync(new GetMessageRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageId = messageId,
            IncludeRawContent = includeRawContent
        }, cancellationToken).ConfigureAwait(false);

        return message ?? throw new InvalidOperationException($"Message '{messageId}' was not found.");
    }

    [McpServerTool]
    [Description("Gets detailed message views for multiple specific message identifiers.")]
    public Task<IReadOnlyList<MessageDetail>> mail_get_many(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to retrieve.")] string[] messageIds,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("When true, includes raw provider content where supported.")] bool includeRawContent = false,
        CancellationToken cancellationToken = default) =>
        _application.Read.GetMessagesAsync(new GetMessagesRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            IncludeRawContent = includeRawContent
        }, cancellationToken);

    [McpServerTool]
    [Description("Gets lightweight detailed message views for multiple specific message identifiers.")]
    public Task<IReadOnlyList<MessageDetailCompact>> mail_get_many_compact(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to retrieve.")] string[] messageIds,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("When true, includes raw-content presence where supported.")] bool includeRawContent = false,
        CancellationToken cancellationToken = default) =>
        _application.Read.GetMessagesCompactAsync(new GetMessagesRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            IncludeRawContent = includeRawContent
        }, cancellationToken);

    [McpServerTool]
    [Description("Builds a dry-run preview for changing messages to read or unread, including normalized message ids and a reusable confirmation token.")]
    public Task<MessageStateChangePreview> mail_mark_read_preview(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to preview.")] string[] messageIds,
        [Description("Desired read state. True marks as read; false marks as unread.")] bool isRead = true,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPreview.PreviewReadStateAsync(new SetReadStateRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            IsRead = isRead
        }, cancellationToken);

    [McpServerTool]
    [Description("Marks one or more messages as read or unread using the shared Mailozaurr message-action service.")]
    public Task<MessageActionResult> mail_mark_read(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to update.")] string[] messageIds,
        [Description("Desired read state. True marks as read; false marks as unread.")] bool isRead = true,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional confirmation token returned by a matching preview call.")] string? confirmationToken = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActions.SetReadStateAsync(new SetReadStateRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            IsRead = isRead,
            ConfirmationToken = confirmationToken
        }, cancellationToken);

    [McpServerTool]
    [Description("Builds a dry-run preview for flagging or unflagging messages, including normalized message ids and a reusable confirmation token.")]
    public Task<MessageStateChangePreview> mail_flag_preview(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to preview.")] string[] messageIds,
        [Description("Desired flagged state. True flags/star-marks; false unflags.")] bool isFlagged = true,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPreview.PreviewFlaggedStateAsync(new SetFlaggedStateRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            IsFlagged = isFlagged
        }, cancellationToken);

    [McpServerTool]
    [Description("Flags or unflags one or more messages using the shared Mailozaurr message-action service.")]
    public Task<MessageActionResult> mail_flag(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to update.")] string[] messageIds,
        [Description("Desired flagged state. True flags/star-marks; false unflags.")] bool isFlagged = true,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional confirmation token returned by a matching preview call.")] string? confirmationToken = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActions.SetFlaggedStateAsync(new SetFlaggedStateRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            IsFlagged = isFlagged,
            ConfirmationToken = confirmationToken
        }, cancellationToken);

    [McpServerTool]
    [Description("Archives one or more messages using the shared Mailozaurr message-action service and a provider-neutral Archive alias.")]
    public Task<MessageActionResult> mail_archive(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to archive.")] string[] messageIds,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional source folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional confirmation token returned by a matching preview call.")] string? confirmationToken = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActions.MoveAsync(new MoveMessagesRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            DestinationFolderId = MailFolderAliases.Archive,
            ConfirmationToken = confirmationToken
        }, cancellationToken);

    [McpServerTool]
    [Description("Moves one or more messages to trash using the shared Mailozaurr message-action service and a provider-neutral Trash alias.")]
    public Task<MessageActionResult> mail_trash(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to trash.")] string[] messageIds,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional source folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional confirmation token returned by a matching preview call.")] string? confirmationToken = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActions.MoveAsync(new MoveMessagesRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            DestinationFolderId = MailFolderAliases.Trash,
            ConfirmationToken = confirmationToken
        }, cancellationToken);

    [McpServerTool]
    [Description("Moves one or more messages to a destination folder using the shared Mailozaurr message-action service.")]
    public Task<MessageActionResult> mail_move(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to move.")] string[] messageIds,
        [Description("Destination folder identifier or provider alias.")] string destinationFolderId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional source folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional confirmation token returned by a matching preview call.")] string? confirmationToken = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActions.MoveAsync(new MoveMessagesRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            DestinationFolderId = destinationFolderId,
            ConfirmationToken = confirmationToken
        }, cancellationToken);

    [McpServerTool]
    [Description("Deletes one or more messages using the shared Mailozaurr message-action service.")]
    public Task<MessageActionResult> mail_delete(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to delete.")] string[] messageIds,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional confirmation token returned by a matching preview call.")] string? confirmationToken = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActions.DeleteAsync(new DeleteMessagesRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            ConfirmationToken = confirmationToken
        }, cancellationToken);

    [McpServerTool]
    [Description("Lists attachment metadata for a specific message without returning the full message body.")]
    public Task<IReadOnlyList<AttachmentSummary>> mail_attachments_list(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifier that owns the attachments.")] string messageId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        CancellationToken cancellationToken = default) =>
        _application.Read.GetAttachmentsAsync(new ListAttachmentsRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageId = messageId
        }, cancellationToken);

    [McpServerTool]
    [Description("Saves a message attachment to a local path that the Mailozaurr server can access.")]
    public Task<OperationResult> mail_attachment_save(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifier that owns the attachment.")] string messageId,
        [Description("The provider-specific attachment identifier to save.")] string attachmentId,
        [Description("The destination file path on the server filesystem.")] string destinationPath,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("When true, allows an existing destination file to be overwritten.")] bool overwrite = false,
        CancellationToken cancellationToken = default) =>
        _application.Read.SaveAttachmentAsync(new SaveAttachmentRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageId = messageId,
            AttachmentId = attachmentId,
            DestinationPath = destinationPath,
            Overwrite = overwrite
        }, cancellationToken);

    [McpServerTool]
    [Description("Saves one or more attachments from a message using shared Mailozaurr filtering and batching logic.")]
    public Task<SaveAttachmentsResult> mail_attachments_save(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifier that owns the attachments.")] string messageId,
        [Description("The destination path or directory on the server filesystem.")] string destinationPath,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional explicit attachment identifiers to save.")] string[]? attachmentIds = null,
        [Description("Optional case-insensitive file-name filter.")] string? fileNameContains = null,
        [Description("Optional case-insensitive content-type filter.")] string? contentTypeContains = null,
        [Description("When true, allows existing destination files to be overwritten.")] bool overwrite = false,
        CancellationToken cancellationToken = default) =>
        _application.Read.SaveAttachmentsAsync(new SaveAttachmentsRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageId = messageId,
            DestinationPath = destinationPath,
            AttachmentIds = attachmentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            FileNameContains = fileNameContains,
            ContentTypeContains = contentTypeContains,
            Overwrite = overwrite
        }, cancellationToken);

    [McpServerTool]
    [Description("Saves attachments from multiple messages using shared Mailozaurr filtering and batching logic.")]
    public Task<SaveAttachmentsManyResult> mail_attachments_save_many(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers that own the attachments.")] string[] messageIds,
        [Description("The destination path or directory on the server filesystem.")] string destinationPath,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional explicit attachment identifiers to save.")] string[]? attachmentIds = null,
        [Description("Optional case-insensitive file-name filter.")] string? fileNameContains = null,
        [Description("Optional case-insensitive content-type filter.")] string? contentTypeContains = null,
        [Description("When true, allows existing destination files to be overwritten.")] bool overwrite = false,
        CancellationToken cancellationToken = default) =>
        _application.Read.SaveAttachmentsManyAsync(new SaveAttachmentsManyRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            DestinationPath = destinationPath,
            AttachmentIds = attachmentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            FileNameContains = fileNameContains,
            ContentTypeContains = contentTypeContains,
            Overwrite = overwrite
        }, cancellationToken);

    [McpServerTool]
    [Description("Sends or queues a message using a configured Mailozaurr profile. Queueing is the default unless sendNow is true.")]
    public Task<SendResult> mail_send(
        [Description("The profile identifier to use for sending.")] string profileId,
        [Description("Primary recipient email addresses.")] string[] to,
        [Description("Optional subject line.")] string? subject = null,
        [Description("Optional plain text body.")] string? textBody = null,
        [Description("Optional HTML body.")] string? htmlBody = null,
        [Description("Optional CC recipient email addresses.")] string[]? cc = null,
        [Description("Optional BCC recipient email addresses.")] string[]? bcc = null,
        [Description("Optional Reply-To recipient email addresses.")] string[]? replyTo = null,
        [Description("Optional From email address override.")] string? from = null,
        [Description("Optional attachment file paths on the server filesystem.")] string[]? attachmentPaths = null,
        [Description("When true, sends immediately instead of preferring the queue.")] bool sendNow = false,
        CancellationToken cancellationToken = default) =>
        _application.Send.SendAsync(new SendMessageRequest {
            ProfileId = profileId,
            PreferQueue = !sendNow,
            RequireImmediateSend = sendNow,
            Message = BuildDraftMessage(profileId, to, subject, textBody, htmlBody, cc, bcc, replyTo, from, attachmentPaths)
        }, cancellationToken);

    [McpServerTool]
    [Description("Lists reusable drafts stored by Mailozaurr.")]
    public Task<IReadOnlyList<MailDraft>> mail_draft_list(CancellationToken cancellationToken = default) =>
        _application.Drafts.GetDraftsAsync(cancellationToken);

    [McpServerTool]
    [Description("Lists reusable drafts stored by Mailozaurr using a lightweight projection.")]
    public Task<IReadOnlyList<MailDraftCompact>> mail_draft_compact_list(CancellationToken cancellationToken = default) =>
        _application.Drafts.GetDraftsCompactAsync(cancellationToken);

    [McpServerTool]
    [Description("Gets a reusable draft by identifier.")]
    public async Task<MailDraft> mail_draft_get(
        [Description("The stored draft identifier to retrieve.")] string draftId,
        CancellationToken cancellationToken = default) {
        var draft = await _application.Drafts.GetDraftAsync(draftId, cancellationToken).ConfigureAwait(false);
        return draft ?? throw new InvalidOperationException($"Draft '{draftId}' was not found.");
    }

    [McpServerTool]
    [Description("Gets a reusable draft by identifier using a lightweight projection.")]
    public async Task<MailDraftCompact> mail_draft_compact_get(
        [Description("The stored draft identifier to retrieve.")] string draftId,
        CancellationToken cancellationToken = default) {
        var draft = await _application.Drafts.GetDraftCompactAsync(draftId, cancellationToken).ConfigureAwait(false);
        return draft ?? throw new InvalidOperationException($"Draft '{draftId}' was not found.");
    }

    [McpServerTool]
    [Description("Creates or updates a reusable draft using the shared Mailozaurr draft store.")]
    public Task<OperationResult> mail_draft_save(
        [Description("The stable draft identifier to create or update.")] string draftId,
        [Description("A human-readable draft name.")] string name,
        [Description("The profile identifier that should eventually send this draft.")] string profileId,
        [Description("Primary recipient email addresses.")] string[] to,
        [Description("Optional subject line.")] string? subject = null,
        [Description("Optional plain text body.")] string? textBody = null,
        [Description("Optional HTML body.")] string? htmlBody = null,
        [Description("Optional CC recipient email addresses.")] string[]? cc = null,
        [Description("Optional BCC recipient email addresses.")] string[]? bcc = null,
        [Description("Optional Reply-To recipient email addresses.")] string[]? replyTo = null,
        [Description("Optional From email address override.")] string? from = null,
        [Description("Optional attachment file paths on the server filesystem.")] string[]? attachmentPaths = null,
        CancellationToken cancellationToken = default) =>
        _application.Drafts.SaveAsync(new MailDraft {
            Id = draftId,
            Name = name,
            Message = BuildDraftMessage(profileId, to, subject, textBody, htmlBody, cc, bcc, replyTo, from, attachmentPaths)
        }, cancellationToken);

    [McpServerTool]
    [Description("Deletes a reusable draft from the shared Mailozaurr draft store.")]
    public Task<OperationResult> mail_draft_delete(
        [Description("The stored draft identifier to delete.")] string draftId,
        CancellationToken cancellationToken = default) =>
        _application.Drafts.DeleteAsync(draftId, cancellationToken);

    [McpServerTool]
    [Description("Imports a draft JSON file into the shared Mailozaurr draft store.")]
    public async Task<MailDraft> mail_draft_import(
        [Description("The draft file path on the server filesystem.")] string path,
        [Description("Optional replacement draft identifier to use after import.")] string? draftId = null,
        [Description("Optional replacement draft name to use after import.")] string? name = null,
        CancellationToken cancellationToken = default) {
        var draft = await _application.DraftExchange.LoadAsync(path, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(draftId)) {
            draft.Id = draftId.Trim();
        }
        if (!string.IsNullOrWhiteSpace(name)) {
            draft.Name = name.Trim();
        }

        var result = await _application.Drafts.SaveAsync(draft, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded) {
            throw new InvalidOperationException(result.Message ?? $"Draft '{draft.Id}' could not be imported.");
        }

        return draft;
    }

    [McpServerTool]
    [Description("Exports a stored Mailozaurr draft to a draft JSON file.")]
    public async Task<OperationResult> mail_draft_export(
        [Description("The stored draft identifier to export.")] string draftId,
        [Description("The destination draft file path on the server filesystem.")] string path,
        CancellationToken cancellationToken = default) {
        var draft = await _application.Drafts.GetDraftAsync(draftId, cancellationToken).ConfigureAwait(false);
        if (draft == null) {
            throw new InvalidOperationException($"Draft '{draftId}' was not found.");
        }

        await _application.DraftExchange.SaveAsync(path, draft, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success("Draft exported.");
    }

    [McpServerTool]
    [Description("Sends or queues a previously stored Mailozaurr draft. Queueing is the default unless sendNow is true.")]
    public async Task<SendResult> mail_draft_send(
        [Description("The stored draft identifier to send.")] string draftId,
        [Description("When true, sends immediately instead of preferring the queue.")] bool sendNow = false,
        CancellationToken cancellationToken = default) {
        var draft = await _application.Drafts.GetDraftAsync(draftId, cancellationToken).ConfigureAwait(false);
        if (draft == null) {
            throw new InvalidOperationException($"Draft '{draftId}' was not found.");
        }

        return await _application.Send.SendAsync(new SendMessageRequest {
            ProfileId = draft.Message.ProfileId,
            PreferQueue = !sendNow,
            RequireImmediateSend = sendNow,
            Message = CloneDraftMessage(draft.Message)
        }, cancellationToken).ConfigureAwait(false);
    }

    [McpServerTool]
    [Description("Lists outbound messages currently waiting in Mailozaurr's pending queue.")]
    public Task<IReadOnlyList<QueuedMessageSummary>> mail_queue_list(CancellationToken cancellationToken = default) =>
        _application.Queue.ListAsync(cancellationToken);

    [McpServerTool]
    [Description("Lists outbound messages currently waiting in Mailozaurr's pending queue using a lightweight projection.")]
    public Task<IReadOnlyList<QueuedMessageCompact>> mail_queue_compact_list(CancellationToken cancellationToken = default) =>
        _application.Queue.ListCompactAsync(cancellationToken);

    [McpServerTool]
    [Description("Gets a queued outbound message by identifier.")]
    public async Task<QueuedMessageSummary> mail_queue_get(
        [Description("The queued message identifier to inspect.")] string messageId,
        CancellationToken cancellationToken = default) {
        var message = await _application.Queue.GetAsync(messageId, cancellationToken).ConfigureAwait(false);
        return message ?? throw new InvalidOperationException($"Queued message '{messageId}' was not found.");
    }

    [McpServerTool]
    [Description("Gets a queued outbound message by identifier using a lightweight projection.")]
    public async Task<QueuedMessageCompact> mail_queue_compact_get(
        [Description("The queued message identifier to inspect.")] string messageId,
        CancellationToken cancellationToken = default) {
        var message = await _application.Queue.GetCompactAsync(messageId, cancellationToken).ConfigureAwait(false);
        return message ?? throw new InvalidOperationException($"Queued message '{messageId}' was not found.");
    }

    [McpServerTool]
    [Description("Removes a queued outbound message without sending it.")]
    public Task<OperationResult> mail_queue_remove(
        [Description("The queued message identifier to remove.")] string messageId,
        CancellationToken cancellationToken = default) =>
        _application.Queue.RemoveAsync(messageId, cancellationToken);

    [McpServerTool]
    [Description("Processes all due queued outbound messages.")]
    public Task<QueueProcessResult> mail_queue_process(CancellationToken cancellationToken = default) =>
        _application.Queue.ProcessAsync(cancellationToken);

    private static DraftMessage BuildDraftMessage(
        string profileId,
        IEnumerable<string> to,
        string? subject,
        string? textBody,
        string? htmlBody,
        IEnumerable<string>? cc,
        IEnumerable<string>? bcc,
        IEnumerable<string>? replyTo,
        string? from,
        IEnumerable<string>? attachmentPaths) {
        var draft = new DraftMessage {
            ProfileId = profileId,
            Subject = subject,
            TextBody = textBody,
            HtmlBody = htmlBody
        };

        if (!string.IsNullOrWhiteSpace(from)) {
            draft.From = new MessageRecipient {
                Address = from.Trim()
            };
        }

        AddRecipients(draft.To, to);
        AddRecipients(draft.Cc, cc);
        AddRecipients(draft.Bcc, bcc);
        AddRecipients(draft.ReplyTo, replyTo);

        if (attachmentPaths != null) {
            foreach (var attachmentPath in attachmentPaths.Where(path => !string.IsNullOrWhiteSpace(path))) {
                draft.Attachments.Add(new DraftAttachment {
                    Path = attachmentPath.Trim()
                });
            }
        }

        return draft;
    }

    private static DraftMessage CloneDraftMessage(DraftMessage draft) => new() {
        ProfileId = draft.ProfileId,
        From = draft.From == null ? null : new MessageRecipient {
            Name = draft.From.Name,
            Address = draft.From.Address
        },
        To = draft.To.Select(ToRecipientCopy).ToList(),
        Cc = draft.Cc.Select(ToRecipientCopy).ToList(),
        Bcc = draft.Bcc.Select(ToRecipientCopy).ToList(),
        ReplyTo = draft.ReplyTo.Select(ToRecipientCopy).ToList(),
        Subject = draft.Subject,
        TextBody = draft.TextBody,
        HtmlBody = draft.HtmlBody,
        Headers = new Dictionary<string, string>(draft.Headers, StringComparer.OrdinalIgnoreCase),
        Attachments = draft.Attachments.Select(attachment => new DraftAttachment {
            Path = attachment.Path,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            IsInline = attachment.IsInline,
            ContentId = attachment.ContentId
        }).ToList()
    };

    private static void AddRecipients(ICollection<MessageRecipient> destination, IEnumerable<string>? addresses) {
        if (addresses == null) {
            return;
        }

        foreach (var address in addresses.Where(value => !string.IsNullOrWhiteSpace(value))) {
            destination.Add(new MessageRecipient {
                Address = address.Trim()
            });
        }
    }

    private static MessageRecipient ToRecipientCopy(MessageRecipient recipient) => new() {
        Name = recipient.Name,
        Address = recipient.Address
    };

    private static MailMessageActionPlanBatchQuery? BuildBatchQuery(IReadOnlyList<string>? planNames, IReadOnlyList<string>? profileIds, IReadOnlyList<string>? actions, string? sortBy, bool descending) {
        var normalizedPlanNames = (planNames ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var normalizedProfileIds = (profileIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var normalizedActions = (actions ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var hasExplicitSort = !string.IsNullOrWhiteSpace(sortBy);
        var parsedSortBy = ParseBatchSortBy(sortBy);

        if (normalizedPlanNames.Count == 0 && normalizedProfileIds.Count == 0 && normalizedActions.Count == 0 && !hasExplicitSort && parsedSortBy == MailMessageActionPlanBatchSortBy.Id && !descending) {
            return null;
        }

        return new MailMessageActionPlanBatchQuery {
            PlanNames = normalizedPlanNames,
            ProfileIds = normalizedProfileIds,
            Actions = normalizedActions,
            SortBy = parsedSortBy,
            Descending = descending
        };
    }

    private static MailMessageActionPlanBatchSortBy ParseBatchSortBy(string? rawSortBy) {
        if (string.IsNullOrWhiteSpace(rawSortBy)) {
            return MailMessageActionPlanBatchSortBy.Id;
        }

        return rawSortBy.Trim().ToLowerInvariant() switch {
            "id" => MailMessageActionPlanBatchSortBy.Id,
            "name" => MailMessageActionPlanBatchSortBy.Name,
            "plans" or "plan-count" => MailMessageActionPlanBatchSortBy.PlanCount,
            "ready" or "ready-count" => MailMessageActionPlanBatchSortBy.ReadyPlanCount,
            "updated" or "updated-at" => MailMessageActionPlanBatchSortBy.UpdatedAt,
            "actions" or "action-types" => MailMessageActionPlanBatchSortBy.ActionTypeCount,
            _ => throw new InvalidOperationException($"Unsupported batch sort '{rawSortBy}'.")
        };
    }

    private static MailProfileConnectionTestScope ParseConnectionTestScope(string? rawScope) {
        if (string.IsNullOrWhiteSpace(rawScope)) {
            return MailProfileConnectionTestScope.Auto;
        }

        if (Enum.TryParse<MailProfileConnectionTestScope>(rawScope.Trim(), ignoreCase: true, out var scope)) {
            return scope;
        }

        throw new InvalidOperationException($"Unsupported connection test scope '{rawScope}'.");
    }
}
