using Mailozaurr;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Mailozaurr.Cli.Mcp;

public sealed partial class MailMcpTools {
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
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

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
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

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Lists provider-neutral folder aliases for a profile, resolving them to provider folders when possible.")]
    public Task<IReadOnlyList<MailFolderAliasSummary>> mail_folder_aliases_list(
        [Description("The profile identifier to query.")] string profileId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        CancellationToken cancellationToken = default) =>
        _application.FolderAliases.GetAliasesAsync(profileId, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Resolves a requested folder target to a provider-neutral alias or an effective provider folder destination.")]
    public Task<MailFolderTargetResolution> mail_folder_resolve(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The requested target folder identifier or alias.")] string targetFolderId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        CancellationToken cancellationToken = default) =>
        _application.FolderAliases.ResolveAsync(profileId, targetFolderId, mailboxId, cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
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

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists persisted reusable message action plan batches.")]
    public Task<IReadOnlyList<MailMessageActionPlanBatch>> mail_action_batch_store_list(
        [Description("Optional human-readable plan names that must exist in the returned batch.")] IReadOnlyList<string>? planNames = null,
        [Description("Optional profile identifiers that must be referenced by the returned batch.")] IReadOnlyList<string>? profileIds = null,
        [Description("Optional normalized action names that must be referenced by the returned batch.")] IReadOnlyList<string>? actions = null,
        [Description("Optional sort key: id, name, plans, ready, updated, or actions.")] string? sortBy = null,
        [Description("When true, reverses the selected sort order.")] bool descending = false,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.GetBatchesAsync(BuildBatchQuery(planNames, profileIds, actions, sortBy, descending), cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists persisted reusable message action plan batches using a lightweight projection.")]
    public Task<IReadOnlyList<MailMessageActionPlanBatchCompact>> mail_action_batch_store_compact_list(
        [Description("Optional human-readable plan names that must exist in the returned batch.")] IReadOnlyList<string>? planNames = null,
        [Description("Optional profile identifiers that must be referenced by the returned batch.")] IReadOnlyList<string>? profileIds = null,
        [Description("Optional normalized action names that must be referenced by the returned batch.")] IReadOnlyList<string>? actions = null,
        [Description("Optional sort key: id, name, plans, ready, updated, or actions.")] string? sortBy = null,
        [Description("When true, reverses the selected sort order.")] bool descending = false,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.GetBatchesCompactAsync(BuildBatchQuery(planNames, profileIds, actions, sortBy, descending), cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists persisted reusable message action plan batches using a richer summary projection.")]
    public Task<IReadOnlyList<MailMessageActionPlanBatchSummary>> mail_action_batch_store_summary_list(
        [Description("Optional human-readable plan names that must exist in the returned batch.")] IReadOnlyList<string>? planNames = null,
        [Description("Optional profile identifiers that must be referenced by the returned batch.")] IReadOnlyList<string>? profileIds = null,
        [Description("Optional normalized action names that must be referenced by the returned batch.")] IReadOnlyList<string>? actions = null,
        [Description("Optional sort key: id, name, plans, ready, updated, or actions.")] string? sortBy = null,
        [Description("When true, reverses the selected sort order.")] bool descending = false,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.GetBatchesSummaryAsync(BuildBatchQuery(planNames, profileIds, actions, sortBy, descending), cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Gets one persisted reusable message action plan batch by identifier.")]
    public async Task<MailMessageActionPlanBatch> mail_action_batch_store_get(
        [Description("The persisted batch identifier to retrieve.")] string batchId,
        CancellationToken cancellationToken = default) {
        var batch = await _application.MessageActionPlanRegistry.GetBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        return batch ?? throw new InvalidOperationException($"Action plan batch '{batchId}' was not found.");
    }

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Gets one persisted reusable message action plan batch by identifier using a lightweight projection.")]
    public async Task<MailMessageActionPlanBatchCompact> mail_action_batch_store_compact_get(
        [Description("The persisted batch identifier to retrieve.")] string batchId,
        CancellationToken cancellationToken = default) {
        var batch = await _application.MessageActionPlanRegistry.GetBatchCompactAsync(batchId, cancellationToken).ConfigureAwait(false);
        return batch ?? throw new InvalidOperationException($"Action plan batch '{batchId}' was not found.");
    }

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Gets one persisted reusable message action plan batch by identifier using a richer summary projection.")]
    public async Task<MailMessageActionPlanBatchSummary> mail_action_batch_store_summary_get(
        [Description("The persisted batch identifier to retrieve.")] string batchId,
        CancellationToken cancellationToken = default) {
        var batch = await _application.MessageActionPlanRegistry.GetBatchSummaryAsync(batchId, cancellationToken).ConfigureAwait(false);
        return batch ?? throw new InvalidOperationException($"Action plan batch '{batchId}' was not found.");
    }

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Imports a persisted reusable message action plan batch from an external batch file.")]
    public Task<OperationResult> mail_action_batch_store_import(
        [Description("The persisted batch identifier to create or update.")] string batchId,
        [Description("The human-readable batch name.")] string name,
        [Description("The source batch file path on the server filesystem.")] string path,
        [Description("Optional operator-facing description.")] string? description = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.ImportAsync(batchId, name, path, description, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Exports a persisted reusable message action plan batch to an external batch file.")]
    public Task<OperationResult> mail_action_batch_store_export(
        [Description("The persisted batch identifier to export.")] string batchId,
        [Description("The destination batch file path on the server filesystem.")] string path,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.ExportAsync(batchId, path, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = true)]
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

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
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

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Clones an existing persisted reusable message action plan batch to a new identifier and name.")]
    public Task<OperationResult> mail_action_batch_store_clone(
        [Description("The persisted source batch identifier to clone.")] string sourceBatchId,
        [Description("The persisted target batch identifier to create.")] string targetBatchId,
        [Description("The human-readable name for the cloned batch.")] string name,
        [Description("Optional operator-facing description.")] string? description = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.CloneAsync(sourceBatchId, targetBatchId, name, description, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
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

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
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

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
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

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Loads one normalized action plan from a file and appends it to an existing persisted batch.")]
    public Task<OperationResult> mail_action_batch_store_append_imported_plan(
        [Description("The persisted batch identifier to update.")] string batchId,
        [Description("The source plan file path on the server filesystem.")] string path,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.AppendImportedPlanAsync(batchId, path, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = true)]
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

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Loads one normalized action plan from a file and replaces a stored plan by zero-based index.")]
    public Task<OperationResult> mail_action_batch_store_replace_imported_plan(
        [Description("The persisted batch identifier to update.")] string batchId,
        [Description("The zero-based plan index to replace.")] int index,
        [Description("The source plan file path on the server filesystem.")] string path,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.ReplaceImportedPlanAtAsync(batchId, index, path, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Removes one plan from an existing persisted batch by zero-based index.")]
    public Task<OperationResult> mail_action_batch_store_remove_plan(
        [Description("The persisted batch identifier to update.")] string batchId,
        [Description("The zero-based plan index to remove.")] int index,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.RemovePlanAtAsync(batchId, index, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Deletes a persisted reusable message action plan batch.")]
    public Task<OperationResult> mail_action_batch_store_delete(
        [Description("The persisted batch identifier to delete.")] string batchId,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.DeleteAsync(batchId, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("Executes a persisted reusable message action plan batch through the shared batch-execution service.")]
    public Task<MessageActionBatchExecutionResult> mail_action_batch_store_execute(
        [Description("The persisted batch identifier to execute.")] string batchId,
        [Description("When true, continues after failures. When false, later plans are skipped after the first failure.")] bool continueOnError = true,
        [Description("Optional confirmation tokens returned by matching plan previews or transform previews. Provide one token for each protected plan that should execute.")] string[]? confirmationTokens = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanRegistry.ExecuteAsync(batchId, continueOnError, cancellationToken, confirmationTokens);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = true)]
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

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Loads one normalized action plan from a file through the shared Mailozaurr plan-exchange service.")]
    public Task<MessageActionExecutionPlan> mail_action_plan_import(
        [Description("The source file path on the server filesystem.")] string path,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanExchange.LoadAsync(path, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
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

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Loads a batch of normalized action plans from a file through the shared Mailozaurr plan-exchange service.")]
    public Task<IReadOnlyList<MessageActionExecutionPlan>> mail_action_batch_import(
        [Description("The source file path on the server filesystem.")] string path,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPlanExchange.LoadBatchAsync(path, cancellationToken);

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = true)]
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

    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("Executes a batch of prebuilt normalized message action plans through the shared Mailozaurr batch-execution service.")]
    public Task<MessageActionBatchExecutionResult> mail_action_batch_execute(
        [Description("The normalized action plans to execute.")] MessageActionExecutionPlan[] plans,
        [Description("When true, continues after failures. When false, later plans are skipped after the first failure.")] bool continueOnError = true,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionBatch.ExecuteAsync(
            plans?.ToList() ?? new List<MessageActionExecutionPlan>(),
            continueOnError,
            cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
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

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
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

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
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

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
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
}
