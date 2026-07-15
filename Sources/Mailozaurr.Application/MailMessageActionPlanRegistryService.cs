namespace Mailozaurr.Application;

/// <summary>
/// Implements reusable lifecycle operations for persisted message action plan batches.
/// </summary>
public sealed partial class MailMessageActionPlanRegistryService : IMailMessageActionPlanRegistryService {
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
    public async Task<MessageActionBatchExecutionResult> ExecuteAsync(
        string batchId,
        bool continueOnError = true,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? confirmationTokens = null) {
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

        return await _batchService.ExecuteAsync(batch.Plans, continueOnError, cancellationToken, confirmationTokens).ConfigureAwait(false);
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

}
