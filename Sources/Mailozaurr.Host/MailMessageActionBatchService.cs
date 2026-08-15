namespace Mailozaurr.Hosting;

/// <summary>
/// Executes batches of normalized message action plans through the shared planning service.
/// </summary>
public sealed class MailMessageActionBatchService : IMailMessageActionBatchService {
    private readonly IMailMessageActionPlanService _planService;

    /// <summary>
    /// Creates a new message action batch execution service.
    /// </summary>
    public MailMessageActionBatchService(IMailMessageActionPlanService planService) {
        _planService = planService ?? throw new ArgumentNullException(nameof(planService));
    }

    /// <inheritdoc />
    public async Task<MessageActionBatchExecutionResult> ExecuteAsync(
        IReadOnlyList<MessageActionExecutionPlan> plans,
        bool continueOnError = true,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? confirmationTokens = null) {
        if (plans == null) {
            throw new ArgumentNullException(nameof(plans));
        }

        var normalizedConfirmationTokens = NormalizeConfirmationTokens(confirmationTokens);
        var result = new MessageActionBatchExecutionResult {
            RequestedPlanCount = plans.Count
        };

        for (var index = 0; index < plans.Count; index++) {
            var plan = plans[index];
            if (plan == null) {
                result.FailedPlanCount++;
                result.AttemptedPlanCount++;
                result.Results.Add(new MessageActionBatchExecutionItemResult {
                    Index = index,
                    Succeeded = false,
                    Code = "plan_required",
                    Message = "A plan entry was null.",
                    FailedCount = 1
                });
                if (!continueOnError) {
                    AddSkippedItems(result, plans, index + 1);
                    break;
                }

                continue;
            }

            var executablePlan = PreparePlanForExecution(plan, normalizedConfirmationTokens);
            var execution = await _planService.ExecuteAsync(executablePlan, cancellationToken).ConfigureAwait(false);
            result.AttemptedPlanCount++;
            if (execution.Succeeded) {
                result.SucceededPlanCount++;
            } else {
                result.FailedPlanCount++;
            }

            result.Results.Add(new MessageActionBatchExecutionItemResult {
                Index = index,
                Action = executablePlan.Action,
                ExecutionKind = executablePlan.ExecutionKind,
                ProfileId = executablePlan.ProfileId,
                RequestedCount = executablePlan.RequestedCount,
                Succeeded = execution.Succeeded,
                Code = execution.Code,
                Message = execution.Message,
                SucceededCount = execution.SucceededCount,
                FailedCount = execution.FailedCount
            });

            if (!execution.Succeeded && !continueOnError) {
                AddSkippedItems(result, plans, index + 1);
                break;
            }
        }

        result.Succeeded = result.FailedPlanCount == 0;
        result.Code = result.Succeeded ? null : "batch_execution_failed";
        result.Message = result.Succeeded
            ? $"Executed {result.AttemptedPlanCount} action plan(s) successfully."
            : $"Executed {result.AttemptedPlanCount} action plan(s): {result.SucceededPlanCount} succeeded, {result.FailedPlanCount} failed, {result.SkippedPlanCount} skipped.";
        return result;
    }

    private static HashSet<string> NormalizeConfirmationTokens(IReadOnlyList<string>? confirmationTokens) {
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        if (confirmationTokens == null || confirmationTokens.Count == 0) {
            return normalized;
        }

        foreach (var token in confirmationTokens) {
            if (!string.IsNullOrWhiteSpace(token)) {
                normalized.Add(token.Trim());
            }
        }

        return normalized;
    }

    private static MessageActionExecutionPlan PreparePlanForExecution(MessageActionExecutionPlan plan, HashSet<string> confirmationTokens) {
        var confirmationToken = plan.ConfirmationToken;
        if (confirmationTokens.Count == 0 ||
            string.IsNullOrWhiteSpace(confirmationToken) ||
            plan.ConfirmationValidated ||
            !confirmationTokens.Contains(confirmationToken!)) {
            return plan;
        }

        var executablePlan = ClonePlan(plan);
        executablePlan.ConfirmationProvided = true;
        executablePlan.ConfirmationValidated = true;
        return executablePlan;
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

    private static void AddSkippedItems(MessageActionBatchExecutionResult result, IReadOnlyList<MessageActionExecutionPlan> plans, int startIndex) {
        for (var index = startIndex; index < plans.Count; index++) {
            var plan = plans[index];
            result.SkippedPlanCount++;
            result.Results.Add(new MessageActionBatchExecutionItemResult {
                Index = index,
                Action = plan?.Action ?? string.Empty,
                ExecutionKind = plan?.ExecutionKind ?? string.Empty,
                ProfileId = plan?.ProfileId ?? string.Empty,
                RequestedCount = plan?.RequestedCount ?? 0,
                Succeeded = false,
                Code = "skipped_after_failure",
                Message = "Skipped because an earlier plan failed and continueOnError was false."
            });
        }
    }
}
