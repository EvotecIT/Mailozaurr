namespace Mailozaurr;

public sealed partial class MailMessageActionPlanRegistryService {
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

    private static MessageActionExecutionPlan TransformPlan(
        MessageActionExecutionPlan plan,
        MessageActionPlanBatchTransformRequest transform) {
        var cloned = ClonePlan(plan);
        var transformedProfileId = string.IsNullOrWhiteSpace(transform.ProfileId)
            ? cloned.ProfileId
            : transform.ProfileId!.Trim();
        var transformedMailboxId = string.IsNullOrWhiteSpace(transform.MailboxId)
            ? cloned.MailboxId
            : transform.MailboxId!.Trim();
        var transformedFolderId = string.IsNullOrWhiteSpace(transform.FolderId)
            ? cloned.FolderId
            : transform.FolderId!.Trim();

        cloned.ProfileId = transformedProfileId;
        cloned.MailboxId = transformedMailboxId;
        cloned.FolderId = transformedFolderId;

        if (cloned.Destination != null) {
            cloned.Destination.ProfileId = transformedProfileId;
            cloned.Destination.MailboxId = transformedMailboxId;
        }

        if (!string.IsNullOrWhiteSpace(transform.DestinationFolderId) &&
            string.Equals(cloned.ExecutionKind, "Move", StringComparison.OrdinalIgnoreCase)) {
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

    private static string BuildTransformSummary(
        MessageActionExecutionPlan sourcePlan,
        MessageActionExecutionPlan transformedPlan,
        bool willChange,
        bool tokenChanged) {
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
        if (!string.Equals(
                sourcePlan.RequestedDestinationFolderId,
                transformedPlan.RequestedDestinationFolderId,
                StringComparison.Ordinal)) {
            changes.Add($"destination {(sourcePlan.RequestedDestinationFolderId ?? "<none>")}->{(transformedPlan.RequestedDestinationFolderId ?? "<none>")}");
        }
        if (tokenChanged) {
            changes.Add("confirmation token regenerated");
        }

        return willChange || tokenChanged
            ? $"{sourcePlan.Action}: {string.Join(", ", changes)}"
            : $"{sourcePlan.Action}: unchanged";
    }
}
