namespace Mailozaurr.Application;

/// <summary>
/// Implements reusable lifecycle operations for persisted message action plan batches.
/// </summary>
public sealed class MailMessageActionPlanRegistryService : IMailMessageActionPlanRegistryService {
    private readonly IMailMessageActionPlanBatchStore _store;
    private readonly IMailMessageActionPlanExchangeService _exchangeService;
    private readonly IMailMessageActionPreviewService _previewService;
    private readonly IMailMessageActionPlanService _planService;
    private readonly IMailMessageActionBatchService _batchService;
    private readonly IMailProfileStore _profileStore;

    /// <summary>
    /// Creates a new persisted action plan registry service.
    /// </summary>
    public MailMessageActionPlanRegistryService(
        IMailMessageActionPlanBatchStore store,
        IMailMessageActionPlanExchangeService exchangeService,
        IMailMessageActionPreviewService previewService,
        IMailMessageActionPlanService planService,
        IMailMessageActionBatchService batchService,
        IMailProfileStore profileStore) {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _exchangeService = exchangeService ?? throw new ArgumentNullException(nameof(exchangeService));
        _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
        _planService = planService ?? throw new ArgumentNullException(nameof(planService));
        _batchService = batchService ?? throw new ArgumentNullException(nameof(batchService));
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MailMessageActionPlanBatch>> GetBatchesAsync(MailMessageActionPlanBatchQuery? query = null, CancellationToken cancellationToken = default) =>
        ApplyBatchQuery(await _store.GetAllAsync(cancellationToken).ConfigureAwait(false), query);

    /// <inheritdoc />
    public async Task<IReadOnlyList<MailMessageActionPlanBatchCompact>> GetBatchesCompactAsync(MailMessageActionPlanBatchQuery? query = null, CancellationToken cancellationToken = default) =>
        ApplyBatchQuery(await _store.GetAllAsync(cancellationToken).ConfigureAwait(false), query)
        .Select(ToCompact)
        .ToArray();

    /// <inheritdoc />
    public async Task<IReadOnlyList<MailMessageActionPlanBatchSummary>> GetBatchesSummaryAsync(MailMessageActionPlanBatchQuery? query = null, CancellationToken cancellationToken = default) =>
        ApplyBatchQuery(await _store.GetAllAsync(cancellationToken).ConfigureAwait(false), query)
        .Select(ToSummary)
        .ToArray();

    /// <inheritdoc />
    public Task<MailMessageActionPlanBatch?> GetBatchAsync(string batchId, CancellationToken cancellationToken = default) =>
        _store.GetByIdAsync(batchId, cancellationToken);

    /// <inheritdoc />
    public async Task<MailMessageActionPlanBatchCompact?> GetBatchCompactAsync(string batchId, CancellationToken cancellationToken = default) {
        var batch = await _store.GetByIdAsync(batchId, cancellationToken).ConfigureAwait(false);
        return batch == null ? null : ToCompact(batch);
    }

    /// <inheritdoc />
    public async Task<MailMessageActionPlanBatchSummary?> GetBatchSummaryAsync(string batchId, CancellationToken cancellationToken = default) {
        var batch = await _store.GetByIdAsync(batchId, cancellationToken).ConfigureAwait(false);
        return batch == null ? null : ToSummary(batch);
    }

    /// <inheritdoc />
    public async Task<OperationResult> SaveAsync(MailMessageActionPlanBatch batch, CancellationToken cancellationToken = default) {
        var validation = await ValidateAsync(batch, cancellationToken).ConfigureAwait(false);
        if (!validation.Succeeded) {
            return validation;
        }

        await _store.SaveAsync(batch, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success($"Action plan batch '{batch.Id}' saved.");
    }

    /// <inheritdoc />
    public async Task<OperationResult> CreateCommonBatchAsync(
        string batchId,
        string name,
        CommonMessageActionsPreviewRequest request,
        IReadOnlyList<string>? actions = null,
        string? description = null,
        CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var preview = await _previewService.PreviewCommonActionsAsync(request, cancellationToken).ConfigureAwait(false);
        return await CreateCommonBatchFromPreviewAsync(batchId, name, preview, actions, description, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> CreateCommonBatchFromPreviewAsync(
        string batchId,
        string name,
        CommonMessageActionsPreview preview,
        IReadOnlyList<string>? actions = null,
        string? description = null,
        CancellationToken cancellationToken = default) {
        if (preview == null) {
            throw new ArgumentNullException(nameof(preview));
        }

        var selectedActions = SelectCommonActions(preview, actions);
        var plans = new List<MessageActionExecutionPlan>();
        foreach (var action in selectedActions) {
            var plan = await _planService.CreatePlanAsync(new MessageActionExecutionPlanRequest {
                Action = action.Action,
                ProfileId = preview.ProfileId,
                MailboxId = preview.MailboxId,
                FolderId = preview.FolderId,
                MessageIds = preview.MessageIds.ToList(),
                DestinationFolderId = ResolveDestinationFolderId(action, preview.RequestedDestinationFolderId)
            }, cancellationToken).ConfigureAwait(false);

            if (plan.Succeeded) {
                plans.Add(plan);
            }
        }

        if (plans.Count == 0) {
            return OperationResult.Failure("no_supported_actions", "No supported action plans could be created for this message selection.");
        }

        return await SaveAsync(new MailMessageActionPlanBatch {
            Id = batchId,
            Name = name,
            Description = description,
            Plans = plans
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> CloneAsync(string sourceBatchId, string targetBatchId, string name, string? description = null, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(sourceBatchId)) {
            throw new ArgumentException("Source batch id is required.", nameof(sourceBatchId));
        }
        if (string.IsNullOrWhiteSpace(targetBatchId)) {
            throw new ArgumentException("Target batch id is required.", nameof(targetBatchId));
        }
        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException("Batch name is required.", nameof(name));
        }

        var source = await _store.GetByIdAsync(sourceBatchId.Trim(), cancellationToken).ConfigureAwait(false);
        if (source == null) {
            return OperationResult.Failure("action_plan_batch_not_found", $"Action plan batch '{sourceBatchId.Trim()}' was not found.");
        }

        return await SaveAsync(new MailMessageActionPlanBatch {
            Id = targetBatchId.Trim(),
            Name = name.Trim(),
            Description = description ?? source.Description,
            Plans = source.Plans.Select(ClonePlan).ToList()
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> TransformCloneAsync(
        string sourceBatchId,
        string targetBatchId,
        string name,
        MessageActionPlanBatchTransformRequest transform,
        string? description = null,
        CancellationToken cancellationToken = default) {
        if (transform == null) {
            throw new ArgumentNullException(nameof(transform));
        }
        if (string.IsNullOrWhiteSpace(sourceBatchId)) {
            throw new ArgumentException("Source batch id is required.", nameof(sourceBatchId));
        }
        if (string.IsNullOrWhiteSpace(targetBatchId)) {
            throw new ArgumentException("Target batch id is required.", nameof(targetBatchId));
        }
        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException("Batch name is required.", nameof(name));
        }

        var source = await _store.GetByIdAsync(sourceBatchId.Trim(), cancellationToken).ConfigureAwait(false);
        if (source == null) {
            return OperationResult.Failure("action_plan_batch_not_found", $"Action plan batch '{sourceBatchId.Trim()}' was not found.");
        }

        var selectedPlans = SelectPlans(source, transform, out var selectionError);
        if (selectionError != null) {
            return selectionError;
        }

        return await SaveAsync(new MailMessageActionPlanBatch {
            Id = targetBatchId.Trim(),
            Name = name.Trim(),
            Description = description ?? source.Description,
            Plans = selectedPlans.Select(selection => TransformPlan(selection.Plan, transform)).ToList()
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MailMessageActionPlanBatchTransformPreview> PreviewTransformCloneAsync(
        string sourceBatchId,
        MessageActionPlanBatchTransformRequest transform,
        CancellationToken cancellationToken = default) {
        if (transform == null) {
            throw new ArgumentNullException(nameof(transform));
        }
        if (string.IsNullOrWhiteSpace(sourceBatchId)) {
            throw new ArgumentException("Source batch id is required.", nameof(sourceBatchId));
        }

        var normalizedSourceBatchId = sourceBatchId.Trim();
        var source = await _store.GetByIdAsync(normalizedSourceBatchId, cancellationToken).ConfigureAwait(false);
        if (source == null) {
            return new MailMessageActionPlanBatchTransformPreview {
                Succeeded = false,
                Code = "action_plan_batch_not_found",
                Message = $"Action plan batch '{normalizedSourceBatchId}' was not found.",
                SourceBatchId = normalizedSourceBatchId
            };
        }

        var selectedPlans = SelectPlans(source, transform, out var selectionError);

        var preview = new MailMessageActionPlanBatchTransformPreview {
            Succeeded = selectionError == null,
            Code = selectionError?.Code,
            SourceBatchId = source.Id,
            SourceBatchName = source.Name,
            PlanCount = selectedPlans.Count,
            Message = selectionError?.Message
        };

        if (selectionError != null && !string.IsNullOrWhiteSpace(selectionError.Message)) {
            preview.Errors.Add(selectionError.Message!);
        }

        if (!string.IsNullOrWhiteSpace(transform.ProfileId)) {
            var targetProfileId = transform.ProfileId!.Trim();
            var targetProfile = await _profileStore.GetByIdAsync(targetProfileId, cancellationToken).ConfigureAwait(false);
            preview.TargetProfileExists = targetProfile != null;
            preview.TargetProfileKind = targetProfile?.Kind;
            if (targetProfile == null) {
                preview.Succeeded = false;
                preview.Code = "action_plan_profile_not_found";
                preview.Errors.Add($"Profile '{targetProfileId}' was not found.");
            }
        }

        foreach (var selection in selectedPlans) {
            var sourcePlan = selection.Plan;
            var transformedPlan = TransformPlan(sourcePlan, transform);
            var willChange =
                !string.Equals(sourcePlan.ProfileId, transformedPlan.ProfileId, StringComparison.Ordinal) ||
                !string.Equals(sourcePlan.MailboxId, transformedPlan.MailboxId, StringComparison.Ordinal) ||
                !string.Equals(sourcePlan.FolderId, transformedPlan.FolderId, StringComparison.Ordinal) ||
                !string.Equals(sourcePlan.RequestedDestinationFolderId, transformedPlan.RequestedDestinationFolderId, StringComparison.Ordinal);
            var tokenChanged = !string.Equals(sourcePlan.ConfirmationToken, transformedPlan.ConfirmationToken, StringComparison.Ordinal);

            if (willChange) {
                preview.ChangedPlanCount++;
            }
            if (tokenChanged) {
                preview.ConfirmationTokenChangedCount++;
            }

            preview.Plans.Add(new MessageActionPlanBatchTransformPreviewItem {
                Index = selection.Index,
                Action = sourcePlan.Action,
                ExecutionKind = sourcePlan.ExecutionKind,
                SourceProfileId = sourcePlan.ProfileId,
                TargetProfileId = transformedPlan.ProfileId,
                SourceMailboxId = sourcePlan.MailboxId,
                TargetMailboxId = transformedPlan.MailboxId,
                SourceFolderId = sourcePlan.FolderId,
                TargetFolderId = transformedPlan.FolderId,
                SourceDestinationFolderId = sourcePlan.RequestedDestinationFolderId,
                TargetDestinationFolderId = transformedPlan.RequestedDestinationFolderId,
                WillChange = willChange,
                ConfirmationTokenWillChange = tokenChanged,
                Summary = BuildTransformSummary(sourcePlan, transformedPlan, willChange, tokenChanged)
            });
        }

        if (preview.ChangedPlanCount == 0) {
            preview.Warnings.Add("The requested transform would not change any stored plan values.");
        }

        preview.Message = preview.Succeeded
            ? $"Previewed transform for {preview.PlanCount} plan(s); {preview.ChangedPlanCount} would change and {preview.ConfirmationTokenChangedCount} confirmation token(s) would be regenerated."
            : preview.Message ?? $"Transform preview for batch '{source.Id}' found validation issues.";
        return preview;
    }

    /// <inheritdoc />
    public async Task<OperationResult> AppendPlanAsync(string batchId, MessageActionExecutionPlan plan, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(batchId)) {
            throw new ArgumentException("Batch id is required.", nameof(batchId));
        }
        if (plan == null) {
            throw new ArgumentNullException(nameof(plan));
        }

        var batch = await _store.GetByIdAsync(batchId.Trim(), cancellationToken).ConfigureAwait(false);
        if (batch == null) {
            return OperationResult.Failure("action_plan_batch_not_found", $"Action plan batch '{batchId.Trim()}' was not found.");
        }

        batch.Plans.Add(ClonePlan(plan));
        return await SaveAsync(batch, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> AppendImportedPlanAsync(string batchId, string path, CancellationToken cancellationToken = default) {
        var plan = await _exchangeService.LoadAsync(path, cancellationToken).ConfigureAwait(false);
        return await AppendPlanAsync(batchId, plan, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> ReplacePlanAtAsync(string batchId, int index, MessageActionExecutionPlan plan, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(batchId)) {
            throw new ArgumentException("Batch id is required.", nameof(batchId));
        }
        if (plan == null) {
            throw new ArgumentNullException(nameof(plan));
        }

        var batch = await _store.GetByIdAsync(batchId.Trim(), cancellationToken).ConfigureAwait(false);
        if (batch == null) {
            return OperationResult.Failure("action_plan_batch_not_found", $"Action plan batch '{batchId.Trim()}' was not found.");
        }
        if (index < 0 || index >= batch.Plans.Count) {
            return OperationResult.Failure("action_plan_batch_index_invalid", $"Action plan batch '{batch.Id}' does not contain a plan at index {index}.");
        }

        batch.Plans[index] = ClonePlan(plan);
        return await SaveAsync(batch, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> ReplaceImportedPlanAtAsync(string batchId, int index, string path, CancellationToken cancellationToken = default) {
        var plan = await _exchangeService.LoadAsync(path, cancellationToken).ConfigureAwait(false);
        return await ReplacePlanAtAsync(batchId, index, plan, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> RemovePlanAtAsync(string batchId, int index, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(batchId)) {
            throw new ArgumentException("Batch id is required.", nameof(batchId));
        }

        var batch = await _store.GetByIdAsync(batchId.Trim(), cancellationToken).ConfigureAwait(false);
        if (batch == null) {
            return OperationResult.Failure("action_plan_batch_not_found", $"Action plan batch '{batchId.Trim()}' was not found.");
        }
        if (index < 0 || index >= batch.Plans.Count) {
            return OperationResult.Failure("action_plan_batch_index_invalid", $"Action plan batch '{batch.Id}' does not contain a plan at index {index}.");
        }
        if (batch.Plans.Count == 1) {
            return OperationResult.Failure("action_plan_batch_invalid", "Cannot remove the last plan from a batch. Delete the batch instead.");
        }

        batch.Plans.RemoveAt(index);
        return await SaveAsync(batch, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> DeleteAsync(string batchId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(batchId)) {
            throw new ArgumentException("Batch id is required.", nameof(batchId));
        }

        var normalizedId = batchId.Trim();
        var removed = await _store.RemoveAsync(normalizedId, cancellationToken).ConfigureAwait(false);
        return removed
            ? OperationResult.Success($"Action plan batch '{normalizedId}' deleted.")
            : OperationResult.Failure("action_plan_batch_not_found", $"Action plan batch '{normalizedId}' was not found.");
    }

    /// <inheritdoc />
    public async Task<OperationResult> ImportAsync(string batchId, string name, string path, string? description = null, CancellationToken cancellationToken = default) {
        var plans = await _exchangeService.LoadBatchAsync(path, cancellationToken).ConfigureAwait(false);
        return await SaveAsync(new MailMessageActionPlanBatch {
            Id = batchId,
            Name = name,
            Description = description,
            Plans = plans.ToList()
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> ExportAsync(string batchId, string path, CancellationToken cancellationToken = default) {
        var batch = await _store.GetByIdAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch == null) {
            return OperationResult.Failure("action_plan_batch_not_found", $"Action plan batch '{batchId}' was not found.");
        }

        await _exchangeService.SaveBatchAsync(path, batch.Plans, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success($"Action plan batch '{batch.Id}' exported.");
    }

    /// <inheritdoc />
    public async Task<MessageActionBatchExecutionResult> ExecuteAsync(string batchId, bool continueOnError = true, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(batchId)) {
            throw new ArgumentException("Batch id is required.", nameof(batchId));
        }

        var batch = await _store.GetByIdAsync(batchId.Trim(), cancellationToken).ConfigureAwait(false);
        if (batch == null) {
            return new MessageActionBatchExecutionResult {
                Succeeded = false,
                Code = "action_plan_batch_not_found",
                Message = $"Action plan batch '{batchId.Trim()}' was not found."
            };
        }

        return await _batchService.ExecuteAsync(batch.Plans, continueOnError, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OperationResult> ValidateAsync(MailMessageActionPlanBatch? batch, CancellationToken cancellationToken) {
        if (batch == null) {
            return OperationResult.Failure("action_plan_batch_invalid", "Action plan batch is required.");
        }
        if (string.IsNullOrWhiteSpace(batch.Id)) {
            return OperationResult.Failure("action_plan_batch_invalid", "Action plan batch id is required.");
        }
        if (string.IsNullOrWhiteSpace(batch.Name)) {
            return OperationResult.Failure("action_plan_batch_invalid", "Action plan batch name is required.");
        }
        if (batch.Plans == null || batch.Plans.Count == 0) {
            return OperationResult.Failure("action_plan_batch_invalid", "Action plan batch must contain at least one plan.");
        }

        foreach (var plan in batch.Plans) {
            if (plan == null) {
                return OperationResult.Failure("action_plan_batch_invalid", "Action plan batch cannot contain null plans.");
            }
            if (string.IsNullOrWhiteSpace(plan.ProfileId)) {
                return OperationResult.Failure("action_plan_batch_invalid", "Each stored plan must include a profile id.");
            }

            var profile = await _profileStore.GetByIdAsync(plan.ProfileId, cancellationToken).ConfigureAwait(false);
            if (profile == null) {
                return OperationResult.Failure("action_plan_profile_not_found", $"Profile '{plan.ProfileId}' was not found.");
            }
        }

        return OperationResult.Success();
    }

    private static MessageActionExecutionPlan ClonePlan(MessageActionExecutionPlan plan) => new() {
        Succeeded = plan.Succeeded,
        Code = plan.Code,
        Message = plan.Message,
        Name = plan.Name,
        Summary = plan.Summary,
        Action = plan.Action,
        ExecutionKind = plan.ExecutionKind,
        ProfileId = plan.ProfileId,
        MailboxId = plan.MailboxId,
        FolderId = plan.FolderId,
        RequestedCount = plan.RequestedCount,
        UniqueMessageCount = plan.UniqueMessageCount,
        MessageIds = plan.MessageIds.ToList(),
        RequestedDestinationFolderId = plan.RequestedDestinationFolderId,
        Destination = plan.Destination == null
            ? null
            : new MailFolderTargetResolution {
                ProfileId = plan.Destination.ProfileId,
                MailboxId = plan.Destination.MailboxId,
                RequestedValue = plan.Destination.RequestedValue,
                IsAlias = plan.Destination.IsAlias,
                Alias = plan.Destination.Alias,
                IsSupported = plan.Destination.IsSupported,
                IsResolved = plan.Destination.IsResolved,
                EffectiveFolderId = plan.Destination.EffectiveFolderId,
                FolderDisplayName = plan.Destination.FolderDisplayName,
                FolderPath = plan.Destination.FolderPath,
                Summary = plan.Destination.Summary
            },
        DesiredState = plan.DesiredState,
        ConfirmationToken = plan.ConfirmationToken,
        ConfirmationProvided = plan.ConfirmationProvided,
        ConfirmationValidated = plan.ConfirmationValidated,
        Warnings = plan.Warnings.ToList()
    };

    private static MessageActionExecutionPlan TransformPlan(MessageActionExecutionPlan plan, MessageActionPlanBatchTransformRequest transform) {
        var cloned = ClonePlan(plan);
        var transformedProfileId = string.IsNullOrWhiteSpace(transform.ProfileId) ? cloned.ProfileId : transform.ProfileId!.Trim();
        var transformedMailboxId = string.IsNullOrWhiteSpace(transform.MailboxId) ? cloned.MailboxId : transform.MailboxId!.Trim();
        var transformedFolderId = string.IsNullOrWhiteSpace(transform.FolderId) ? cloned.FolderId : transform.FolderId!.Trim();

        cloned.ProfileId = transformedProfileId;
        cloned.MailboxId = transformedMailboxId;
        cloned.FolderId = transformedFolderId;

        if (cloned.Destination != null) {
            cloned.Destination.ProfileId = transformedProfileId;
            cloned.Destination.MailboxId = transformedMailboxId;
        }

        if (!string.IsNullOrWhiteSpace(transform.DestinationFolderId) && string.Equals(cloned.ExecutionKind, "Move", StringComparison.OrdinalIgnoreCase)) {
            var transformedDestination = transform.DestinationFolderId!.Trim();
            cloned.RequestedDestinationFolderId = transformedDestination;
            cloned.Destination = null;
        }

        cloned.ConfirmationProvided = false;
        cloned.ConfirmationValidated = false;
        cloned.ConfirmationToken = CreateConfirmationToken(cloned);
        cloned.Summary = BuildStoredPlanSummary(cloned);
        return cloned;
    }

    private static IReadOnlyList<MessageActionPreviewItem> SelectCommonActions(CommonMessageActionsPreview preview, IReadOnlyList<string>? actions) {
        if (actions == null || actions.Count == 0) {
            return preview.Actions
                .Where(action => action.Succeeded)
                .ToArray();
        }

        var selected = new List<MessageActionPreviewItem>();
        foreach (var actionName in actions
                     .Where(action => !string.IsNullOrWhiteSpace(action))
                     .Select(action => action.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase)) {
            var match = preview.Actions.FirstOrDefault(action =>
                string.Equals(action.Action, actionName, StringComparison.OrdinalIgnoreCase) &&
                action.Succeeded);
            if (match != null) {
                selected.Add(match);
            }
        }

        return selected;
    }

    private static string? ResolveDestinationFolderId(MessageActionPreviewItem action, string? fallbackDestinationFolderId) =>
        string.Equals(action.Action, "move", StringComparison.OrdinalIgnoreCase)
            ? action.Destination?.EffectiveFolderId ?? action.RequestedDestinationFolderId ?? fallbackDestinationFolderId
            : null;

    private static string? CreateConfirmationToken(MessageActionExecutionPlan plan) =>
        plan.ExecutionKind switch {
            "Move" => !string.IsNullOrWhiteSpace(plan.Destination?.EffectiveFolderId ?? plan.RequestedDestinationFolderId)
                ? MessageActionConfirmationTokens.CreateMoveToken(
                    plan.ProfileId,
                    plan.MailboxId,
                    plan.FolderId,
                    plan.MessageIds,
                    plan.Destination?.EffectiveFolderId ?? plan.RequestedDestinationFolderId!)
                : null,
            "Delete" => MessageActionConfirmationTokens.CreateDeleteToken(
                plan.ProfileId,
                plan.MailboxId,
                plan.FolderId,
                plan.MessageIds),
            "SetReadState" when plan.DesiredState.HasValue => MessageActionConfirmationTokens.CreateReadStateToken(
                plan.ProfileId,
                plan.MailboxId,
                plan.FolderId,
                plan.MessageIds,
                plan.DesiredState.Value),
            "SetFlaggedState" when plan.DesiredState.HasValue => MessageActionConfirmationTokens.CreateFlaggedStateToken(
                plan.ProfileId,
                plan.MailboxId,
                plan.FolderId,
                plan.MessageIds,
                plan.DesiredState.Value),
            _ => plan.ConfirmationToken
        };

    private static string BuildTransformSummary(MessageActionExecutionPlan sourcePlan, MessageActionExecutionPlan transformedPlan, bool willChange, bool tokenChanged) {
        var changes = new List<string>();
        if (!string.Equals(sourcePlan.ProfileId, transformedPlan.ProfileId, StringComparison.Ordinal)) {
            changes.Add($"profile {sourcePlan.ProfileId}->{transformedPlan.ProfileId}");
        }
        if (!string.Equals(sourcePlan.MailboxId, transformedPlan.MailboxId, StringComparison.Ordinal)) {
            changes.Add($"mailbox {(sourcePlan.MailboxId ?? "<none>")}->{(transformedPlan.MailboxId ?? "<none>")}");
        }
        if (!string.Equals(sourcePlan.FolderId, transformedPlan.FolderId, StringComparison.Ordinal)) {
            changes.Add($"folder {(sourcePlan.FolderId ?? "<none>")}->{(transformedPlan.FolderId ?? "<none>")}");
        }
        if (!string.Equals(sourcePlan.RequestedDestinationFolderId, transformedPlan.RequestedDestinationFolderId, StringComparison.Ordinal)) {
            changes.Add($"destination {(sourcePlan.RequestedDestinationFolderId ?? "<none>")}->{(transformedPlan.RequestedDestinationFolderId ?? "<none>")}");
        }
        if (tokenChanged) {
            changes.Add("confirmation token regenerated");
        }

        return willChange || tokenChanged
            ? $"{sourcePlan.Action}: {string.Join(", ", changes)}"
            : $"{sourcePlan.Action}: unchanged";
    }

    private static IReadOnlyList<SelectedPlan> SelectPlans(
        MailMessageActionPlanBatch source,
        MessageActionPlanBatchTransformRequest transform,
        out OperationResult? error) {
        error = null;

        var selected = new Dictionary<int, SelectedPlan>();
        var hasIndexes = transform.PlanIndexes != null && transform.PlanIndexes.Count > 0;
        var hasNames = transform.PlanNames != null && transform.PlanNames.Count > 0;
        IEnumerable<int> requestedIndexes = hasIndexes ? transform.PlanIndexes! : Array.Empty<int>();
        IEnumerable<string> requestedNames = hasNames ? transform.PlanNames! : Array.Empty<string>();

        if (!hasIndexes && !hasNames) {
            return source.Plans
                .Select((plan, index) => new SelectedPlan(index, plan))
                .ToArray();
        }

        if (hasIndexes) {
            foreach (var index in requestedIndexes.Distinct()) {
                if (index < 0 || index >= source.Plans.Count) {
                    error = OperationResult.Failure("action_plan_batch_index_invalid", $"Action plan batch '{source.Id}' does not contain a plan at index {index}.");
                    return Array.Empty<SelectedPlan>();
                }

                selected[index] = new SelectedPlan(index, source.Plans[index]);
            }
        }

        if (hasNames) {
            foreach (var requestedName in requestedNames
                         .Where(name => !string.IsNullOrWhiteSpace(name))
                         .Select(name => name.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase)) {
                var matches = source.Plans
                    .Select((plan, index) => new SelectedPlan(index, plan))
                    .Where(selection => PlanMatchesName(selection.Plan, requestedName))
                    .ToArray();
                if (matches.Length == 0) {
                    error = OperationResult.Failure("action_plan_batch_name_invalid", $"Action plan batch '{source.Id}' does not contain a plan named '{requestedName}'.");
                    return Array.Empty<SelectedPlan>();
                }

                foreach (var match in matches) {
                    selected[match.Index] = match;
                }
            }
        }

        if (selected.Count == 0) {
            error = OperationResult.Failure("action_plan_batch_invalid", "Action plan batch transform must include at least one plan.");
            return Array.Empty<SelectedPlan>();
        }

        return selected
            .OrderBy(item => item.Key)
            .Select(item => item.Value)
            .ToArray();
    }

    private static MailMessageActionPlanBatchCompact ToCompact(MailMessageActionPlanBatch batch) {
        var profileCount = batch.Plans
            .Select(plan => plan.ProfileId)
            .Where(profileId => !string.IsNullOrWhiteSpace(profileId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var readyPlanCount = batch.Plans.Count(plan => plan.Succeeded);

        return new MailMessageActionPlanBatchCompact {
            Id = batch.Id,
            Name = batch.Name,
            PlanCount = batch.Plans.Count,
            ReadyPlanCount = readyPlanCount,
            ProfileCount = profileCount,
            PlanNames = batch.Plans
                .Select(plan => !string.IsNullOrWhiteSpace(plan.Name) ? plan.Name : BuildStoredPlanSummary(plan))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            UpdatedAt = batch.UpdatedAt,
            Summary = $"{batch.Id} ({batch.Plans.Count} plan(s), {readyPlanCount} ready)"
        };
    }

    private static MailMessageActionPlanBatchSummary ToSummary(MailMessageActionPlanBatch batch) {
        var profileIds = batch.Plans
            .Select(plan => plan.ProfileId)
            .Where(profileId => !string.IsNullOrWhiteSpace(profileId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(profileId => profileId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var readyPlanCount = batch.Plans.Count(plan => plan.Succeeded);
        var actionCounts = batch.Plans
            .Where(plan => !string.IsNullOrWhiteSpace(plan.Action))
            .GroupBy(plan => plan.Action, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return new MailMessageActionPlanBatchSummary {
            Id = batch.Id,
            Name = batch.Name,
            PlanCount = batch.Plans.Count,
            ReadyPlanCount = readyPlanCount,
            ProfileIds = profileIds,
            ActionCounts = actionCounts,
            PlanNames = batch.Plans
                .Select(plan => !string.IsNullOrWhiteSpace(plan.Name) ? plan.Name : BuildStoredPlanSummary(plan))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            UpdatedAt = batch.UpdatedAt,
            Summary = $"{batch.Id} ({batch.Plans.Count} plan(s), {readyPlanCount} ready, {actionCounts.Count} action type(s))"
        };
    }

    private static IReadOnlyList<MailMessageActionPlanBatch> ApplyBatchQuery(
        IReadOnlyList<MailMessageActionPlanBatch> batches,
        MailMessageActionPlanBatchQuery? query) {
        if (query == null) {
            return batches;
        }

        var requestedPlanNames = query.PlanNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var requestedProfileIds = query.ProfileIds
            .Where(profileId => !string.IsNullOrWhiteSpace(profileId))
            .Select(profileId => profileId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var requestedActions = query.Actions
            .Where(action => !string.IsNullOrWhiteSpace(action))
            .Select(action => action.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        IEnumerable<MailMessageActionPlanBatch> filtered = batches;
        if (requestedPlanNames.Length > 0 || requestedProfileIds.Length > 0 || requestedActions.Length > 0) {
            filtered = filtered.Where(batch =>
                (requestedPlanNames.Length == 0 || batch.Plans.Any(plan => requestedPlanNames.Any(requestedName => PlanMatchesName(plan, requestedName)))) &&
                (requestedProfileIds.Length == 0 || batch.Plans.Any(plan => requestedProfileIds.Any(requestedProfileId => string.Equals(plan.ProfileId, requestedProfileId, StringComparison.OrdinalIgnoreCase)))) &&
                (requestedActions.Length == 0 || batch.Plans.Any(plan => requestedActions.Any(requestedAction => string.Equals(plan.Action, requestedAction, StringComparison.OrdinalIgnoreCase)))));
        }

        var ordered = ApplyBatchQuerySort(filtered, query.SortBy, query.Descending);
        return ordered.ToArray();
    }

    private static IEnumerable<MailMessageActionPlanBatch> ApplyBatchQuerySort(
        IEnumerable<MailMessageActionPlanBatch> batches,
        MailMessageActionPlanBatchSortBy sortBy,
        bool descending) {
        Func<MailMessageActionPlanBatch, object> keySelector = sortBy switch {
            MailMessageActionPlanBatchSortBy.Name => batch => batch.Name,
            MailMessageActionPlanBatchSortBy.PlanCount => batch => batch.Plans.Count,
            MailMessageActionPlanBatchSortBy.ReadyPlanCount => batch => batch.Plans.Count(plan => plan.Succeeded),
            MailMessageActionPlanBatchSortBy.UpdatedAt => batch => batch.UpdatedAt,
            MailMessageActionPlanBatchSortBy.ActionTypeCount => batch => batch.Plans
                .Select(plan => plan.Action)
                .Where(action => !string.IsNullOrWhiteSpace(action))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            _ => batch => batch.Id
        };

        var ordered = descending
            ? batches.OrderByDescending(keySelector)
            : batches.OrderBy(keySelector);

        return ordered.ThenBy(batch => batch.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static bool PlanMatchesName(MessageActionExecutionPlan plan, string requestedName) =>
        (!string.IsNullOrWhiteSpace(plan.Name) && string.Equals(plan.Name, requestedName, StringComparison.OrdinalIgnoreCase)) ||
        (!string.IsNullOrWhiteSpace(plan.Summary) && string.Equals(plan.Summary, requestedName, StringComparison.OrdinalIgnoreCase));

    private static string BuildStoredPlanSummary(MessageActionExecutionPlan plan) {
        var messageCount = plan.UniqueMessageCount == 1 ? "1 message" : $"{plan.UniqueMessageCount} messages";
        return !string.IsNullOrWhiteSpace(plan.Name)
            ? $"{plan.Name} ({messageCount})"
            : $"{plan.Action} ({messageCount})";
    }

    private sealed class SelectedPlan {
        public SelectedPlan(int index, MessageActionExecutionPlan plan) {
            Index = index;
            Plan = plan;
        }

        public int Index { get; }

        public MessageActionExecutionPlan Plan { get; }
    }
}
