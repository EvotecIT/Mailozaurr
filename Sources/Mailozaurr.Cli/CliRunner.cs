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
        var planBatchesDir = parseResult.GetOption("plan-batches-dir");
        if (!string.IsNullOrWhiteSpace(planBatchesDir)) {
            options.ActionPlanBatchStore.DirectoryPath = planBatchesDir;
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
            await error.WriteLineAsync("Missing mail command. Use 'mail folders', 'mail folder-aliases', 'mail resolve-folder', 'mail list-plan-batches', 'mail show-plan-batch', 'mail import-plan-batch', 'mail export-plan-batch', 'mail create-common-plan-batch', 'mail clone-plan-batch', 'mail preview-transform-plan-batch', 'mail transform-plan-batch', 'mail add-plan-to-batch', 'mail add-plan-file-to-batch', 'mail replace-plan-in-batch', 'mail replace-plan-file-in-batch', 'mail remove-plan-from-batch', 'mail delete-plan-batch', 'mail execute-plan-batch-stored', 'mail plan-action', 'mail export-plan', 'mail show-plan', 'mail execute-plan', 'mail execute-plan-file', 'mail execute-plan-batch', 'mail preview-all', 'mail preview-mark-read', 'mail preview-flag', 'mail preview-actions', 'mail preview-move', 'mail preview-delete', 'mail search', 'mail attachments', 'mail get', 'mail get-many', 'mail mark-read', 'mail flag', 'mail archive', 'mail trash', 'mail move', 'mail delete', 'mail save-attachment', 'mail save-attachments', or 'mail save-attachments-many'.").ConfigureAwait(false);
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
            case "folder-aliases":
                var aliases = await application.FolderAliases.GetAliasesAsync(
                    RequireOption(parseResult, "profile"),
                    parseResult.GetOption("mailbox")).ConfigureAwait(false);
                await WriteSequenceAsync(output, aliases, json, value => value.Summary).ConfigureAwait(false);
                return 0;
            case "resolve-folder":
                var resolution = await application.FolderAliases.ResolveAsync(
                    RequireOption(parseResult, "profile"),
                    RequireOption(parseResult, "target-folder"),
                    parseResult.GetOption("mailbox")).ConfigureAwait(false);
                await WriteItemAsync(output, resolution, json, value => value.Summary).ConfigureAwait(false);
                return resolution.IsSupported ? 0 : 1;
            case "list-plan-batches":
                var batchQuery = BuildMessageActionPlanBatchQuery(parseResult);
                if (parseResult.HasFlag("summary")) {
                    var summaryBatches = await application.MessageActionPlanRegistry.GetBatchesSummaryAsync(batchQuery).ConfigureAwait(false);
                    await WriteSequenceAsync(output, summaryBatches, json, value => value.Summary).ConfigureAwait(false);
                    return 0;
                }
                if (parseResult.HasFlag("compact")) {
                    var compactBatches = await application.MessageActionPlanRegistry.GetBatchesCompactAsync(batchQuery).ConfigureAwait(false);
                    await WriteSequenceAsync(output, compactBatches, json, value => value.Summary).ConfigureAwait(false);
                    return 0;
                }
                var batches = await application.MessageActionPlanRegistry.GetBatchesAsync(batchQuery).ConfigureAwait(false);
                await WriteSequenceAsync(output, batches, json, value => $"{value.Id} [{value.Plans.Count} plan(s)] {value.Name}").ConfigureAwait(false);
                return 0;
            case "show-plan-batch":
                var batchId = RequireOption(parseResult, "batch");
                if (parseResult.HasFlag("summary")) {
                    var summaryBatch = await application.MessageActionPlanRegistry.GetBatchSummaryAsync(batchId).ConfigureAwait(false);
                    if (summaryBatch == null) {
                        await error.WriteLineAsync($"Action plan batch '{batchId}' was not found.").ConfigureAwait(false);
                        return 1;
                    }
                    await WriteItemAsync(output, summaryBatch, json, value => value.Summary).ConfigureAwait(false);
                    return 0;
                }
                if (parseResult.HasFlag("compact")) {
                    var compactBatch = await application.MessageActionPlanRegistry.GetBatchCompactAsync(batchId).ConfigureAwait(false);
                    if (compactBatch == null) {
                        await error.WriteLineAsync($"Action plan batch '{batchId}' was not found.").ConfigureAwait(false);
                        return 1;
                    }
                    await WriteItemAsync(output, compactBatch, json, value => value.Summary).ConfigureAwait(false);
                    return 0;
                }
                var batch = await application.MessageActionPlanRegistry.GetBatchAsync(batchId).ConfigureAwait(false);
                if (batch == null) {
                    await error.WriteLineAsync($"Action plan batch '{batchId}' was not found.").ConfigureAwait(false);
                    return 1;
                }
                await WriteItemAsync(output, batch, json, value => $"{value.Id} [{value.Plans.Count} plan(s)] {value.Name}").ConfigureAwait(false);
                return 0;
            case "import-plan-batch":
                var importBatchResult = await application.MessageActionPlanRegistry.ImportAsync(
                    RequireOption(parseResult, "batch"),
                    RequireOption(parseResult, "name"),
                    RequireOption(parseResult, "path"),
                    parseResult.GetOption("description")).ConfigureAwait(false);
                await WriteItemAsync(output, importBatchResult, json, value => value.Message ?? "Action plan batch imported.").ConfigureAwait(false);
                return importBatchResult.Succeeded ? 0 : 1;
            case "export-plan-batch":
                var exportBatchResult = await application.MessageActionPlanRegistry.ExportAsync(
                    RequireOption(parseResult, "batch"),
                    RequireOption(parseResult, "path")).ConfigureAwait(false);
                await WriteItemAsync(output, exportBatchResult, json, value => value.Message ?? "Action plan batch exported.").ConfigureAwait(false);
                return exportBatchResult.Succeeded ? 0 : 1;
            case "create-common-plan-batch":
                var createCommonBatchResult = await application.MessageActionPlanRegistry.CreateCommonBatchAsync(
                    RequireOption(parseResult, "batch"),
                    RequireOption(parseResult, "name"),
                    BuildCommonActionsPreviewRequest(parseResult),
                    parseResult.GetOptionValues("action")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToArray(),
                    parseResult.GetOption("description")).ConfigureAwait(false);
                await WriteItemAsync(output, createCommonBatchResult, json, value => value.Message ?? "Action plan batch created.").ConfigureAwait(false);
                return createCommonBatchResult.Succeeded ? 0 : 1;
            case "clone-plan-batch":
                var cloneBatchResult = await application.MessageActionPlanRegistry.CloneAsync(
                    RequireOption(parseResult, "source-batch"),
                    RequireOption(parseResult, "target-batch"),
                    RequireOption(parseResult, "name"),
                    parseResult.GetOption("description")).ConfigureAwait(false);
                await WriteItemAsync(output, cloneBatchResult, json, value => value.Message ?? "Action plan batch cloned.").ConfigureAwait(false);
                return cloneBatchResult.Succeeded ? 0 : 1;
            case "preview-transform-plan-batch":
                var previewTransformBatchResult = await application.MessageActionPlanRegistry.PreviewTransformCloneAsync(
                    RequireOption(parseResult, "source-batch"),
                    BuildMessageActionPlanBatchTransformRequest(parseResult)).ConfigureAwait(false);
                await WriteItemAsync(output, previewTransformBatchResult, json, value => value.Message ?? "Action plan batch transform preview generated.").ConfigureAwait(false);
                return previewTransformBatchResult.Succeeded ? 0 : 1;
            case "transform-plan-batch":
                var transformBatchResult = await application.MessageActionPlanRegistry.TransformCloneAsync(
                    RequireOption(parseResult, "source-batch"),
                    RequireOption(parseResult, "target-batch"),
                    RequireOption(parseResult, "name"),
                    BuildMessageActionPlanBatchTransformRequest(parseResult),
                    parseResult.GetOption("description")).ConfigureAwait(false);
                await WriteItemAsync(output, transformBatchResult, json, value => value.Message ?? "Action plan batch transformed and cloned.").ConfigureAwait(false);
                return transformBatchResult.Succeeded ? 0 : 1;
            case "add-plan-to-batch":
                var planToAppend = await application.MessageActionPlans.CreatePlanAsync(BuildMessageActionExecutionPlanRequest(parseResult)).ConfigureAwait(false);
                var appendPlanResult = await application.MessageActionPlanRegistry.AppendPlanAsync(
                    RequireOption(parseResult, "batch"),
                    planToAppend).ConfigureAwait(false);
                await WriteItemAsync(output, appendPlanResult, json, value => value.Message ?? "Action plan appended.").ConfigureAwait(false);
                return appendPlanResult.Succeeded ? 0 : 1;
            case "add-plan-file-to-batch":
                var appendImportedPlanResult = await application.MessageActionPlanRegistry.AppendImportedPlanAsync(
                    RequireOption(parseResult, "batch"),
                    RequireOption(parseResult, "path")).ConfigureAwait(false);
                await WriteItemAsync(output, appendImportedPlanResult, json, value => value.Message ?? "Imported action plan appended.").ConfigureAwait(false);
                return appendImportedPlanResult.Succeeded ? 0 : 1;
            case "replace-plan-in-batch":
                var replaceIndex = parseResult.GetIntOption("index");
                if (!replaceIndex.HasValue) {
                    throw new InvalidOperationException("Missing required option '--index'.");
                }
                var replacementPlan = await application.MessageActionPlans.CreatePlanAsync(BuildMessageActionExecutionPlanRequest(parseResult)).ConfigureAwait(false);
                var replacePlanResult = await application.MessageActionPlanRegistry.ReplacePlanAtAsync(
                    RequireOption(parseResult, "batch"),
                    replaceIndex.Value,
                    replacementPlan).ConfigureAwait(false);
                await WriteItemAsync(output, replacePlanResult, json, value => value.Message ?? "Action plan replaced.").ConfigureAwait(false);
                return replacePlanResult.Succeeded ? 0 : 1;
            case "replace-plan-file-in-batch":
                var replaceImportedIndex = parseResult.GetIntOption("index");
                if (!replaceImportedIndex.HasValue) {
                    throw new InvalidOperationException("Missing required option '--index'.");
                }
                var replaceImportedPlanResult = await application.MessageActionPlanRegistry.ReplaceImportedPlanAtAsync(
                    RequireOption(parseResult, "batch"),
                    replaceImportedIndex.Value,
                    RequireOption(parseResult, "path")).ConfigureAwait(false);
                await WriteItemAsync(output, replaceImportedPlanResult, json, value => value.Message ?? "Imported action plan replaced.").ConfigureAwait(false);
                return replaceImportedPlanResult.Succeeded ? 0 : 1;
            case "remove-plan-from-batch":
                var index = parseResult.GetIntOption("index");
                if (!index.HasValue) {
                    throw new InvalidOperationException("Missing required option '--index'.");
                }
                var removePlanResult = await application.MessageActionPlanRegistry.RemovePlanAtAsync(
                    RequireOption(parseResult, "batch"),
                    index.Value).ConfigureAwait(false);
                await WriteItemAsync(output, removePlanResult, json, value => value.Message ?? "Action plan removed from batch.").ConfigureAwait(false);
                return removePlanResult.Succeeded ? 0 : 1;
            case "delete-plan-batch":
                var deleteBatchResult = await application.MessageActionPlanRegistry.DeleteAsync(RequireOption(parseResult, "batch")).ConfigureAwait(false);
                await WriteItemAsync(output, deleteBatchResult, json, value => value.Message ?? "Action plan batch deleted.").ConfigureAwait(false);
                return deleteBatchResult.Succeeded ? 0 : 1;
            case "execute-plan-batch-stored":
                var storedBatchExecutionResult = await application.MessageActionPlanRegistry.ExecuteAsync(
                    RequireOption(parseResult, "batch"),
                    continueOnError: !parseResult.HasFlag("stop-on-error")).ConfigureAwait(false);
                await WriteItemAsync(output, storedBatchExecutionResult, json, value => value.Message ?? "Stored action plan batch executed.").ConfigureAwait(false);
                return storedBatchExecutionResult.Succeeded ? 0 : 1;
            case "preview-move":
                var preview = await application.MessageActionPreview.PreviewMoveAsync(new MoveMessagesPreviewRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageIds = parseResult.GetOptionValues("message-id")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToList(),
                    DestinationFolderId = RequireOption(parseResult, "target-folder")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, preview, json, value => value.Message ?? "Move preview ready.").ConfigureAwait(false);
                return preview.Succeeded ? 0 : 1;
            case "preview-actions":
                var standardPreview = await application.MessageActionPreview.PreviewStandardActionsAsync(new StandardMessageActionsPreviewRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageIds = parseResult.GetOptionValues("message-id")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToList(),
                    DestinationFolderId = parseResult.GetOption("target-folder")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, standardPreview, json, value => value.Message ?? "Action previews ready.").ConfigureAwait(false);
                return standardPreview.Succeeded ? 0 : 1;
            case "preview-all":
                var commonPreview = await application.MessageActionPreview.PreviewCommonActionsAsync(new CommonMessageActionsPreviewRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageIds = parseResult.GetOptionValues("message-id")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToList(),
                    DestinationFolderId = parseResult.GetOption("target-folder")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, commonPreview, json, value => value.Message ?? "Common action previews ready.").ConfigureAwait(false);
                return commonPreview.Succeeded ? 0 : 1;
            case "plan-action":
                var actionPlan = await application.MessageActionPlans.CreatePlanAsync(BuildMessageActionExecutionPlanRequest(parseResult)).ConfigureAwait(false);
                await WriteItemAsync(output, actionPlan, json, value => value.Message ?? "Action plan ready.").ConfigureAwait(false);
                return actionPlan.Succeeded ? 0 : 1;
            case "export-plan":
                var exportPlan = await application.MessageActionPlans.CreatePlanAsync(BuildMessageActionExecutionPlanRequest(parseResult)).ConfigureAwait(false);
                await application.MessageActionPlanExchange.SaveAsync(RequireOption(parseResult, "path"), exportPlan).ConfigureAwait(false);
                var exportPlanResult = OperationResult.Success("Action plan exported.");
                await WriteItemAsync(output, exportPlanResult, json, value => value.Message ?? "Action plan exported.").ConfigureAwait(false);
                return exportPlan.Succeeded ? 0 : 1;
            case "show-plan":
                var loadedPlan = await application.MessageActionPlanExchange.LoadAsync(RequireOption(parseResult, "path")).ConfigureAwait(false);
                await WriteItemAsync(output, loadedPlan, json, value => value.Message ?? "Action plan loaded.").ConfigureAwait(false);
                return loadedPlan.Succeeded ? 0 : 1;
            case "execute-plan":
                var executionPlan = await application.MessageActionPlans.CreatePlanAsync(BuildMessageActionExecutionPlanRequest(parseResult)).ConfigureAwait(false);
                var executePlanResult = await application.MessageActionBatch.ExecuteAsync(new[] { executionPlan }).ConfigureAwait(false);
                await WriteItemAsync(output, executePlanResult, json, value => value.Message ?? "Action plan executed.").ConfigureAwait(false);
                return executePlanResult.Succeeded ? 0 : 1;
            case "execute-plan-file":
                var storedPlan = await application.MessageActionPlanExchange.LoadAsync(RequireOption(parseResult, "path")).ConfigureAwait(false);
                var executePlanFileResult = await application.MessageActionBatch.ExecuteAsync(new[] { storedPlan }).ConfigureAwait(false);
                await WriteItemAsync(output, executePlanFileResult, json, value => value.Message ?? "Stored action plan executed.").ConfigureAwait(false);
                return executePlanFileResult.Succeeded ? 0 : 1;
            case "execute-plan-batch":
                var plans = await application.MessageActionPlanExchange.LoadBatchAsync(RequireOption(parseResult, "path")).ConfigureAwait(false);
                var batchExecutionResult = await application.MessageActionBatch.ExecuteAsync(
                    plans,
                    continueOnError: !parseResult.HasFlag("stop-on-error")).ConfigureAwait(false);
                await WriteItemAsync(output, batchExecutionResult, json, value => value.Message ?? "Action batch executed.").ConfigureAwait(false);
                return batchExecutionResult.Succeeded ? 0 : 1;
            case "preview-delete":
                var deletePreview = await application.MessageActionPreview.PreviewDeleteAsync(new DeleteMessagesPreviewRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageIds = parseResult.GetOptionValues("message-id")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToList()
                }).ConfigureAwait(false);
                await WriteItemAsync(output, deletePreview, json, value => value.Message ?? "Delete preview ready.").ConfigureAwait(false);
                return deletePreview.Succeeded ? 0 : 1;
            case "preview-mark-read":
                var readPreview = await application.MessageActionPreview.PreviewReadStateAsync(new SetReadStateRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageIds = parseResult.GetOptionValues("message-id")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToList(),
                    IsRead = !parseResult.HasFlag("unread")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, readPreview, json, value => value.Message ?? "Read-state preview ready.").ConfigureAwait(false);
                return readPreview.Succeeded ? 0 : 1;
            case "preview-flag":
                var flagPreview = await application.MessageActionPreview.PreviewFlaggedStateAsync(new SetFlaggedStateRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageIds = parseResult.GetOptionValues("message-id")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToList(),
                    IsFlagged = !parseResult.HasFlag("unflag")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, flagPreview, json, value => value.Message ?? "Flag preview ready.").ConfigureAwait(false);
                return flagPreview.Succeeded ? 0 : 1;
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
            case "get-many":
                var getMessagesRequest = new GetMessagesRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageIds = parseResult.GetOptionValues("message-id")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToList(),
                    IncludeRawContent = parseResult.HasFlag("include-raw")
                };
                if (getMessagesRequest.MessageIds.Count == 0) {
                    throw new InvalidOperationException("Missing required option '--message-id'.");
                }
                if (parseResult.HasFlag("compact")) {
                    var compactDetails = await application.Read.GetMessagesCompactAsync(getMessagesRequest).ConfigureAwait(false);
                    await WriteSequenceAsync(output, compactDetails, json, value => value.SummaryText).ConfigureAwait(false);
                    return 0;
                }
                var details = await application.Read.GetMessagesAsync(getMessagesRequest).ConfigureAwait(false);
                await WriteSequenceAsync(output, details, json, value => value.Summary?.Subject ?? value.Id).ConfigureAwait(false);
                return 0;
            case "mark-read":
                var markReadResult = await application.MessageActions.SetReadStateAsync(new SetReadStateRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageIds = parseResult.GetOptionValues("message-id")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToList(),
                    IsRead = !parseResult.HasFlag("unread"),
                    ConfirmationToken = parseResult.GetOption("confirm-token")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, markReadResult, json, value =>
                    value.Message ?? $"Updated {value.SucceededCount} message(s).").ConfigureAwait(false);
                return markReadResult.Succeeded ? 0 : 1;
            case "flag":
                var flagResult = await application.MessageActions.SetFlaggedStateAsync(new SetFlaggedStateRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageIds = parseResult.GetOptionValues("message-id")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToList(),
                    IsFlagged = !parseResult.HasFlag("unflag"),
                    ConfirmationToken = parseResult.GetOption("confirm-token")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, flagResult, json, value =>
                    value.Message ?? $"Updated {value.SucceededCount} message(s).").ConfigureAwait(false);
                return flagResult.Succeeded ? 0 : 1;
            case "move":
                var moveResult = await application.MessageActions.MoveAsync(new MoveMessagesRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageIds = parseResult.GetOptionValues("message-id")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToList(),
                    DestinationFolderId = RequireOption(parseResult, "target-folder"),
                    ConfirmationToken = parseResult.GetOption("confirm-token")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, moveResult, json, value =>
                    value.Message ?? $"Moved {value.SucceededCount} message(s).").ConfigureAwait(false);
                return moveResult.Succeeded ? 0 : 1;
            case "archive":
                var archiveResult = await application.MessageActions.MoveAsync(new MoveMessagesRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageIds = parseResult.GetOptionValues("message-id")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToList(),
                    DestinationFolderId = MailFolderAliases.Archive,
                    ConfirmationToken = parseResult.GetOption("confirm-token")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, archiveResult, json, value =>
                    value.Message ?? $"Archived {value.SucceededCount} message(s).").ConfigureAwait(false);
                return archiveResult.Succeeded ? 0 : 1;
            case "trash":
                var trashResult = await application.MessageActions.MoveAsync(new MoveMessagesRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageIds = parseResult.GetOptionValues("message-id")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToList(),
                    DestinationFolderId = MailFolderAliases.Trash,
                    ConfirmationToken = parseResult.GetOption("confirm-token")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, trashResult, json, value =>
                    value.Message ?? $"Moved {value.SucceededCount} message(s) to trash.").ConfigureAwait(false);
                return trashResult.Succeeded ? 0 : 1;
            case "delete":
                var deleteResult = await application.MessageActions.DeleteAsync(new DeleteMessagesRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageIds = parseResult.GetOptionValues("message-id")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToList(),
                    ConfirmationToken = parseResult.GetOption("confirm-token")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, deleteResult, json, value =>
                    value.Message ?? $"Deleted {value.SucceededCount} message(s).").ConfigureAwait(false);
                return deleteResult.Succeeded ? 0 : 1;
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
            case "save-attachments-many":
                var saveAttachmentsManyResult = await application.Read.SaveAttachmentsManyAsync(new SaveAttachmentsManyRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageIds = parseResult.GetOptionValues("message-id")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToList(),
                    DestinationPath = RequireOption(parseResult, "path"),
                    AttachmentIds = parseResult.GetOptionValues("attachment-id")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToList(),
                    FileNameContains = parseResult.GetOption("name-contains"),
                    ContentTypeContains = parseResult.GetOption("content-type"),
                    Overwrite = parseResult.HasFlag("overwrite")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, saveAttachmentsManyResult, json, value =>
                    value.Message ?? $"Saved {value.SavedCount} attachment(s) across {value.AttemptedMessageCount} message(s).").ConfigureAwait(false);
                return saveAttachmentsManyResult.Succeeded ? 0 : 1;
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

    private static MessageActionExecutionPlanRequest BuildMessageActionExecutionPlanRequest(CliArguments parseResult) => new() {
        Action = RequireOption(parseResult, "action"),
        ProfileId = RequireOption(parseResult, "profile"),
        MailboxId = parseResult.GetOption("mailbox"),
        FolderId = parseResult.GetOption("folder"),
        MessageIds = parseResult.GetOptionValues("message-id")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList(),
        DestinationFolderId = parseResult.GetOption("target-folder"),
        ConfirmationToken = parseResult.GetOption("confirm-token")
    };

    private static CommonMessageActionsPreviewRequest BuildCommonActionsPreviewRequest(CliArguments parseResult) => new() {
        ProfileId = RequireOption(parseResult, "profile"),
        MailboxId = parseResult.GetOption("mailbox"),
        FolderId = parseResult.GetOption("folder"),
        MessageIds = parseResult.GetOptionValues("message-id")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList(),
        DestinationFolderId = parseResult.GetOption("target-folder")
    };

    private static MessageActionPlanBatchTransformRequest BuildMessageActionPlanBatchTransformRequest(CliArguments parseResult) => new() {
        PlanIndexes = parseResult.GetIntOptionValues("index").ToList(),
        PlanNames = parseResult.GetOptionValues("plan-name")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList(),
        ProfileId = parseResult.GetOption("target-profile"),
        MailboxId = parseResult.GetOption("mailbox"),
        FolderId = parseResult.GetOption("folder"),
        DestinationFolderId = parseResult.GetOption("target-folder")
    };

    private static MailMessageActionPlanBatchQuery? BuildMessageActionPlanBatchQuery(CliArguments parseResult) {
        var planNames = parseResult.GetOptionValues("plan-name")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var profileIds = parseResult.GetOptionValues("profile")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var actions = parseResult.GetOptionValues("action")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var hasExplicitSort = parseResult.GetOptionValues("sort").Count > 0;
        var sortBy = ParseBatchSortBy(parseResult.GetOption("sort"));
        var descending = parseResult.HasFlag("desc");

        if (planNames.Count == 0 && profileIds.Count == 0 && actions.Count == 0 && !hasExplicitSort && sortBy == MailMessageActionPlanBatchSortBy.Id && !descending) {
            return null;
        }

        return new MailMessageActionPlanBatchQuery {
            PlanNames = planNames,
            ProfileIds = profileIds,
            Actions = actions,
            SortBy = sortBy,
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
        output.WriteLine("  mail folder-aliases --profile <id> [--mailbox <id>] [--json]");
        output.WriteLine("  mail resolve-folder --profile <id> --target-folder <id> [--mailbox <id>] [--json]");
        output.WriteLine("  mail list-plan-batches [--summary|--compact] [--plan-name <name>] [--plan-name <name>] [--profile <id>] [--profile <id>] [--action <name>] [--action <name>] [--sort <id|name|plans|ready|updated|actions>] [--desc] [--json]");
        output.WriteLine("  mail show-plan-batch --batch <id> [--summary|--compact] [--json]");
        output.WriteLine("  mail import-plan-batch --batch <id> --name <display-name> --path <file> [--description <text>] [--json]");
        output.WriteLine("  mail export-plan-batch --batch <id> --path <file> [--json]");
        output.WriteLine("  mail create-common-plan-batch --batch <id> --name <display-name> --profile <id> --message-id <id> [--message-id <id>] [--action <name>] [--action <name>] [--target-folder <id>] [--mailbox <id>] [--folder <name>] [--description <text>] [--json]");
        output.WriteLine("  mail clone-plan-batch --source-batch <id> --target-batch <id> --name <display-name> [--description <text>] [--json]");
        output.WriteLine("  mail preview-transform-plan-batch --source-batch <id> [--index <n>] [--index <n>] [--plan-name <name>] [--plan-name <name>] [--target-profile <id>] [--mailbox <id>] [--folder <name>] [--target-folder <id>] [--json]");
        output.WriteLine("  mail transform-plan-batch --source-batch <id> --target-batch <id> --name <display-name> [--index <n>] [--index <n>] [--plan-name <name>] [--plan-name <name>] [--target-profile <id>] [--mailbox <id>] [--folder <name>] [--target-folder <id>] [--description <text>] [--json]");
        output.WriteLine("  mail add-plan-to-batch --batch <id> --action <mark-read|mark-unread|flag|unflag|archive|trash|move|delete> --profile <id> --message-id <id> [--message-id <id>] [--target-folder <id>] [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail add-plan-file-to-batch --batch <id> --path <file> [--json]");
        output.WriteLine("  mail replace-plan-in-batch --batch <id> --index <n> --action <mark-read|mark-unread|flag|unflag|archive|trash|move|delete> --profile <id> --message-id <id> [--message-id <id>] [--target-folder <id>] [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail replace-plan-file-in-batch --batch <id> --index <n> --path <file> [--json]");
        output.WriteLine("  mail remove-plan-from-batch --batch <id> --index <n> [--json]");
        output.WriteLine("  mail delete-plan-batch --batch <id> [--json]");
        output.WriteLine("  mail execute-plan-batch-stored --batch <id> [--stop-on-error] [--json]");
        output.WriteLine("  mail plan-action --action <mark-read|mark-unread|flag|unflag|archive|trash|move|delete> --profile <id> --message-id <id> [--message-id <id>] [--target-folder <id>] [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail export-plan --action <mark-read|mark-unread|flag|unflag|archive|trash|move|delete> --profile <id> --message-id <id> [--message-id <id>] --path <file> [--target-folder <id>] [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail show-plan --path <file> [--json]");
        output.WriteLine("  mail execute-plan --action <mark-read|mark-unread|flag|unflag|archive|trash|move|delete> --profile <id> --message-id <id> [--message-id <id>] [--target-folder <id>] [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail execute-plan-file --path <file> [--json]");
        output.WriteLine("  mail execute-plan-batch --path <file> [--stop-on-error] [--json]");
        output.WriteLine("  mail preview-all --profile <id> --message-id <id> [--message-id <id>] [--target-folder <id>] [--mailbox <id>] [--folder <name>] [--json]");
        output.WriteLine("  mail preview-mark-read --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--unread] [--json]");
        output.WriteLine("  mail preview-flag --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--unflag] [--json]");
        output.WriteLine("  mail preview-actions --profile <id> --message-id <id> [--message-id <id>] [--target-folder <id>] [--mailbox <id>] [--folder <name>] [--json]");
        output.WriteLine("  mail preview-move --profile <id> --message-id <id> [--message-id <id>] --target-folder <id> [--mailbox <id>] [--folder <name>] [--json]");
        output.WriteLine("  mail preview-delete --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--json]");
        output.WriteLine("  mail search --profile <id> [--mailbox <id>] [--folder <name>] [--query <text>] [--subject <text>] [--from <text>] [--to <text>] [--limit <n>] [--compact] [--json]");
        output.WriteLine("  mail get --profile <id> --message-id <id> [--mailbox <id>] [--folder <name>] [--include-raw] [--compact] [--json]");
        output.WriteLine("  mail get-many --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--include-raw] [--compact] [--json]");
        output.WriteLine("  mail mark-read --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--unread] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail flag --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--unflag] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail archive --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail trash --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail move --profile <id> --message-id <id> [--message-id <id>] --target-folder <id> [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail delete --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail attachments --profile <id> --message-id <id> [--mailbox <id>] [--folder <name>] [--json]");
        output.WriteLine("  mail save-attachment --profile <id> --message-id <id> --attachment-id <id> --path <destination> [--mailbox <id>] [--folder <name>] [--overwrite] [--json]");
        output.WriteLine("  mail save-attachments --profile <id> --message-id <id> --path <destination> [--mailbox <id>] [--folder <name>] [--attachment-id <id>] [--attachment-id <id>] [--name-contains <text>] [--content-type <text>] [--overwrite] [--json]");
        output.WriteLine("  mail save-attachments-many --profile <id> --message-id <id> [--message-id <id>] --path <destination> [--mailbox <id>] [--folder <name>] [--attachment-id <id>] [--attachment-id <id>] [--name-contains <text>] [--content-type <text>] [--overwrite] [--json]");
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
        output.WriteLine("  --plan-batches-dir <path>");
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
