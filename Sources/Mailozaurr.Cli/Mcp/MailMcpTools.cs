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
        [Description("Optional confidential client secret to store securely.")] string? clientSecret = null,
        [Description("Optional explicit access token to store securely.")] string? accessToken = null,
        [Description("Optional certificate path for certificate-based auth.")] string? certificatePath = null,
        [Description("Optional certificate password to store securely.")] string? certificatePassword = null,
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
            AccessToken = accessToken,
            CertificatePath = certificatePath,
            CertificatePassword = certificatePassword
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
        [Description("Optional Google OAuth client secret to store securely.")] string? clientSecret = null,
        [Description("Optional Google OAuth refresh token to store securely.")] string? refreshToken = null,
        [Description("Optional explicit access token to store securely.")] string? accessToken = null,
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
            RefreshToken = refreshToken,
            AccessToken = accessToken
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
        [Description("Optional OAuth client secret override.")] string? clientSecret = null,
        [Description("Optional scopes override.")] string[]? scopes = null,
        CancellationToken cancellationToken = default) =>
        _application.ProfileAuth.LoginGmailAsync(new GmailProfileLoginRequest {
            ProfileId = profileId,
            GmailAccount = mailbox,
            ClientId = clientId,
            ClientSecret = clientSecret,
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
        [Description("The secret value to store.")] string secretValue,
        CancellationToken cancellationToken = default) =>
        _application.ProfileSecrets.SetSecretAsync(profileId, secretName, secretValue, cancellationToken);

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
