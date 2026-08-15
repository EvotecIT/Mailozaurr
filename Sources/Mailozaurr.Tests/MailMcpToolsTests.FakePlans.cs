#if NET8_0_OR_GREATER
using Mailozaurr.Hosting;
using Mailozaurr.Cli.Mcp;

namespace Mailozaurr.Tests;

public sealed partial class MailMcpToolsTests {
    private sealed class FakeMessageActionPlanExchangeService : IMailMessageActionPlanExchangeService {
        public string? LastLoadedPath { get; private set; }

        public string? LastLoadedBatchPath { get; private set; }

        public string? LastSavedPath { get; private set; }

        public string? LastSavedBatchPath { get; private set; }

        public MessageActionExecutionPlan? LastSavedPlan { get; private set; }

        public IReadOnlyList<MessageActionExecutionPlan>? LastSavedBatch { get; private set; }

        public MessageActionExecutionPlan NextPlan { get; set; } = new() {
            Succeeded = true,
            Action = "move",
            ExecutionKind = "Move",
            ProfileId = "gmail-work",
            RequestedCount = 1,
            UniqueMessageCount = 1,
            RequestedDestinationFolderId = "Archive",
            MessageIds = { "message-1" }
        };

        public IReadOnlyList<MessageActionExecutionPlan> NextBatchPlans { get; set; } = Array.Empty<MessageActionExecutionPlan>();

        public Task<MessageActionExecutionPlan> LoadAsync(string path, CancellationToken cancellationToken = default) {
            LastLoadedPath = path;
            return Task.FromResult(NextPlan);
        }

        public Task<IReadOnlyList<MessageActionExecutionPlan>> LoadBatchAsync(string path, CancellationToken cancellationToken = default) {
            LastLoadedBatchPath = path;
            return Task.FromResult(NextBatchPlans);
        }

        public Task SaveAsync(string path, MessageActionExecutionPlan plan, CancellationToken cancellationToken = default) {
            LastSavedPath = path;
            LastSavedPlan = plan;
            return Task.CompletedTask;
        }

        public Task SaveBatchAsync(string path, IReadOnlyList<MessageActionExecutionPlan> plans, CancellationToken cancellationToken = default) {
            LastSavedBatchPath = path;
            LastSavedBatch = plans;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMessageActionPlanRegistryService : IMailMessageActionPlanRegistryService {
        public int ListCompactCalls { get; private set; }
        public int ListSummaryCalls { get; private set; }
        public MailMessageActionPlanBatchQuery? LastBatchQuery { get; private set; }

        public string? LastAppendedBatchId { get; private set; }

        public MessageActionExecutionPlan? LastAppendedPlan { get; private set; }

        public string? LastClonedSourceBatchId { get; private set; }

        public string? LastClonedTargetBatchId { get; private set; }

        public string? LastImportedBatchId { get; private set; }

        public string? LastImportedPath { get; private set; }

        public string? LastCreatedCommonBatchId { get; private set; }

        public string? LastCreatedCommonName { get; private set; }

        public string? LastCreatedCommonDescription { get; private set; }

        public CommonMessageActionsPreviewRequest? LastCreatedCommonRequest { get; private set; }

        public IReadOnlyList<string>? LastCreatedCommonActions { get; private set; }

        public string? LastCreatedFromPreviewBatchId { get; private set; }

        public string? LastCreatedFromPreviewName { get; private set; }

        public string? LastCreatedFromPreviewDescription { get; private set; }

        public CommonMessageActionsPreview? LastCreatedFromPreview { get; private set; }

        public IReadOnlyList<string>? LastCreatedFromPreviewActions { get; private set; }

        public string? LastTransformedSourceBatchId { get; private set; }

        public string? LastTransformedTargetBatchId { get; private set; }

        public string? LastTransformedName { get; private set; }

        public string? LastTransformedDescription { get; private set; }

        public MessageActionPlanBatchTransformRequest? LastTransformRequest { get; private set; }

        public string? LastPreviewedTransformSourceBatchId { get; private set; }

        public MessageActionPlanBatchTransformRequest? LastPreviewedTransformRequest { get; private set; }

        public string? LastExecutedBatchId { get; private set; }

        public IReadOnlyList<string>? LastExecutionConfirmationTokens { get; private set; }

        public string? LastRemovedBatchId { get; private set; }

        public int? LastRemovedIndex { get; private set; }

        public string? LastReplacedBatchId { get; private set; }

        public int? LastReplacedIndex { get; private set; }

        public MessageActionExecutionPlan? LastReplacedPlan { get; private set; }

        public Task<OperationResult> AppendImportedPlanAsync(string batchId, string path, CancellationToken cancellationToken = default) {
            LastAppendedBatchId = batchId;
            LastImportedPath = path;
            return Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' saved."));
        }

        public Task<OperationResult> AppendPlanAsync(string batchId, MessageActionExecutionPlan plan, CancellationToken cancellationToken = default) {
            LastAppendedBatchId = batchId;
            LastAppendedPlan = plan;
            return Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' saved."));
        }

        public Task<OperationResult> CloneAsync(string sourceBatchId, string targetBatchId, string name, string? description = null, CancellationToken cancellationToken = default) {
            LastClonedSourceBatchId = sourceBatchId;
            LastClonedTargetBatchId = targetBatchId;
            return Task.FromResult(OperationResult.Success($"Action plan batch '{targetBatchId}' saved."));
        }

        public Task<OperationResult> CreateCommonBatchAsync(
            string batchId,
            string name,
            CommonMessageActionsPreviewRequest request,
            IReadOnlyList<string>? actions = null,
            string? description = null,
            CancellationToken cancellationToken = default) {
            LastCreatedCommonBatchId = batchId;
            LastCreatedCommonName = name;
            LastCreatedCommonDescription = description;
            LastCreatedCommonRequest = new CommonMessageActionsPreviewRequest {
                ProfileId = request.ProfileId,
                MailboxId = request.MailboxId,
                FolderId = request.FolderId,
                DestinationFolderId = request.DestinationFolderId,
                MessageIds = request.MessageIds.ToList()
            };
            LastCreatedCommonActions = actions?.ToArray();
            return Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' saved."));
        }

        public Task<OperationResult> CreateCommonBatchFromPreviewAsync(
            string batchId,
            string name,
            CommonMessageActionsPreview preview,
            IReadOnlyList<string>? actions = null,
            string? description = null,
            CancellationToken cancellationToken = default) {
            LastCreatedFromPreviewBatchId = batchId;
            LastCreatedFromPreviewName = name;
            LastCreatedFromPreviewDescription = description;
            LastCreatedFromPreview = new CommonMessageActionsPreview {
                ProfileId = preview.ProfileId,
                MailboxId = preview.MailboxId,
                FolderId = preview.FolderId,
                RequestedDestinationFolderId = preview.RequestedDestinationFolderId,
                RequestedCount = preview.RequestedCount,
                UniqueMessageCount = preview.UniqueMessageCount,
                DuplicateOrEmptyCount = preview.DuplicateOrEmptyCount,
                MessageIds = preview.MessageIds.ToList(),
                Actions = preview.Actions.Select(action => new MessageActionPreviewItem {
                    Action = action.Action,
                    DisplayName = action.DisplayName,
                    Succeeded = action.Succeeded,
                    Code = action.Code,
                    Message = action.Message,
                    RequestedDestinationFolderId = action.RequestedDestinationFolderId,
                    DesiredState = action.DesiredState,
                    ConfirmationToken = action.ConfirmationToken
                }).ToList()
            };
            LastCreatedFromPreviewActions = actions?.ToArray();
            return Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' saved."));
        }

        public Task<OperationResult> TransformCloneAsync(
            string sourceBatchId,
            string targetBatchId,
            string name,
            MessageActionPlanBatchTransformRequest transform,
            string? description = null,
            CancellationToken cancellationToken = default) {
            LastTransformedSourceBatchId = sourceBatchId;
            LastTransformedTargetBatchId = targetBatchId;
            LastTransformedName = name;
            LastTransformedDescription = description;
            LastTransformRequest = new MessageActionPlanBatchTransformRequest {
                PlanIndexes = transform.PlanIndexes.ToList(),
                PlanNames = transform.PlanNames.ToList(),
                ProfileId = transform.ProfileId,
                MailboxId = transform.MailboxId,
                FolderId = transform.FolderId,
                DestinationFolderId = transform.DestinationFolderId
            };
            return Task.FromResult(OperationResult.Success($"Action plan batch '{targetBatchId}' saved."));
        }

        public Task<MailMessageActionPlanBatchTransformPreview> PreviewTransformCloneAsync(
            string sourceBatchId,
            MessageActionPlanBatchTransformRequest transform,
            CancellationToken cancellationToken = default) {
            LastPreviewedTransformSourceBatchId = sourceBatchId;
            LastPreviewedTransformRequest = new MessageActionPlanBatchTransformRequest {
                PlanIndexes = transform.PlanIndexes.ToList(),
                PlanNames = transform.PlanNames.ToList(),
                ProfileId = transform.ProfileId,
                MailboxId = transform.MailboxId,
                FolderId = transform.FolderId,
                DestinationFolderId = transform.DestinationFolderId
            };
            return Task.FromResult(new MailMessageActionPlanBatchTransformPreview {
                Succeeded = true,
                SourceBatchId = sourceBatchId,
                SourceBatchName = "Cleanup batch",
                PlanCount = 1,
                ChangedPlanCount = 1,
                ConfirmationTokenChangedCount = 1,
                TargetProfileExists = true,
                Plans = {
                    new MessageActionPlanBatchTransformPreviewItem {
                        Index = 0,
                        Action = "move",
                        ExecutionKind = "Move",
                        SourceProfileId = "gmail-work",
                        TargetProfileId = transform.ProfileId ?? "gmail-work",
                        SourceMailboxId = "primary",
                        TargetMailboxId = transform.MailboxId,
                        SourceFolderId = "Inbox",
                        TargetFolderId = transform.FolderId,
                        SourceDestinationFolderId = "Archive",
                        TargetDestinationFolderId = transform.DestinationFolderId,
                        WillChange = true,
                        ConfirmationTokenWillChange = true,
                        Summary = "move: preview"
                    }
                },
                Message = "Previewed transform."
            });
        }

        public Task<OperationResult> DeleteAsync(string batchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' deleted."));

        public Task<MessageActionBatchExecutionResult> ExecuteAsync(
            string batchId,
            bool continueOnError = true,
            CancellationToken cancellationToken = default,
            IReadOnlyList<string>? confirmationTokens = null) {
            LastExecutedBatchId = batchId;
            LastExecutionConfirmationTokens = confirmationTokens?.ToArray();
            return Task.FromResult(new MessageActionBatchExecutionResult {
                Succeeded = true,
                RequestedPlanCount = 1,
                AttemptedPlanCount = 1,
                SucceededPlanCount = 1,
                Message = "Stored batch executed."
            });
        }

        public Task<OperationResult> ExportAsync(string batchId, string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' exported."));

        public Task<MailMessageActionPlanBatch?> GetBatchAsync(string batchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MailMessageActionPlanBatch?>(new MailMessageActionPlanBatch {
                Id = batchId,
                Name = "Cleanup batch",
                Plans = {
                    new MessageActionExecutionPlan {
                        Succeeded = true,
                        Action = "delete",
                        ExecutionKind = "Delete",
                        ProfileId = "gmail-work",
                        RequestedCount = 1,
                        UniqueMessageCount = 1,
                        MessageIds = { "message-1" }
                    }
                }
            });

        public Task<MailMessageActionPlanBatchCompact?> GetBatchCompactAsync(string batchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MailMessageActionPlanBatchCompact?>(new MailMessageActionPlanBatchCompact {
                Id = batchId,
                Name = "Cleanup batch",
                PlanCount = 1,
                ReadyPlanCount = 1,
                ProfileCount = 1,
                PlanNames = { "Delete spam" },
                Summary = $"{batchId} (1 plan(s), 1 ready)"
            });

        public Task<MailMessageActionPlanBatchSummary?> GetBatchSummaryAsync(string batchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MailMessageActionPlanBatchSummary?>(new MailMessageActionPlanBatchSummary {
                Id = batchId,
                Name = "Cleanup batch",
                PlanCount = 1,
                ReadyPlanCount = 1,
                ProfileIds = { "gmail-work" },
                ActionCounts = {
                    ["delete"] = 1
                },
                PlanNames = { "Delete spam" },
                Summary = $"{batchId} (1 plan(s), 1 ready, 1 action type(s))"
            });

        public Task<IReadOnlyList<MailMessageActionPlanBatch>> GetBatchesAsync(MailMessageActionPlanBatchQuery? query = null, CancellationToken cancellationToken = default) {
            LastBatchQuery = query == null
                ? null
                : new MailMessageActionPlanBatchQuery {
                    PlanNames = query.PlanNames.ToList(),
                    ProfileIds = query.ProfileIds.ToList(),
                    Actions = query.Actions.ToList(),
                    SortBy = query.SortBy,
                    Descending = query.Descending
                };
            return Task.FromResult<IReadOnlyList<MailMessageActionPlanBatch>>(new[] {
                new MailMessageActionPlanBatch {
                    Id = "cleanup",
                    Name = "Cleanup batch",
                    Plans = {
                        new MessageActionExecutionPlan {
                            Succeeded = true,
                            Action = "delete",
                            ExecutionKind = "Delete",
                            ProfileId = "gmail-work",
                            RequestedCount = 1,
                            UniqueMessageCount = 1,
                            MessageIds = { "message-1" }
                        }
                    }
                }
            });
        }

        public Task<IReadOnlyList<MailMessageActionPlanBatchCompact>> GetBatchesCompactAsync(MailMessageActionPlanBatchQuery? query = null, CancellationToken cancellationToken = default) {
            LastBatchQuery = query == null
                ? null
                : new MailMessageActionPlanBatchQuery {
                    PlanNames = query.PlanNames.ToList(),
                    ProfileIds = query.ProfileIds.ToList(),
                    Actions = query.Actions.ToList(),
                    SortBy = query.SortBy,
                    Descending = query.Descending
                };
            ListCompactCalls++;
            return Task.FromResult<IReadOnlyList<MailMessageActionPlanBatchCompact>>(new[] {
                new MailMessageActionPlanBatchCompact {
                    Id = "cleanup",
                    Name = "Cleanup batch",
                    PlanCount = 1,
                    ReadyPlanCount = 1,
                    ProfileCount = 1,
                    PlanNames = { "Delete spam" },
                    Summary = "cleanup (1 plan(s), 1 ready)"
                }
            });
        }

        public Task<IReadOnlyList<MailMessageActionPlanBatchSummary>> GetBatchesSummaryAsync(MailMessageActionPlanBatchQuery? query = null, CancellationToken cancellationToken = default) {
            LastBatchQuery = query == null
                ? null
                : new MailMessageActionPlanBatchQuery {
                    PlanNames = query.PlanNames.ToList(),
                    ProfileIds = query.ProfileIds.ToList(),
                    Actions = query.Actions.ToList(),
                    SortBy = query.SortBy,
                    Descending = query.Descending
                };
            ListSummaryCalls++;
            return Task.FromResult<IReadOnlyList<MailMessageActionPlanBatchSummary>>(new[] {
                new MailMessageActionPlanBatchSummary {
                    Id = "cleanup",
                    Name = "Cleanup batch",
                    PlanCount = 1,
                    ReadyPlanCount = 1,
                    ProfileIds = { "gmail-work" },
                    ActionCounts = {
                        ["delete"] = 1
                    },
                    PlanNames = { "Delete spam" },
                    Summary = "cleanup (1 plan(s), 1 ready, 1 action type(s))"
                }
            });
        }

        public Task<OperationResult> ImportAsync(string batchId, string name, string path, string? description = null, CancellationToken cancellationToken = default) {
            LastImportedBatchId = batchId;
            LastImportedPath = path;
            return Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' saved."));
        }

        public Task<OperationResult> ReplaceImportedPlanAtAsync(string batchId, int index, string path, CancellationToken cancellationToken = default) {
            LastReplacedBatchId = batchId;
            LastReplacedIndex = index;
            LastImportedPath = path;
            return Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' saved."));
        }

        public Task<OperationResult> ReplacePlanAtAsync(string batchId, int index, MessageActionExecutionPlan plan, CancellationToken cancellationToken = default) {
            LastReplacedBatchId = batchId;
            LastReplacedIndex = index;
            LastReplacedPlan = plan;
            return Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' saved."));
        }

        public Task<OperationResult> RemovePlanAtAsync(string batchId, int index, CancellationToken cancellationToken = default) {
            LastRemovedBatchId = batchId;
            LastRemovedIndex = index;
            return Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' saved."));
        }

        public Task<OperationResult> SaveAsync(MailMessageActionPlanBatch batch, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success($"Action plan batch '{batch.Id}' saved."));
    }
}
#endif
