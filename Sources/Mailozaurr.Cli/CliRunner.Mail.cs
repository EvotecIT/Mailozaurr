using Mailozaurr;
using Mailozaurr.Cli.Mcp;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Mailozaurr.Cli;

public static partial class CliRunner {
    private static async Task<int> ExecuteMailAsync(
        MailApplication application,
        CliArguments parseResult,
        TextWriter output,
        TextWriter error) {
        if (parseResult.Positionals.Count < 2) {
            await error.WriteLineAsync("Missing mail command. Use 'mail --help' to list mailbox operations.").ConfigureAwait(false);
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
                    continueOnError: !parseResult.HasFlag("stop-on-error"),
                    confirmationTokens: parseResult.GetOptionValues("confirm-token")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToArray()).ConfigureAwait(false);
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
            case "export-eml":
                var maxBytes = parseResult.GetIntOption("max-bytes");
                var emlResult = await application.EmlExport.ExportAsync(new MailEmlExportRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    MessageIds = parseResult.GetOptionValues("message-id")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToList(),
                    DestinationDirectory = RequireOption(parseResult, "path"),
                    MaxMessageBytes = maxBytes ?? 64 * 1024 * 1024,
                    Overwrite = parseResult.HasFlag("overwrite")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, emlResult, json, value =>
                    value.Message ?? $"Exported {value.ExportedCount} EML message(s).").ConfigureAwait(false);
                return emlResult.Succeeded ? 0 : 1;
            case "changes":
                var changeResult = await application.ChangeFeeds.GetChangesAsync(new MailChangeFeedRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderId = parseResult.GetOption("folder"),
                    Cursor = parseResult.GetOption("cursor"),
                    MaxChanges = parseResult.GetIntOption("limit") ?? 100
                }).ConfigureAwait(false);
                await WriteItemAsync(output, changeResult, json, value =>
                    $"{value.Provider} {value.Changes.Count} change(s); cursor={value.NextCursor ?? "(none)"}; reset={value.ResetRequired}").ConfigureAwait(false);
                return changeResult.ResetRequired ? 2 : 0;
            case "wait-changes":
                var waitResult = await application.ChangeFeeds.WaitForChangesAsync(new MailChangeWaitRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    FolderId = parseResult.GetOption("folder"),
                    MaxChanges = parseResult.GetIntOption("limit") ?? 1,
                    Timeout = TimeSpan.FromSeconds(parseResult.GetIntOption("timeout-seconds") ?? 30)
                }).ConfigureAwait(false);
                await WriteItemAsync(output, waitResult, json, value =>
                    $"{value.Provider} {value.Changes.Count} live change(s)").ConfigureAwait(false);
                return 0;
            case "subscribe-changes":
                var expirationRaw = parseResult.GetOption("expiration");
                DateTimeOffset? expiration = null;
                if (!string.IsNullOrWhiteSpace(expirationRaw)) {
                    if (!DateTimeOffset.TryParse(expirationRaw, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.RoundtripKind, out var parsedExpiration)) {
                        throw new InvalidOperationException("Option '--expiration' must be an ISO 8601 timestamp.");
                    }
                    expiration = parsedExpiration;
                }
                var subscriptionResult = await application.ChangeFeeds.SubscribeAsync(new MailChangeSubscriptionRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    FolderIds = parseResult.GetOptionValues("folder")
                        .Select(value => value ?? string.Empty)
                        .ToList(),
                    NotificationUrl = parseResult.GetOption("notification-url"),
                    ClientState = parseResult.GetOption("client-state"),
                    Expiration = expiration,
                    SubscriptionId = parseResult.GetOption("subscription-id"),
                    TopicName = parseResult.GetOption("topic")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, subscriptionResult, json, value =>
                    $"{value.Provider} subscription {value.SubscriptionId ?? value.Cursor ?? "active"}").ConfigureAwait(false);
                return subscriptionResult.Succeeded ? 0 : 1;
            case "unsubscribe-changes":
                var unsubscribeResult = await application.ChangeFeeds.UnsubscribeAsync(new MailChangeUnsubscribeRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    MailboxId = parseResult.GetOption("mailbox"),
                    SubscriptionId = parseResult.GetOption("subscription-id"),
                    TreatMissingAsSuccess = !parseResult.HasFlag("strict")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, unsubscribeResult, json, value =>
                    $"{value.Provider} subscription removed; already-missing={value.AlreadyMissing}").ConfigureAwait(false);
                return unsubscribeResult.Succeeded ? 0 : 1;
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
}
