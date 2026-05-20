using System.Text.Json.Serialization;
using Mailozaurr.Application;

namespace Mailozaurr.Cli;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CliErrorEnvelope))]
[JsonSerializable(typeof(CliError))]
[JsonSerializable(typeof(OperationResult))]
[JsonSerializable(typeof(MailProfileAuthenticationResult))]
[JsonSerializable(typeof(MailProfileAuthStatus))]
[JsonSerializable(typeof(MailProfileConnectionTestResult))]
[JsonSerializable(typeof(MailProfileOverviewCompact))]
[JsonSerializable(typeof(MailProfileOverview))]
[JsonSerializable(typeof(MailProfile))]
[JsonSerializable(typeof(ProfileCapabilities))]
[JsonSerializable(typeof(MailProfileValidationResult))]
[JsonSerializable(typeof(FolderRefCompact))]
[JsonSerializable(typeof(FolderRef))]
[JsonSerializable(typeof(MailFolderAliasSummary))]
[JsonSerializable(typeof(MailFolderTargetResolution))]
[JsonSerializable(typeof(MailMessageActionPlanBatchSummary))]
[JsonSerializable(typeof(MailMessageActionPlanBatchCompact))]
[JsonSerializable(typeof(MailMessageActionPlanBatch))]
[JsonSerializable(typeof(MailMessageActionPlanBatchTransformPreview))]
[JsonSerializable(typeof(MoveMessagesPreview))]
[JsonSerializable(typeof(StandardMessageActionsPreview))]
[JsonSerializable(typeof(CommonMessageActionsPreview))]
[JsonSerializable(typeof(MessageActionExecutionPlan))]
[JsonSerializable(typeof(MessageActionBatchExecutionResult))]
[JsonSerializable(typeof(DeleteMessagesPreview))]
[JsonSerializable(typeof(MessageStateChangePreview))]
[JsonSerializable(typeof(MessageActionResult))]
[JsonSerializable(typeof(MessageSummaryCompact))]
[JsonSerializable(typeof(MessageSummary))]
[JsonSerializable(typeof(MessageDetailCompact))]
[JsonSerializable(typeof(MessageDetail))]
[JsonSerializable(typeof(AttachmentSummary))]
[JsonSerializable(typeof(SavedAttachmentResult))]
[JsonSerializable(typeof(SaveAttachmentsResult))]
[JsonSerializable(typeof(SaveAttachmentsManyResult))]
[JsonSerializable(typeof(MailDraftCompact))]
[JsonSerializable(typeof(MailDraft))]
[JsonSerializable(typeof(QueuedMessageCompact))]
[JsonSerializable(typeof(QueuedMessageSummary))]
[JsonSerializable(typeof(QueueProcessResult))]
[JsonSerializable(typeof(SendResult))]
[JsonSerializable(typeof(IReadOnlyList<MailProfileOverviewCompact>), TypeInfoPropertyName = "MailProfileOverviewCompactList")]
[JsonSerializable(typeof(IReadOnlyList<MailProfileOverview>), TypeInfoPropertyName = "MailProfileOverviewList")]
[JsonSerializable(typeof(IReadOnlyList<MailProfile>), TypeInfoPropertyName = "MailProfileList")]
[JsonSerializable(typeof(IReadOnlyList<FolderRefCompact>), TypeInfoPropertyName = "FolderRefCompactList")]
[JsonSerializable(typeof(IReadOnlyList<FolderRef>), TypeInfoPropertyName = "FolderRefList")]
[JsonSerializable(typeof(IReadOnlyList<MailFolderAliasSummary>), TypeInfoPropertyName = "MailFolderAliasSummaryList")]
[JsonSerializable(typeof(IReadOnlyList<MailMessageActionPlanBatchSummary>), TypeInfoPropertyName = "MailMessageActionPlanBatchSummaryList")]
[JsonSerializable(typeof(IReadOnlyList<MailMessageActionPlanBatchCompact>), TypeInfoPropertyName = "MailMessageActionPlanBatchCompactList")]
[JsonSerializable(typeof(IReadOnlyList<MailMessageActionPlanBatch>), TypeInfoPropertyName = "MailMessageActionPlanBatchList")]
[JsonSerializable(typeof(IReadOnlyList<MessageSummaryCompact>), TypeInfoPropertyName = "MessageSummaryCompactList")]
[JsonSerializable(typeof(IReadOnlyList<MessageSummary>), TypeInfoPropertyName = "MessageSummaryList")]
[JsonSerializable(typeof(IReadOnlyList<MessageDetailCompact>), TypeInfoPropertyName = "MessageDetailCompactList")]
[JsonSerializable(typeof(IReadOnlyList<MessageDetail>), TypeInfoPropertyName = "MessageDetailList")]
[JsonSerializable(typeof(IReadOnlyList<AttachmentSummary>), TypeInfoPropertyName = "AttachmentSummaryList")]
[JsonSerializable(typeof(IReadOnlyList<MailDraftCompact>), TypeInfoPropertyName = "MailDraftCompactList")]
[JsonSerializable(typeof(IReadOnlyList<MailDraft>), TypeInfoPropertyName = "MailDraftList")]
[JsonSerializable(typeof(IReadOnlyList<QueuedMessageCompact>), TypeInfoPropertyName = "QueuedMessageCompactList")]
[JsonSerializable(typeof(IReadOnlyList<QueuedMessageSummary>), TypeInfoPropertyName = "QueuedMessageSummaryList")]
internal partial class CliJsonContext : JsonSerializerContext;

internal sealed class CliErrorEnvelope {
    public required CliError Error { get; init; }
}

internal sealed class CliError {
    public required string Type { get; init; }

    public required string Message { get; init; }
}
