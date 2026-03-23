namespace Mailozaurr.Application;

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
        CancellationToken cancellationToken = default) {
        if (plans == null) {
            throw new ArgumentNullException(nameof(plans));
        }

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

            var execution = await _planService.ExecuteAsync(plan, cancellationToken).ConfigureAwait(false);
            result.AttemptedPlanCount++;
            if (execution.Succeeded) {
                result.SucceededPlanCount++;
            } else {
                result.FailedPlanCount++;
            }

            result.Results.Add(new MessageActionBatchExecutionItemResult {
                Index = index,
                Action = plan.Action,
                ExecutionKind = plan.ExecutionKind,
                ProfileId = plan.ProfileId,
                RequestedCount = plan.RequestedCount,
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
