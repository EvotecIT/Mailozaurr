using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationMessageActionBatchServiceTests {
    [Fact]
    public async Task ExecuteAggregatesSuccessfulPlans() {
        var planService = new FakePlanService(plan => Task.FromResult(new MessageActionResult {
            Succeeded = true,
            ProfileId = plan.ProfileId,
            RequestedCount = plan.UniqueMessageCount,
            SucceededCount = plan.UniqueMessageCount,
            FailedCount = 0,
            Message = $"Executed '{plan.Action}'."
        }));
        var batchService = new MailMessageActionBatchService(planService);

        var result = await batchService.ExecuteAsync(new[] {
            CreatePlan("mark-read", "SetReadState", "work-imap", "msg-1"),
            CreatePlan("delete", "Delete", "work-imap", "msg-2")
        });

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.RequestedPlanCount);
        Assert.Equal(2, result.AttemptedPlanCount);
        Assert.Equal(2, result.SucceededPlanCount);
        Assert.Equal(0, result.FailedPlanCount);
        Assert.Equal(0, result.SkippedPlanCount);
        Assert.Equal(2, planService.ExecutedPlans.Count);
    }

    [Fact]
    public async Task ExecuteStopsAfterFailureWhenContinueOnErrorIsFalse() {
        var planService = new FakePlanService(plan => Task.FromResult(plan.Action == "delete"
            ? new MessageActionResult {
                Succeeded = false,
                Code = "delete_failed",
                Message = "Delete failed.",
                ProfileId = plan.ProfileId,
                RequestedCount = plan.UniqueMessageCount,
                FailedCount = plan.UniqueMessageCount
            }
            : new MessageActionResult {
                Succeeded = true,
                ProfileId = plan.ProfileId,
                RequestedCount = plan.UniqueMessageCount,
                SucceededCount = plan.UniqueMessageCount,
                Message = $"Executed '{plan.Action}'."
            }));
        var batchService = new MailMessageActionBatchService(planService);

        var result = await batchService.ExecuteAsync(new[] {
            CreatePlan("mark-read", "SetReadState", "work-imap", "msg-1"),
            CreatePlan("delete", "Delete", "work-imap", "msg-2"),
            CreatePlan("move", "Move", "work-imap", "msg-3")
        }, continueOnError: false);

        Assert.False(result.Succeeded);
        Assert.Equal(3, result.RequestedPlanCount);
        Assert.Equal(2, result.AttemptedPlanCount);
        Assert.Equal(1, result.SucceededPlanCount);
        Assert.Equal(1, result.FailedPlanCount);
        Assert.Equal(1, result.SkippedPlanCount);
        Assert.Equal(2, planService.ExecutedPlans.Count);
        Assert.Contains(result.Results, item => item.Code == "skipped_after_failure" && item.Index == 2);
    }

    private static MessageActionExecutionPlan CreatePlan(string action, string executionKind, string profileId, string messageId) =>
        new() {
            Succeeded = true,
            Action = action,
            ExecutionKind = executionKind,
            ProfileId = profileId,
            RequestedCount = 1,
            UniqueMessageCount = 1,
            MessageIds = { messageId }
        };

    private sealed class FakePlanService : IMailMessageActionPlanService {
        private readonly Func<MessageActionExecutionPlan, Task<MessageActionResult>> _executor;

        public FakePlanService(Func<MessageActionExecutionPlan, Task<MessageActionResult>> executor) {
            _executor = executor;
        }

        public List<MessageActionExecutionPlan> ExecutedPlans { get; } = new();

        public Task<MessageActionExecutionPlan> CreatePlanAsync(MessageActionExecutionPlanRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<MessageActionResult> ExecuteAsync(MessageActionExecutionPlan plan, CancellationToken cancellationToken = default) {
            ExecutedPlans.Add(plan);
            return await _executor(plan).ConfigureAwait(false);
        }
    }
}
