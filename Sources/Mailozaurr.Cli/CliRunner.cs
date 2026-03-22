using System.Text.Json;
using Mailozaurr.Application;
using Mailozaurr.Cli.Mcp;

namespace Mailozaurr.Cli;

public static class CliRunner {
    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true
    };

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        Func<MailApplicationOptions, MailApplicationBuilder>? builderFactory = null) {
        if (args == null) {
            throw new ArgumentNullException(nameof(args));
        }
        if (output == null) {
            throw new ArgumentNullException(nameof(output));
        }
        if (error == null) {
            throw new ArgumentNullException(nameof(error));
        }

        var parseResult = CliArguments.Parse(args);
        if (parseResult.ShowHelp || parseResult.Positionals.Count == 0) {
            WriteHelp(output);
            return 0;
        }

        var options = new MailApplicationOptions();
        var profilesDir = parseResult.GetOption("profiles-dir");
        if (!string.IsNullOrWhiteSpace(profilesDir)) {
            options.ProfileStore.DirectoryPath = profilesDir;
        }
        var secretsDir = parseResult.GetOption("secrets-dir");
        if (!string.IsNullOrWhiteSpace(secretsDir)) {
            options.SecretStore.DirectoryPath = secretsDir;
        }
        var draftsDir = parseResult.GetOption("drafts-dir");
        if (!string.IsNullOrWhiteSpace(draftsDir)) {
            options.DraftStore.DirectoryPath = draftsDir;
        }

        var application = (builderFactory ?? (appOptions => new MailApplicationBuilder(appOptions)))
            .Invoke(options)
            .Build();

        try {
            return await ExecuteAsync(application, parseResult, output, error).ConfigureAwait(false);
        } catch (Exception ex) {
            await error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<int> ExecuteAsync(
        MailApplication application,
        CliArguments parseResult,
        TextWriter output,
        TextWriter error) {
        var command = parseResult.Positionals[0];
        return command switch {
            "profile" => await ExecuteProfileAsync(application, parseResult, output, error).ConfigureAwait(false),
            "draft" => await ExecuteDraftAsync(application, parseResult, output, error).ConfigureAwait(false),
            "mail" => await ExecuteMailAsync(application, parseResult, output, error).ConfigureAwait(false),
            "mcp" => await ExecuteMcpAsync(application, parseResult, error).ConfigureAwait(false),
            "send" => await ExecuteSendAsync(application, parseResult, output, error).ConfigureAwait(false),
            "queue" => await ExecuteQueueAsync(application, parseResult, output, error).ConfigureAwait(false),
            _ => await WriteUnknownCommandAsync(command, error).ConfigureAwait(false)
        };
    }

    private static async Task<int> ExecuteProfileAsync(
        MailApplication application,
        CliArguments parseResult,
        TextWriter output,
        TextWriter error) {
        if (parseResult.Positionals.Count < 2) {
            await error.WriteLineAsync("Missing profile command. Use 'profile list', 'profile create', 'profile graph-bootstrap', 'profile gmail-bootstrap', 'profile graph-login', 'profile gmail-login', 'profile refresh-auth', 'profile auth-status', 'profile test', 'profile summary', 'profile capabilities', 'profile show', 'profile validate', 'profile doctor', 'profile delete', 'profile set-default', 'profile set-secret', or 'profile remove-secret'.").ConfigureAwait(false);
            return 1;
        }

        var subCommand = parseResult.Positionals[1];
        var json = parseResult.HasFlag("json");
        switch (subCommand) {
            case "list":
                if (parseResult.HasFlag("summary")) {
                    if (parseResult.HasFlag("compact")) {
                        var compactOverviews = await application.ProfileOverview.GetCompactOverviewsAsync(BuildProfileOverviewQuery(parseResult)).ConfigureAwait(false);
                        await WriteSequenceAsync(output, compactOverviews, json, value => value.Summary).ConfigureAwait(false);
                        return compactOverviews.All(value => value.IsReady) ? 0 : 1;
                    }
                    var overviews = await application.ProfileOverview.GetOverviewsAsync(BuildProfileOverviewQuery(parseResult)).ConfigureAwait(false);
                    await WriteSequenceAsync(output, overviews, json, value => value.Summary).ConfigureAwait(false);
                    return overviews.All(value => value.IsReady) ? 0 : 1;
                }
                var profiles = await application.Profiles.GetProfilesAsync().ConfigureAwait(false);
                await WriteSequenceAsync(output, profiles, json, profile => $"{profile.Id} [{profile.Kind}] {profile.DisplayName}").ConfigureAwait(false);
                return 0;
            case "create":
                var createdProfile = BuildProfile(parseResult);
                var createResult = await application.Profiles.SaveAsync(createdProfile).ConfigureAwait(false);
                await WriteItemAsync(output, createResult, json, value => value.Message ?? "Profile saved.").ConfigureAwait(false);
                return createResult.Succeeded ? 0 : 1;
            case "graph-bootstrap":
                var graphBootstrapResult = await application.ProfileBootstrap.SaveGraphProfileAsync(new GraphProfileBootstrapRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    DisplayName = RequireOption(parseResult, "name"),
                    Description = parseResult.GetOption("description"),
                    Mailbox = RequireOption(parseResult, "mailbox"),
                    DefaultSender = parseResult.GetOption("default-sender"),
                    IsDefault = parseResult.HasFlag("is-default"),
                    ClientId = parseResult.GetOption("client-id"),
                    TenantId = parseResult.GetOption("tenant-id"),
                    ClientSecret = parseResult.GetOption("client-secret"),
                    AccessToken = parseResult.GetOption("access-token"),
                    CertificatePath = parseResult.GetOption("certificate-path"),
                    CertificatePassword = parseResult.GetOption("certificate-password")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, graphBootstrapResult, json, value => value.Message ?? "Graph profile saved.").ConfigureAwait(false);
                return graphBootstrapResult.Succeeded ? 0 : 1;
            case "gmail-bootstrap":
                var gmailBootstrapResult = await application.ProfileBootstrap.SaveGmailProfileAsync(new GmailProfileBootstrapRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    DisplayName = RequireOption(parseResult, "name"),
                    Description = parseResult.GetOption("description"),
                    Mailbox = parseResult.GetOption("mailbox"),
                    DefaultSender = parseResult.GetOption("default-sender"),
                    IsDefault = parseResult.HasFlag("is-default"),
                    ClientId = parseResult.GetOption("client-id"),
                    ClientSecret = parseResult.GetOption("client-secret"),
                    RefreshToken = parseResult.GetOption("refresh-token"),
                    AccessToken = parseResult.GetOption("access-token")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, gmailBootstrapResult, json, value => value.Message ?? "Gmail profile saved.").ConfigureAwait(false);
                return gmailBootstrapResult.Succeeded ? 0 : 1;
            case "graph-login":
                var graphLoginResult = await application.ProfileAuth.LoginGraphAsync(new GraphProfileLoginRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    Login = parseResult.GetOption("login"),
                    Mailbox = parseResult.GetOption("mailbox"),
                    ClientId = parseResult.GetOption("client-id"),
                    TenantId = parseResult.GetOption("tenant-id"),
                    RedirectUri = parseResult.GetOption("redirect-uri"),
                    Scopes = parseResult.GetOptionValues("scope")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!)
                        .ToArray()
                }).ConfigureAwait(false);
                await WriteItemAsync(output, graphLoginResult, json, value => value.Message ?? (value.Succeeded ? "Graph login completed." : "Graph login failed.")).ConfigureAwait(false);
                return graphLoginResult.Succeeded ? 0 : 1;
            case "gmail-login":
                var gmailLoginResult = await application.ProfileAuth.LoginGmailAsync(new GmailProfileLoginRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    GmailAccount = parseResult.GetOption("mailbox"),
                    ClientId = parseResult.GetOption("client-id"),
                    ClientSecret = parseResult.GetOption("client-secret"),
                    Scopes = parseResult.GetOptionValues("scope")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!)
                        .ToArray()
                }).ConfigureAwait(false);
                await WriteItemAsync(output, gmailLoginResult, json, value => value.Message ?? (value.Succeeded ? "Gmail login completed." : "Gmail login failed.")).ConfigureAwait(false);
                return gmailLoginResult.Succeeded ? 0 : 1;
            case "refresh-auth":
                var refreshAuthResult = await application.ProfileAuth.RefreshAsync(RequireOption(parseResult, "profile")).ConfigureAwait(false);
                await WriteItemAsync(output, refreshAuthResult, json, value => value.Message ?? (value.Succeeded ? "Profile auth refreshed." : "Profile auth refresh failed.")).ConfigureAwait(false);
                return refreshAuthResult.Succeeded ? 0 : 1;
            case "auth-status":
                var authStatusProfileId = RequireOption(parseResult, "profile");
                var authStatus = await application.ProfileAuth.GetStatusAsync(authStatusProfileId).ConfigureAwait(false);
                if (authStatus == null) {
                    await error.WriteLineAsync($"Profile '{authStatusProfileId}' was not found.").ConfigureAwait(false);
                    return 1;
                }
                await WriteItemAsync(output, authStatus, json, value => value.Summary).ConfigureAwait(false);
                return 0;
            case "test":
                var connectionResult = await application.ProfileConnections.TestAsync(
                    RequireOption(parseResult, "profile"),
                    ParseConnectionTestScope(parseResult.GetOption("scope"))).ConfigureAwait(false);
                await WriteItemAsync(output, connectionResult, json, value => value.Message ?? (value.Succeeded ? "Profile connection succeeded." : "Profile connection failed.")).ConfigureAwait(false);
                return connectionResult.Succeeded ? 0 : 1;
            case "summary":
                var summaryProfileId = RequireOption(parseResult, "profile");
                if (parseResult.HasFlag("compact")) {
                    var compactOverview = await application.ProfileOverview.GetCompactOverviewAsync(summaryProfileId).ConfigureAwait(false);
                    if (compactOverview == null) {
                        await error.WriteLineAsync($"Profile '{summaryProfileId}' was not found.").ConfigureAwait(false);
                        return 1;
                    }
                    await WriteItemAsync(output, compactOverview, json, value => value.Summary).ConfigureAwait(false);
                    return compactOverview.IsReady ? 0 : 1;
                }
                var overview = await application.ProfileOverview.GetOverviewAsync(summaryProfileId).ConfigureAwait(false);
                if (overview == null) {
                    await error.WriteLineAsync($"Profile '{summaryProfileId}' was not found.").ConfigureAwait(false);
                    return 1;
                }
                await WriteItemAsync(output, overview, json, value => value.Summary).ConfigureAwait(false);
                return overview.IsReady ? 0 : 1;
            case "show":
                var profileId = RequireOption(parseResult, "profile");
                var profile = await application.Profiles.GetProfileAsync(profileId).ConfigureAwait(false);
                if (profile == null) {
                    await error.WriteLineAsync($"Profile '{profileId}' was not found.").ConfigureAwait(false);
                    return 1;
                }
                await WriteItemAsync(output, profile, json, p => $"{p.Id} [{p.Kind}] {p.DisplayName}").ConfigureAwait(false);
                return 0;
            case "capabilities":
                var capabilitiesProfileId = RequireOption(parseResult, "profile");
                var capabilities = await application.Profiles.GetCapabilitiesAsync(capabilitiesProfileId).ConfigureAwait(false);
                if (capabilities == null) {
                    await error.WriteLineAsync($"Profile '{capabilitiesProfileId}' was not found.").ConfigureAwait(false);
                    return 1;
                }
                await WriteItemAsync(output, capabilities, json, value => $"{value.Kind}: {value.Capabilities}").ConfigureAwait(false);
                return 0;
            case "validate":
                var validateId = RequireOption(parseResult, "profile");
                var profileToValidate = await application.Profiles.GetProfileAsync(validateId).ConfigureAwait(false);
                if (profileToValidate == null) {
                    await error.WriteLineAsync($"Profile '{validateId}' was not found.").ConfigureAwait(false);
                    return 1;
                }
                var result = await application.Profiles.ValidateAsync(profileToValidate).ConfigureAwait(false);
                await WriteItemAsync(output, result, json, r => r.Message ?? (r.Succeeded ? "Profile is valid." : "Profile is invalid.")).ConfigureAwait(false);
                return result.Succeeded ? 0 : 1;
            case "doctor":
                var doctorResult = await application.Profiles.DiagnoseAsync(RequireOption(parseResult, "profile")).ConfigureAwait(false);
                await WriteItemAsync(output, doctorResult, json, value => value.Message ?? (value.Succeeded ? "Profile is ready." : "Profile is not ready.")).ConfigureAwait(false);
                return doctorResult.Succeeded ? 0 : 1;
            case "delete":
                var deleteResult = await application.Profiles.DeleteAsync(RequireOption(parseResult, "profile")).ConfigureAwait(false);
                await WriteItemAsync(output, deleteResult, json, value => value.Message ?? "Profile deleted.").ConfigureAwait(false);
                return deleteResult.Succeeded ? 0 : 1;
            case "set-default":
                var setDefaultResult = await application.Profiles.SetDefaultAsync(RequireOption(parseResult, "profile")).ConfigureAwait(false);
                await WriteItemAsync(output, setDefaultResult, json, value => value.Message ?? "Default profile updated.").ConfigureAwait(false);
                return setDefaultResult.Succeeded ? 0 : 1;
            case "set-secret":
                var setSecretResult = await application.ProfileSecrets.SetSecretAsync(
                    RequireOption(parseResult, "profile"),
                    RequireOption(parseResult, "name"),
                    RequireOption(parseResult, "value")).ConfigureAwait(false);
                await WriteItemAsync(output, setSecretResult, json, value => value.Message ?? "Secret saved.").ConfigureAwait(false);
                return setSecretResult.Succeeded ? 0 : 1;
            case "remove-secret":
                var removeSecretResult = await application.ProfileSecrets.RemoveSecretAsync(
                    RequireOption(parseResult, "profile"),
                    RequireOption(parseResult, "name")).ConfigureAwait(false);
                await WriteItemAsync(output, removeSecretResult, json, value => value.Message ?? "Secret removed.").ConfigureAwait(false);
                return removeSecretResult.Succeeded ? 0 : 1;
            default:
                await error.WriteLineAsync($"Unknown profile command '{subCommand}'.").ConfigureAwait(false);
                return 1;
        }
    }

    private static async Task<int> ExecuteMailAsync(
        MailApplication application,
        CliArguments parseResult,
        TextWriter output,
        TextWriter error) {
        if (parseResult.Positionals.Count < 2) {
            await error.WriteLineAsync("Missing mail command. Use 'mail folders', 'mail search', 'mail attachments', 'mail get', 'mail save-attachment', or 'mail save-attachments'.").ConfigureAwait(false);
            return 1;
        }

        var subCommand = parseResult.Positionals[1];
        var json = parseResult.HasFlag("json");
        switch (subCommand) {
            case "folders":
                var folderQuery = new MailFolderQuery {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    ParentFolderId = parseResult.GetOption("parent-folder"),
                    RootOnly = parseResult.HasFlag("root-only")
                };
                if (parseResult.HasFlag("compact")) {
                    var compactFolders = await application.Read.GetFoldersCompactAsync(folderQuery).ConfigureAwait(false);
                    await WriteSequenceAsync(output, compactFolders, json, folder =>
                        folder.Summary).ConfigureAwait(false);
                    return 0;
                }
                var folders = await application.Read.GetFoldersAsync(folderQuery).ConfigureAwait(false);
                await WriteSequenceAsync(output, folders, json, folder =>
                    $"{folder.Id} {folder.Path ?? folder.DisplayName}").ConfigureAwait(false);
                return 0;
            case "search":
                var searchRequest = new MailSearchRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    QueryText = parseResult.GetOption("query"),
                    SubjectContains = parseResult.GetOption("subject"),
                    FromContains = parseResult.GetOption("from"),
                    ToContains = parseResult.GetOption("to"),
                    HasAttachments = parseResult.HasFlag("has-attachments"),
                    Limit = parseResult.GetIntOption("limit")
                };
                if (parseResult.HasFlag("compact")) {
                    var compactMessages = await application.Read.SearchCompactAsync(searchRequest).ConfigureAwait(false);
                    await WriteSequenceAsync(output, compactMessages, json, message =>
                        message.Summary).ConfigureAwait(false);
                    return 0;
                }
                var messages = await application.Read.SearchAsync(searchRequest).ConfigureAwait(false);
                await WriteSequenceAsync(output, messages, json, message =>
                    $"{message.Id} {message.Subject ?? "(no subject)"}").ConfigureAwait(false);
                return 0;
            case "get":
                var getRequest = new GetMessageRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageId = RequireOption(parseResult, "message-id"),
                    IncludeRawContent = parseResult.HasFlag("include-raw")
                };
                if (parseResult.HasFlag("compact")) {
                    var compactDetail = await application.Read.GetMessageCompactAsync(getRequest).ConfigureAwait(false);
                    if (compactDetail == null) {
                        await error.WriteLineAsync("Message was not found.").ConfigureAwait(false);
                        return 1;
                    }
                    await WriteItemAsync(output, compactDetail, json, value => value.SummaryText).ConfigureAwait(false);
                    return 0;
                }
                var detail = await application.Read.GetMessageAsync(getRequest).ConfigureAwait(false);
                if (detail == null) {
                    await error.WriteLineAsync("Message was not found.").ConfigureAwait(false);
                    return 1;
                }
                await WriteItemAsync(output, detail, json, value => value.Summary?.Subject ?? value.Id).ConfigureAwait(false);
                return 0;
            case "attachments":
                var attachments = await application.Read.GetAttachmentsAsync(new ListAttachmentsRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageId = RequireOption(parseResult, "message-id")
                }).ConfigureAwait(false);
                await WriteSequenceAsync(output, attachments, json, attachment =>
                    $"{attachment.Id} {attachment.FileName}").ConfigureAwait(false);
                return 0;
            case "save-attachment":
                var saveRequest = new SaveAttachmentRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageId = RequireOption(parseResult, "message-id"),
                    AttachmentId = RequireOption(parseResult, "attachment-id"),
                    DestinationPath = RequireOption(parseResult, "path"),
                    Overwrite = parseResult.HasFlag("overwrite")
                };
                var saveResult = await application.Read.SaveAttachmentAsync(saveRequest).ConfigureAwait(false);
                await WriteItemAsync(output, saveResult, json, value => value.Message ?? "Attachment saved.").ConfigureAwait(false);
                return saveResult.Succeeded ? 0 : 1;
            case "save-attachments":
                var saveAttachmentsResult = await application.Read.SaveAttachmentsAsync(new SaveAttachmentsRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageId = RequireOption(parseResult, "message-id"),
                    DestinationPath = RequireOption(parseResult, "path"),
                    AttachmentIds = parseResult.GetOptionValues("attachment-id")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToList(),
                    FileNameContains = parseResult.GetOption("name-contains"),
                    ContentTypeContains = parseResult.GetOption("content-type"),
                    Overwrite = parseResult.HasFlag("overwrite")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, saveAttachmentsResult, json, value =>
                    value.Message ?? $"Saved {value.SavedCount} attachment(s).").ConfigureAwait(false);
                return saveAttachmentsResult.Succeeded ? 0 : 1;
            default:
                await error.WriteLineAsync($"Unknown mail command '{subCommand}'.").ConfigureAwait(false);
                return 1;
        }
    }

    private static async Task<int> ExecuteDraftAsync(
        MailApplication application,
        CliArguments parseResult,
        TextWriter output,
        TextWriter error) {
        if (parseResult.Positionals.Count < 2) {
            await error.WriteLineAsync("Missing draft command. Use 'draft list', 'draft save', 'draft get', 'draft delete', or 'draft export'.").ConfigureAwait(false);
            return 1;
        }

        var subCommand = parseResult.Positionals[1];
        var json = parseResult.HasFlag("json");
        switch (subCommand) {
            case "list":
                if (parseResult.HasFlag("compact")) {
                    var compactDrafts = await application.Drafts.GetDraftsCompactAsync().ConfigureAwait(false);
                    await WriteSequenceAsync(output, compactDrafts, json, draft =>
                        draft.Summary).ConfigureAwait(false);
                    return 0;
                }
                var drafts = await application.Drafts.GetDraftsAsync().ConfigureAwait(false);
                await WriteSequenceAsync(output, drafts, json, draft =>
                    $"{draft.Id} [{draft.Message.ProfileId}] {draft.Name}").ConfigureAwait(false);
                return 0;
            case "save":
                var draft = await BuildMailDraftAsync(application, parseResult).ConfigureAwait(false);
                var saveResult = await application.Drafts.SaveAsync(draft).ConfigureAwait(false);
                await WriteItemAsync(output, saveResult, json, value => value.Message ?? "Draft saved.").ConfigureAwait(false);
                return saveResult.Succeeded ? 0 : 1;
            case "get":
                if (parseResult.HasFlag("compact")) {
                    var compactStoredDraft = await application.Drafts.GetDraftCompactAsync(RequireOption(parseResult, "draft")).ConfigureAwait(false);
                    if (compactStoredDraft == null) {
                        await error.WriteLineAsync("Draft was not found.").ConfigureAwait(false);
                        return 1;
                    }
                    await WriteItemAsync(output, compactStoredDraft, json, value => value.Summary).ConfigureAwait(false);
                    return 0;
                }
                var storedDraft = await application.Drafts.GetDraftAsync(RequireOption(parseResult, "draft")).ConfigureAwait(false);
                if (storedDraft == null) {
                    await error.WriteLineAsync("Draft was not found.").ConfigureAwait(false);
                    return 1;
                }
                await WriteItemAsync(output, storedDraft, json, value => $"{value.Id} [{value.Message.ProfileId}] {value.Name}").ConfigureAwait(false);
                return 0;
            case "delete":
                var deleteResult = await application.Drafts.DeleteAsync(RequireOption(parseResult, "draft")).ConfigureAwait(false);
                await WriteItemAsync(output, deleteResult, json, value => value.Message ?? "Draft deleted.").ConfigureAwait(false);
                return deleteResult.Succeeded ? 0 : 1;
            case "export":
                var draftToExport = await application.Drafts.GetDraftAsync(RequireOption(parseResult, "draft")).ConfigureAwait(false);
                if (draftToExport == null) {
                    await error.WriteLineAsync("Draft was not found.").ConfigureAwait(false);
                    return 1;
                }
                await application.DraftExchange.SaveAsync(RequireOption(parseResult, "path"), draftToExport).ConfigureAwait(false);
                await WriteItemAsync(output, OperationResult.Success("Draft exported."), json, value => value.Message ?? "Draft exported.").ConfigureAwait(false);
                return 0;
            default:
                await error.WriteLineAsync($"Unknown draft command '{subCommand}'.").ConfigureAwait(false);
                return 1;
        }
    }

    private static async Task<int> ExecuteQueueAsync(
        MailApplication application,
        CliArguments parseResult,
        TextWriter output,
        TextWriter error) {
        if (parseResult.Positionals.Count < 2) {
            await error.WriteLineAsync("Missing queue command. Use 'queue list', 'queue get', 'queue remove', or 'queue process'.").ConfigureAwait(false);
            return 1;
        }

        var subCommand = parseResult.Positionals[1];
        var json = parseResult.HasFlag("json");
        switch (subCommand) {
            case "list":
                if (parseResult.HasFlag("compact")) {
                    var compactQueuedMessages = await application.Queue.ListCompactAsync().ConfigureAwait(false);
                    await WriteSequenceAsync(output, compactQueuedMessages, json, message => message.Summary).ConfigureAwait(false);
                    return 0;
                }
                var queuedMessages = await application.Queue.ListAsync().ConfigureAwait(false);
                await WriteSequenceAsync(output, queuedMessages, json, message =>
                    $"{message.MessageId} [{message.Provider}] attempts={message.AttemptCount} next={message.NextAttemptAt:O}").ConfigureAwait(false);
                return 0;
            case "get":
                if (parseResult.HasFlag("compact")) {
                    var compactQueuedMessage = await application.Queue.GetCompactAsync(RequireOption(parseResult, "message-id")).ConfigureAwait(false);
                    if (compactQueuedMessage == null) {
                        await error.WriteLineAsync("Queued message was not found.").ConfigureAwait(false);
                        return 1;
                    }
                    await WriteItemAsync(output, compactQueuedMessage, json, message => message.Summary).ConfigureAwait(false);
                    return 0;
                }
                var queuedMessage = await application.Queue.GetAsync(RequireOption(parseResult, "message-id")).ConfigureAwait(false);
                if (queuedMessage == null) {
                    await error.WriteLineAsync("Queued message was not found.").ConfigureAwait(false);
                    return 1;
                }
                await WriteItemAsync(output, queuedMessage, json, message =>
                    $"{message.MessageId} [{message.Provider}] attempts={message.AttemptCount}").ConfigureAwait(false);
                return 0;
            case "remove":
                var removeResult = await application.Queue.RemoveAsync(RequireOption(parseResult, "message-id")).ConfigureAwait(false);
                await WriteItemAsync(output, removeResult, json, value => value.Message ?? "Queued message removed.").ConfigureAwait(false);
                return removeResult.Succeeded ? 0 : 1;
            case "process":
                var processResult = await application.Queue.ProcessAsync().ConfigureAwait(false);
                await WriteItemAsync(output, processResult, json, value =>
                    value.Message ?? $"Processed queue: attempted={value.AttemptedCount}, sent={value.SentCount}, failed={value.FailedCount}.").ConfigureAwait(false);
                return processResult.Succeeded ? 0 : 1;
            default:
                await error.WriteLineAsync($"Unknown queue command '{subCommand}'.").ConfigureAwait(false);
                return 1;
        }
    }

    private static async Task<int> ExecuteSendAsync(
        MailApplication application,
        CliArguments parseResult,
        TextWriter output,
        TextWriter error) {
        var json = parseResult.HasFlag("json");
        var request = await BuildSendRequestAsync(application, parseResult).ConfigureAwait(false);
        var result = await application.Send.SendAsync(request).ConfigureAwait(false);
        await WriteItemAsync(output, result, json, value => value.Message ?? (value.Queued
            ? $"Message queued: {value.QueueMessageId ?? "(unknown-id)"}"
            : $"Message sent: {value.ProviderMessageId ?? "(provider-id unavailable)"}")).ConfigureAwait(false);
        return result.Succeeded ? 0 : 1;
    }

    private static async Task<int> ExecuteMcpAsync(
        MailApplication application,
        CliArguments parseResult,
        TextWriter error) {
        if (parseResult.Positionals.Count < 2) {
            await error.WriteLineAsync("Missing mcp command. Use 'mcp serve'.").ConfigureAwait(false);
            return 1;
        }

        var subCommand = parseResult.Positionals[1];
        if (!string.Equals(subCommand, "serve", StringComparison.OrdinalIgnoreCase)) {
            await error.WriteLineAsync($"Unknown mcp command '{subCommand}'.").ConfigureAwait(false);
            return 1;
        }

        await McpServerHost.RunAsync(application).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> WriteUnknownCommandAsync(string command, TextWriter error) {
        await error.WriteLineAsync($"Unknown command '{command}'.").ConfigureAwait(false);
        return 1;
    }

    private static string RequireOption(CliArguments parseResult, string name) {
        var value = parseResult.GetOption(name);
        if (!string.IsNullOrWhiteSpace(value)) {
            return value!;
        }

        throw new InvalidOperationException($"Missing required option '--{name}'.");
    }

    private static MailProfile BuildProfile(CliArguments parseResult) {
        var profile = new MailProfile {
            Id = RequireOption(parseResult, "profile"),
            DisplayName = RequireOption(parseResult, "name"),
            Kind = MailProfileKindParser.Parse(RequireOption(parseResult, "kind")),
            Description = parseResult.GetOption("description"),
            DefaultSender = parseResult.GetOption("default-sender"),
            DefaultMailbox = parseResult.GetOption("default-mailbox"),
            IsDefault = parseResult.HasFlag("is-default")
        };

        foreach (var setting in parseResult.GetOptionValues("setting")) {
            if (string.IsNullOrWhiteSpace(setting)) {
                continue;
            }

            var separatorIndex = setting.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == setting.Length - 1) {
                throw new InvalidOperationException("Option '--setting' must use the format key=value.");
            }

            var key = setting[..separatorIndex].Trim();
            var value = setting[(separatorIndex + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0) {
                throw new InvalidOperationException("Option '--setting' must use the format key=value.");
            }

            profile.Settings[key] = value;
        }

        return profile;
    }

    private static async Task<SendMessageRequest> BuildSendRequestAsync(MailApplication application, CliArguments parseResult) {
        if (application == null) {
            throw new ArgumentNullException(nameof(application));
        }

        var existingDraftId = parseResult.GetOption("draft");
        var draftFilePath = parseResult.GetOption("file");
        if (!string.IsNullOrWhiteSpace(existingDraftId) && !string.IsNullOrWhiteSpace(draftFilePath)) {
            throw new InvalidOperationException("Options '--draft' and '--file' cannot be used together.");
        }

        var sendNow = parseResult.HasFlag("send-now");
        if (!string.IsNullOrWhiteSpace(draftFilePath)) {
            var importedDraft = await application.DraftExchange.LoadAsync(draftFilePath!).ConfigureAwait(false);
            return CreateSendRequestFromDraft(importedDraft.Message, sendNow);
        }

        if (!string.IsNullOrWhiteSpace(existingDraftId)) {
            var existingDraft = await application.Drafts.GetDraftAsync(existingDraftId!).ConfigureAwait(false);
            if (existingDraft == null) {
                throw new InvalidOperationException($"Draft '{existingDraftId}' was not found.");
            }

            return CreateSendRequestFromDraft(existingDraft.Message, sendNow);
        }

        var profileId = RequireOption(parseResult, "profile");
        var draft = BuildDraftMessage(parseResult, profileId);

        return new SendMessageRequest {
            ProfileId = profileId,
            Message = draft,
            PreferQueue = !sendNow,
            RequireImmediateSend = sendNow
        };
    }

    private static async Task<MailDraft> BuildMailDraftAsync(MailApplication application, CliArguments parseResult) {
        if (application == null) {
            throw new ArgumentNullException(nameof(application));
        }

        var filePath = parseResult.GetOption("file");
        if (!string.IsNullOrWhiteSpace(filePath)) {
            var importedDraft = await application.DraftExchange.LoadAsync(filePath!).ConfigureAwait(false);
            var overrideDraftId = parseResult.GetOption("draft");
            var overrideName = parseResult.GetOption("name");
            if (!string.IsNullOrWhiteSpace(overrideDraftId)) {
                importedDraft.Id = overrideDraftId!.Trim();
            }
            if (!string.IsNullOrWhiteSpace(overrideName)) {
                importedDraft.Name = overrideName!.Trim();
            }

            return importedDraft;
        }

        var draftId = RequireOption(parseResult, "draft");
        var draftName = RequireOption(parseResult, "name");

        return new MailDraft {
            Id = draftId,
            Name = draftName,
            Message = BuildDraftMessage(parseResult, RequireOption(parseResult, "profile"))
        };
    }

    private static SendMessageRequest CreateSendRequestFromDraft(DraftMessage draft, bool sendNow) =>
        new() {
            ProfileId = draft.ProfileId,
            Message = CloneDraftMessage(draft),
            PreferQueue = !sendNow,
            RequireImmediateSend = sendNow
        };

    private static MessageRecipient ToRecipient(string value) => new() {
        Address = value.Trim()
    };

    private static DraftMessage BuildDraftMessage(CliArguments parseResult, string profileId) {
        var draft = new DraftMessage {
            ProfileId = profileId,
            Subject = parseResult.GetOption("subject"),
            TextBody = parseResult.GetOption("text"),
            HtmlBody = parseResult.GetOption("html")
        };

        var from = parseResult.GetOption("from");
        if (!string.IsNullOrWhiteSpace(from)) {
            draft.From = ToRecipient(from!);
        }

        draft.To.AddRange(parseResult.GetOptionValues("to").Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => ToRecipient(value!)));
        draft.Cc.AddRange(parseResult.GetOptionValues("cc").Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => ToRecipient(value!)));
        draft.Bcc.AddRange(parseResult.GetOptionValues("bcc").Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => ToRecipient(value!)));
        draft.ReplyTo.AddRange(parseResult.GetOptionValues("reply-to").Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => ToRecipient(value!)));

        foreach (var header in parseResult.GetOptionValues("header")) {
            if (string.IsNullOrWhiteSpace(header)) {
                continue;
            }

            var separatorIndex = header.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == header.Length - 1) {
                throw new InvalidOperationException("Option '--header' must use the format key=value.");
            }

            draft.Headers[header[..separatorIndex].Trim()] = header[(separatorIndex + 1)..].Trim();
        }

        foreach (var attachmentPath in parseResult.GetOptionValues("attachment")) {
            if (string.IsNullOrWhiteSpace(attachmentPath)) {
                continue;
            }

            draft.Attachments.Add(new DraftAttachment {
                Path = attachmentPath!.Trim()
            });
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

    private static MessageRecipient ToRecipientCopy(MessageRecipient recipient) => new() {
        Name = recipient.Name,
        Address = recipient.Address
    };

    private static async Task WriteItemAsync<T>(
        TextWriter output,
        T value,
        bool json,
        Func<T, string> formatter) {
        if (json) {
            await output.WriteLineAsync(JsonSerializer.Serialize(value, JsonOptions)).ConfigureAwait(false);
            return;
        }

        await output.WriteLineAsync(formatter(value)).ConfigureAwait(false);
    }

    private static async Task WriteSequenceAsync<T>(
        TextWriter output,
        IReadOnlyList<T> values,
        bool json,
        Func<T, string> formatter) {
        if (json) {
            await output.WriteLineAsync(JsonSerializer.Serialize(values, JsonOptions)).ConfigureAwait(false);
            return;
        }

        foreach (var value in values) {
            await output.WriteLineAsync(formatter(value)).ConfigureAwait(false);
        }
    }

    private static void WriteHelp(TextWriter output) {
        output.WriteLine("Mailozaurr CLI");
        output.WriteLine();
        output.WriteLine("Commands:");
        output.WriteLine("  profile list [--summary] [--compact] [--kind <kind>] [--ready-only] [--can-read] [--can-send] [--default-only] [--sort <id|kind|readiness>] [--desc] [--json]");
        output.WriteLine("  profile create --profile <id> --kind <kind> --name <display-name> [--description <text>] [--default-sender <email>] [--default-mailbox <value>] [--is-default] [--setting <key=value>] [--json]");
        output.WriteLine("  profile graph-bootstrap --profile <id> --name <display-name> --mailbox <address> [--description <text>] [--default-sender <email>] [--is-default] [--client-id <id>] [--tenant-id <id>] [--client-secret <secret>] [--access-token <token>] [--certificate-path <path>] [--certificate-password <secret>] [--json]");
        output.WriteLine("  profile gmail-bootstrap --profile <id> --name <display-name> [--mailbox <address|me>] [--description <text>] [--default-sender <email>] [--is-default] [--client-id <id>] [--client-secret <secret>] [--refresh-token <token>] [--access-token <token>] [--json]");
        output.WriteLine("  profile graph-login --profile <id> [--login <upn>] [--mailbox <address>] [--client-id <id>] [--tenant-id <id>] [--redirect-uri <uri>] [--scope <value>] [--scope <value>] [--json]");
        output.WriteLine("  profile gmail-login --profile <id> [--mailbox <address>] [--client-id <id>] [--client-secret <secret>] [--scope <value>] [--scope <value>] [--json]");
        output.WriteLine("  profile refresh-auth --profile <id> [--json]");
        output.WriteLine("  profile auth-status --profile <id> [--json]");
        output.WriteLine("  profile test --profile <id> [--scope <auto|auth|mailbox|send>] [--json]");
        output.WriteLine("  profile summary --profile <id> [--compact] [--json]");
        output.WriteLine("  profile capabilities --profile <id> [--json]");
        output.WriteLine("  profile show --profile <id> [--json]");
        output.WriteLine("  profile validate --profile <id> [--json]");
        output.WriteLine("  profile doctor --profile <id> [--json]");
        output.WriteLine("  profile delete --profile <id> [--json]");
        output.WriteLine("  profile set-default --profile <id> [--json]");
        output.WriteLine("  profile set-secret --profile <id> --name <secret-name> --value <secret-value> [--json]");
        output.WriteLine("  profile remove-secret --profile <id> --name <secret-name> [--json]");
        output.WriteLine("  draft list [--compact] [--json]");
        output.WriteLine("  draft save --file <path> [--draft <id>] [--name <display-name>] [--json]");
        output.WriteLine("  draft save --draft <id> --name <display-name> --profile <id> --to <address> [--to <address>] [--cc <address>] [--bcc <address>] [--reply-to <address>] [--from <address>] [--subject <text>] [--text <text>] [--html <html>] [--attachment <path>] [--header <key=value>] [--json]");
        output.WriteLine("  draft get --draft <id> [--compact] [--json]");
        output.WriteLine("  draft delete --draft <id> [--json]");
        output.WriteLine("  draft export --draft <id> --path <file> [--json]");
        output.WriteLine("  mail folders --profile <id> [--mailbox <id>] [--parent-folder <id>] [--root-only] [--compact] [--json]");
        output.WriteLine("  mail search --profile <id> [--mailbox <id>] [--folder <name>] [--query <text>] [--subject <text>] [--from <text>] [--to <text>] [--limit <n>] [--compact] [--json]");
        output.WriteLine("  mail get --profile <id> --message-id <id> [--mailbox <id>] [--folder <name>] [--include-raw] [--compact] [--json]");
        output.WriteLine("  mail attachments --profile <id> --message-id <id> [--mailbox <id>] [--folder <name>] [--json]");
        output.WriteLine("  mail save-attachment --profile <id> --message-id <id> --attachment-id <id> --path <destination> [--mailbox <id>] [--folder <name>] [--overwrite] [--json]");
        output.WriteLine("  mail save-attachments --profile <id> --message-id <id> --path <destination> [--mailbox <id>] [--folder <name>] [--attachment-id <id>] [--attachment-id <id>] [--name-contains <text>] [--content-type <text>] [--overwrite] [--json]");
        output.WriteLine("  mcp serve");
        output.WriteLine("  send --draft <id> [--send-now] [--json]");
        output.WriteLine("  send --file <path> [--send-now] [--json]");
        output.WriteLine("  send --profile <id> --to <address> [--to <address>] [--cc <address>] [--bcc <address>] [--reply-to <address>] [--from <address>] [--subject <text>] [--text <text>] [--html <html>] [--attachment <path>] [--header <key=value>] [--send-now] [--json]");
        output.WriteLine("  queue list [--compact] [--json]");
        output.WriteLine("  queue get --message-id <id> [--compact] [--json]");
        output.WriteLine("  queue remove --message-id <id> [--json]");
        output.WriteLine("  queue process [--json]");
        output.WriteLine();
        output.WriteLine("Global options:");
        output.WriteLine("  --profiles-dir <path>");
        output.WriteLine("  --secrets-dir <path>");
        output.WriteLine("  --drafts-dir <path>");
        output.WriteLine("  --help");
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

    private static MailProfileOverviewQuery BuildProfileOverviewQuery(CliArguments parseResult) {
        var query = new MailProfileOverviewQuery {
            Descending = parseResult.HasFlag("desc"),
            ReadyOnly = parseResult.HasFlag("ready-only"),
            CanReadOnly = parseResult.HasFlag("can-read"),
            CanSendOnly = parseResult.HasFlag("can-send"),
            DefaultOnly = parseResult.HasFlag("default-only")
        };

        var kind = parseResult.GetOption("kind");
        if (!string.IsNullOrWhiteSpace(kind)) {
            query.Kind = MailProfileKindParser.Parse(kind);
        }

        var sort = parseResult.GetOption("sort");
        if (!string.IsNullOrWhiteSpace(sort)) {
            query.SortBy = ParseProfileOverviewSortBy(sort);
        }

        return query;
    }

    private static MailProfileOverviewSortBy ParseProfileOverviewSortBy(string rawSort) {
        if (Enum.TryParse<MailProfileOverviewSortBy>(rawSort.Trim(), ignoreCase: true, out var sortBy)) {
            return sortBy;
        }

        throw new InvalidOperationException($"Unsupported profile overview sort '{rawSort}'.");
    }
}
