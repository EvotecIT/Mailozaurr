using Mailozaurr.Hosting;

namespace Mailozaurr.Tests;

public sealed partial class ApplicationMessageActionPlanRegistryServiceTests {
    private static string CreateTemporaryFilePath(string fileName) {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    private sealed class PassThroughPlanService : IMailMessageActionPlanService {
        public Task<MessageActionExecutionPlan> CreatePlanAsync(MessageActionExecutionPlanRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MessageActionResult> ExecuteAsync(MessageActionExecutionPlan plan, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MessageActionResult {
                Succeeded = plan.Succeeded,
                ProfileId = plan.ProfileId,
                RequestedCount = plan.UniqueMessageCount,
                SucceededCount = plan.Succeeded ? plan.UniqueMessageCount : 0,
                FailedCount = plan.Succeeded ? 0 : plan.UniqueMessageCount,
                Message = $"Executed '{plan.Action}'."
            });
    }

    private sealed class RecordingPlanService : IMailMessageActionPlanService {
        public List<MessageActionExecutionPlanRequest> Requests { get; } = new();

        public Task<MessageActionExecutionPlan> CreatePlanAsync(MessageActionExecutionPlanRequest request, CancellationToken cancellationToken = default) {
            Requests.Add(new MessageActionExecutionPlanRequest {
                Action = request.Action,
                ProfileId = request.ProfileId,
                MailboxId = request.MailboxId,
                FolderId = request.FolderId,
                MessageIds = request.MessageIds.ToList(),
                DestinationFolderId = request.DestinationFolderId,
                ConfirmationToken = request.ConfirmationToken
            });

            if (string.Equals(request.Action, "unsupported", StringComparison.OrdinalIgnoreCase)) {
                return Task.FromResult(new MessageActionExecutionPlan {
                    Succeeded = false,
                    Code = "action_not_supported",
                    Action = request.Action,
                    ProfileId = request.ProfileId,
                    MailboxId = request.MailboxId,
                    FolderId = request.FolderId,
                    RequestedCount = request.MessageIds.Count,
                    UniqueMessageCount = request.MessageIds.Distinct(StringComparer.Ordinal).Count(),
                    MessageIds = request.MessageIds.ToList(),
                    RequestedDestinationFolderId = request.DestinationFolderId
                });
            }

            return Task.FromResult(new MessageActionExecutionPlan {
                Succeeded = true,
                Action = request.Action,
                ExecutionKind = request.Action switch {
                    "mark-read" or "mark-unread" => "SetReadState",
                    "flag" or "unflag" => "SetFlaggedState",
                    "move" or "archive" or "trash" => "Move",
                    "delete" => "Delete",
                    _ => "Custom"
                },
                ProfileId = request.ProfileId,
                MailboxId = request.MailboxId,
                FolderId = request.FolderId,
                RequestedCount = request.MessageIds.Count,
                UniqueMessageCount = request.MessageIds.Distinct(StringComparer.Ordinal).Count(),
                MessageIds = request.MessageIds.ToList(),
                RequestedDestinationFolderId = request.DestinationFolderId,
                ConfirmationToken = string.IsNullOrWhiteSpace(request.ConfirmationToken)
                    ? $"generated-{request.Action}"
                    : request.ConfirmationToken,
                ConfirmationProvided = !string.IsNullOrWhiteSpace(request.ConfirmationToken),
                ConfirmationValidated = !string.IsNullOrWhiteSpace(request.ConfirmationToken),
                DesiredState = request.Action switch {
                    "mark-read" => true,
                    "mark-unread" => false,
                    "flag" => true,
                    "unflag" => false,
                    _ => null
                }
            });
        }

        public Task<MessageActionResult> ExecuteAsync(MessageActionExecutionPlan plan, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MessageActionResult {
                Succeeded = plan.Succeeded,
                ProfileId = plan.ProfileId,
                RequestedCount = plan.UniqueMessageCount,
                SucceededCount = plan.Succeeded ? plan.UniqueMessageCount : 0,
                FailedCount = plan.Succeeded ? 0 : plan.UniqueMessageCount,
                Message = $"Executed '{plan.Action}'."
            });
    }

    private sealed class FakePreviewService : IMailMessageActionPreviewService {
        public CommonMessageActionsPreviewRequest? LastCommonRequest { get; private set; }

        public CommonMessageActionsPreview? NextCommonPreview { get; set; }

        public Task<CommonMessageActionsPreview> PreviewCommonActionsAsync(CommonMessageActionsPreviewRequest request, CancellationToken cancellationToken = default) {
            LastCommonRequest = new CommonMessageActionsPreviewRequest {
                ProfileId = request.ProfileId,
                MailboxId = request.MailboxId,
                FolderId = request.FolderId,
                DestinationFolderId = request.DestinationFolderId,
                MessageIds = request.MessageIds.ToList()
            };
            return Task.FromResult(NextCommonPreview ?? new CommonMessageActionsPreview {
                Succeeded = false,
                ProfileId = request.ProfileId,
                MailboxId = request.MailboxId,
                FolderId = request.FolderId,
                RequestedDestinationFolderId = request.DestinationFolderId,
                RequestedCount = request.MessageIds.Count,
                UniqueMessageCount = request.MessageIds.Count,
                MessageIds = request.MessageIds.ToList(),
                Code = "no_supported_actions",
                Message = "No supported actions."
            });
        }

        public Task<MessageStateChangePreview> PreviewReadStateAsync(SetReadStateRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MessageStateChangePreview> PreviewFlaggedStateAsync(SetFlaggedStateRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MoveMessagesPreview> PreviewMoveAsync(MoveMessagesPreviewRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DeleteMessagesPreview> PreviewDeleteAsync(DeleteMessagesPreviewRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StandardMessageActionsPreview> PreviewStandardActionsAsync(StandardMessageActionsPreviewRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
