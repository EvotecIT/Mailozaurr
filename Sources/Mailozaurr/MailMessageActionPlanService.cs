namespace Mailozaurr;

/// <summary>
/// Creates normalized execution plans from previewable message actions and can execute those plans.
/// </summary>
public sealed class MailMessageActionPlanService : IMailMessageActionPlanService {
    private readonly IMailMessageActionPreviewService _previewService;
    private readonly IMailMessageActionService _messageActionService;

    /// <summary>
    /// Creates a new message action planning service.
    /// </summary>
    public MailMessageActionPlanService(IMailMessageActionPreviewService previewService, IMailMessageActionService messageActionService) {
        _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
        _messageActionService = messageActionService ?? throw new ArgumentNullException(nameof(messageActionService));
    }

    /// <inheritdoc />
    public async Task<MessageActionExecutionPlan> CreatePlanAsync(MessageActionExecutionPlanRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }
        if (string.IsNullOrWhiteSpace(request.Action)) {
            throw new ArgumentException("Action is required.", nameof(request));
        }

        var action = request.Action.Trim().ToLowerInvariant();
        return action switch {
            "mark-read" => CreateStatePlan(
                action,
                "SetReadState",
                request,
                await _previewService.PreviewReadStateAsync(new SetReadStateRequest {
                    ProfileId = request.ProfileId,
                    MailboxId = request.MailboxId,
                    FolderId = request.FolderId,
                    MessageIds = request.MessageIds,
                    IsRead = true
                }, cancellationToken).ConfigureAwait(false)),
            "mark-unread" => CreateStatePlan(
                action,
                "SetReadState",
                request,
                await _previewService.PreviewReadStateAsync(new SetReadStateRequest {
                    ProfileId = request.ProfileId,
                    MailboxId = request.MailboxId,
                    FolderId = request.FolderId,
                    MessageIds = request.MessageIds,
                    IsRead = false
                }, cancellationToken).ConfigureAwait(false)),
            "flag" => CreateStatePlan(
                action,
                "SetFlaggedState",
                request,
                await _previewService.PreviewFlaggedStateAsync(new SetFlaggedStateRequest {
                    ProfileId = request.ProfileId,
                    MailboxId = request.MailboxId,
                    FolderId = request.FolderId,
                    MessageIds = request.MessageIds,
                    IsFlagged = true
                }, cancellationToken).ConfigureAwait(false)),
            "unflag" => CreateStatePlan(
                action,
                "SetFlaggedState",
                request,
                await _previewService.PreviewFlaggedStateAsync(new SetFlaggedStateRequest {
                    ProfileId = request.ProfileId,
                    MailboxId = request.MailboxId,
                    FolderId = request.FolderId,
                    MessageIds = request.MessageIds,
                    IsFlagged = false
                }, cancellationToken).ConfigureAwait(false)),
            "archive" => CreateMovePlan(
                action,
                request,
                await _previewService.PreviewMoveAsync(new MoveMessagesPreviewRequest {
                    ProfileId = request.ProfileId,
                    MailboxId = request.MailboxId,
                    FolderId = request.FolderId,
                    MessageIds = request.MessageIds,
                    DestinationFolderId = MailFolderAliases.Archive
                }, cancellationToken).ConfigureAwait(false)),
            "trash" => CreateMovePlan(
                action,
                request,
                await _previewService.PreviewMoveAsync(new MoveMessagesPreviewRequest {
                    ProfileId = request.ProfileId,
                    MailboxId = request.MailboxId,
                    FolderId = request.FolderId,
                    MessageIds = request.MessageIds,
                    DestinationFolderId = MailFolderAliases.Trash
                }, cancellationToken).ConfigureAwait(false)),
            "move" => CreateMovePlan(
                action,
                request,
                await _previewService.PreviewMoveAsync(new MoveMessagesPreviewRequest {
                    ProfileId = request.ProfileId,
                    MailboxId = request.MailboxId,
                    FolderId = request.FolderId,
                    MessageIds = request.MessageIds,
                    DestinationFolderId = request.DestinationFolderId ?? string.Empty
                }, cancellationToken).ConfigureAwait(false)),
            "delete" => CreateDeletePlan(
                action,
                request,
                await _previewService.PreviewDeleteAsync(new DeleteMessagesPreviewRequest {
                    ProfileId = request.ProfileId,
                    MailboxId = request.MailboxId,
                    FolderId = request.FolderId,
                    MessageIds = request.MessageIds
                }, cancellationToken).ConfigureAwait(false)),
            _ => throw new InvalidOperationException($"Unsupported action '{request.Action}'.")
        };
    }

    /// <inheritdoc />
    public Task<MessageActionResult> ExecuteAsync(MessageActionExecutionPlan plan, CancellationToken cancellationToken = default) {
        if (plan == null) {
            throw new ArgumentNullException(nameof(plan));
        }
        if (!plan.Succeeded) {
            return Task.FromResult(new MessageActionResult {
                Succeeded = false,
                Code = plan.Code ?? "plan_not_ready",
                Message = plan.Message ?? "The action plan is not ready for execution.",
                ProfileId = plan.ProfileId,
                RequestedCount = plan.RequestedCount,
                FailedCount = plan.UniqueMessageCount
            });
        }
        if (!plan.ConfirmationProvided || !plan.ConfirmationValidated) {
            return Task.FromResult(new MessageActionResult {
                Succeeded = false,
                Code = "confirmation_token_required",
                Message = "A validated confirmation token is required before executing this action plan.",
                ProfileId = plan.ProfileId,
                RequestedCount = plan.RequestedCount,
                FailedCount = plan.UniqueMessageCount
            });
        }

        return plan.ExecutionKind switch {
            "SetReadState" => _messageActionService.SetReadStateAsync(new SetReadStateRequest {
                ProfileId = plan.ProfileId,
                MailboxId = plan.MailboxId,
                FolderId = plan.FolderId,
                MessageIds = plan.MessageIds.ToList(),
                IsRead = plan.DesiredState == true,
                ConfirmationToken = plan.ConfirmationToken
            }, cancellationToken),
            "SetFlaggedState" => _messageActionService.SetFlaggedStateAsync(new SetFlaggedStateRequest {
                ProfileId = plan.ProfileId,
                MailboxId = plan.MailboxId,
                FolderId = plan.FolderId,
                MessageIds = plan.MessageIds.ToList(),
                IsFlagged = plan.DesiredState == true,
                ConfirmationToken = plan.ConfirmationToken
            }, cancellationToken),
            "Move" => _messageActionService.MoveAsync(new MoveMessagesRequest {
                ProfileId = plan.ProfileId,
                MailboxId = plan.MailboxId,
                FolderId = plan.FolderId,
                MessageIds = plan.MessageIds.ToList(),
                DestinationFolderId = plan.Destination?.EffectiveFolderId ?? plan.RequestedDestinationFolderId ?? string.Empty,
                ConfirmationToken = plan.ConfirmationToken
            }, cancellationToken),
            "Delete" => _messageActionService.DeleteAsync(new DeleteMessagesRequest {
                ProfileId = plan.ProfileId,
                MailboxId = plan.MailboxId,
                FolderId = plan.FolderId,
                MessageIds = plan.MessageIds.ToList(),
                ConfirmationToken = plan.ConfirmationToken
            }, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported execution kind '{plan.ExecutionKind}'.")
        };
    }

    private static MessageActionExecutionPlan CreateStatePlan(
        string action,
        string executionKind,
        MessageActionExecutionPlanRequest request,
        MessageStateChangePreview preview) {
        var plan = CreateBasePlan(action, executionKind, request, preview.ProfileId, preview.RequestedCount, preview.UniqueMessageCount, preview.MessageIds);
        plan.DesiredState = preview.DesiredState;
        plan.ConfirmationToken = preview.ConfirmationToken;
        plan.Warnings.AddRange(preview.Warnings);
        return FinalizePlan(plan, request.ConfirmationToken, preview.Succeeded, preview.Code, preview.Message);
    }

    private static MessageActionExecutionPlan CreateMovePlan(
        string action,
        MessageActionExecutionPlanRequest request,
        MoveMessagesPreview preview) {
        var plan = CreateBasePlan(action, "Move", request, preview.ProfileId, preview.RequestedCount, preview.UniqueMessageCount, preview.MessageIds);
        plan.RequestedDestinationFolderId = preview.RequestedDestinationFolderId;
        plan.Destination = preview.Destination;
        plan.ConfirmationToken = preview.ConfirmationToken;
        plan.Warnings.AddRange(preview.Warnings);
        return FinalizePlan(plan, request.ConfirmationToken, preview.Succeeded, preview.Code, preview.Message);
    }

    private static MessageActionExecutionPlan CreateDeletePlan(
        string action,
        MessageActionExecutionPlanRequest request,
        DeleteMessagesPreview preview) {
        var plan = CreateBasePlan(action, "Delete", request, preview.ProfileId, preview.RequestedCount, preview.UniqueMessageCount, preview.MessageIds);
        plan.ConfirmationToken = preview.ConfirmationToken;
        plan.Warnings.AddRange(preview.Warnings);
        return FinalizePlan(plan, request.ConfirmationToken, preview.Succeeded, preview.Code, preview.Message);
    }

    private static MessageActionExecutionPlan CreateBasePlan(
        string action,
        string executionKind,
        MessageActionExecutionPlanRequest request,
        string profileId,
        int requestedCount,
        int uniqueMessageCount,
        List<string> messageIds) =>
        new() {
            Action = action,
            ExecutionKind = executionKind,
            ProfileId = profileId,
            MailboxId = request.MailboxId,
            FolderId = request.FolderId,
            RequestedCount = requestedCount,
            UniqueMessageCount = uniqueMessageCount,
            MessageIds = messageIds
        };

    private static MessageActionExecutionPlan FinalizePlan(
        MessageActionExecutionPlan plan,
        string? providedConfirmationToken,
        bool previewSucceeded,
        string? previewCode,
        string? previewMessage) {
        plan.Name = BuildPlanName(plan);
        plan.ConfirmationProvided = !string.IsNullOrWhiteSpace(providedConfirmationToken);

        if (!previewSucceeded) {
            plan.Succeeded = false;
            plan.Code = previewCode;
            plan.Message = previewMessage;
            plan.ConfirmationValidated = false;
            plan.Summary = BuildPlanSummary(plan);
            return plan;
        }

        if (plan.ConfirmationProvided) {
            var providedToken = providedConfirmationToken!;
            providedToken = providedToken.Trim();
            if (!string.Equals(providedToken, plan.ConfirmationToken, StringComparison.Ordinal)) {
                plan.Succeeded = false;
                plan.Code = "confirmation_token_mismatch";
                plan.Message = "The supplied confirmation token does not match this normalized action plan.";
                plan.ConfirmationValidated = false;
                plan.Summary = BuildPlanSummary(plan);
                return plan;
            }
        }

        plan.Succeeded = true;
        plan.Code = null;
        plan.ConfirmationValidated = plan.ConfirmationProvided && string.Equals(providedConfirmationToken?.Trim(), plan.ConfirmationToken, StringComparison.Ordinal);
        plan.Message = $"Execution plan ready for '{plan.Action}' on {plan.UniqueMessageCount} message(s).";
        plan.Summary = BuildPlanSummary(plan);
        return plan;
    }

    private static string BuildPlanName(MessageActionExecutionPlan plan) =>
        plan.Action switch {
            "mark-read" => "Mark as read",
            "mark-unread" => "Mark as unread",
            "flag" => "Flag",
            "unflag" => "Unflag",
            "archive" => "Archive",
            "trash" => "Trash",
            "move" => !string.IsNullOrWhiteSpace(plan.RequestedDestinationFolderId)
                ? $"Move to {plan.RequestedDestinationFolderId}"
                : "Move",
            "delete" => "Delete",
            _ => plan.Action
        };

    private static string BuildPlanSummary(MessageActionExecutionPlan plan) {
        var messageCount = plan.UniqueMessageCount == 1 ? "1 message" : $"{plan.UniqueMessageCount} messages";
        return plan.Action switch {
            "move" when !string.IsNullOrWhiteSpace(plan.RequestedDestinationFolderId) => $"{BuildPlanName(plan)} ({messageCount})",
            _ => $"{BuildPlanName(plan)} ({messageCount})"
        };
    }
}
