using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationMessageActionPlanRegistryServiceTests {
    [Fact]
    public async Task SaveAsyncRejectsBatchWhenPlanProfileIsMissing() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var batchStore = new FileMailMessageActionPlanBatchStore(CreateTemporaryFilePath("action-plan-batches.json"));
        var planService = new PassThroughPlanService();
        var previewService = new FakePreviewService();
        var service = new MailMessageActionPlanRegistryService(
            batchStore,
            new JsonMailMessageActionPlanExchangeService(),
            previewService,
            planService,
            new MailMessageActionBatchService(planService),
            profileStore);

        var result = await service.SaveAsync(new MailMessageActionPlanBatch {
            Id = "missing-profile-batch",
            Name = "Missing profile batch",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Action = "delete",
                    ExecutionKind = "Delete",
                    ProfileId = "missing-profile",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-1" }
                }
            }
        });

        Assert.False(result.Succeeded);
        Assert.Equal("action_plan_profile_not_found", result.Code);
    }

    [Fact]
    public async Task ImportAndExecuteUsesSharedRegistryPipeline() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-gmail",
            DisplayName = "Work Gmail",
            Kind = MailProfileKind.Gmail
        });
        var batchStore = new FileMailMessageActionPlanBatchStore(CreateTemporaryFilePath("action-plan-batches.json"));
        var exchange = new JsonMailMessageActionPlanExchangeService();
        var importPath = CreateTemporaryFilePath("plans.json");
        await exchange.SaveBatchAsync(importPath, new[] {
            new MessageActionExecutionPlan {
                Succeeded = true,
                Action = "mark-read",
                ExecutionKind = "SetReadState",
                ProfileId = "work-gmail",
                RequestedCount = 1,
                UniqueMessageCount = 1,
                DesiredState = true,
                MessageIds = { "msg-1" }
            }
        });

        var planService = new PassThroughPlanService();
        var batchService = new MailMessageActionBatchService(planService);
        var service = new MailMessageActionPlanRegistryService(batchStore, exchange, new FakePreviewService(), planService, batchService, profileStore);

        var importResult = await service.ImportAsync("cleanup", "Cleanup", importPath);
        var compact = await service.GetBatchCompactAsync("cleanup");
        var execution = await service.ExecuteAsync("cleanup");

        Assert.True(importResult.Succeeded);
        Assert.NotNull(compact);
        Assert.Equal(1, compact!.PlanCount);
        Assert.Equal(new[] { "mark-read (1 message)" }, compact.PlanNames);
        Assert.True(execution.Succeeded);
        Assert.Equal(1, execution.SucceededPlanCount);
    }

    [Fact]
    public async Task GetBatchesCompactCanFilterByPlanNames() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-gmail",
            DisplayName = "Work Gmail",
            Kind = MailProfileKind.Gmail
        });
        var batchStore = new FileMailMessageActionPlanBatchStore(CreateTemporaryFilePath("action-plan-batches.json"));
        var service = new MailMessageActionPlanRegistryService(
            batchStore,
            new JsonMailMessageActionPlanExchangeService(),
            new FakePreviewService(),
            new PassThroughPlanService(),
            new MailMessageActionBatchService(new PassThroughPlanService()),
            profileStore);

        await service.SaveAsync(new MailMessageActionPlanBatch {
            Id = "cleanup",
            Name = "Cleanup",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Name = "Delete spam",
                    Summary = "Delete spam (1 message)",
                    Action = "delete",
                    ExecutionKind = "Delete",
                    ProfileId = "work-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-1" }
                }
            }
        });
        await service.SaveAsync(new MailMessageActionPlanBatch {
            Id = "review",
            Name = "Review",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Name = "Archive newsletter",
                    Summary = "Archive newsletter (1 message)",
                    Action = "move",
                    ExecutionKind = "Move",
                    ProfileId = "work-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    RequestedDestinationFolderId = "Archive",
                    MessageIds = { "msg-2" }
                }
            }
        });

        var compact = await service.GetBatchesCompactAsync(new MailMessageActionPlanBatchQuery {
            PlanNames = { "Delete spam" }
        });

        var batch = Assert.Single(compact);
        Assert.Equal("cleanup", batch.Id);
        Assert.Equal(new[] { "Delete spam" }, batch.PlanNames);
    }

    [Fact]
    public async Task GetBatchesCompactCanFilterByProfileIds() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-gmail",
            DisplayName = "Work Gmail",
            Kind = MailProfileKind.Gmail
        });
        await profileStore.SaveAsync(new MailProfile {
            Id = "ops-imap",
            DisplayName = "Ops IMAP",
            Kind = MailProfileKind.Imap
        });
        var batchStore = new FileMailMessageActionPlanBatchStore(CreateTemporaryFilePath("action-plan-batches.json"));
        var service = new MailMessageActionPlanRegistryService(
            batchStore,
            new JsonMailMessageActionPlanExchangeService(),
            new FakePreviewService(),
            new PassThroughPlanService(),
            new MailMessageActionBatchService(new PassThroughPlanService()),
            profileStore);

        await service.SaveAsync(new MailMessageActionPlanBatch {
            Id = "cleanup",
            Name = "Cleanup",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Name = "Delete spam",
                    Summary = "Delete spam (1 message)",
                    Action = "delete",
                    ExecutionKind = "Delete",
                    ProfileId = "work-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-1" }
                }
            }
        });
        await service.SaveAsync(new MailMessageActionPlanBatch {
            Id = "ops-review",
            Name = "Ops review",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Name = "Archive alerts",
                    Summary = "Archive alerts (1 message)",
                    Action = "move",
                    ExecutionKind = "Move",
                    ProfileId = "ops-imap",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    RequestedDestinationFolderId = "Archive",
                    MessageIds = { "msg-2" }
                }
            }
        });

        var compact = await service.GetBatchesCompactAsync(new MailMessageActionPlanBatchQuery {
            ProfileIds = { "ops-imap" }
        });

        var batch = Assert.Single(compact);
        Assert.Equal("ops-review", batch.Id);
        Assert.Equal(new[] { "Archive alerts" }, batch.PlanNames);
    }

    [Fact]
    public async Task GetBatchesCompactCanFilterByActions() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-gmail",
            DisplayName = "Work Gmail",
            Kind = MailProfileKind.Gmail
        });
        var batchStore = new FileMailMessageActionPlanBatchStore(CreateTemporaryFilePath("action-plan-batches.json"));
        var service = new MailMessageActionPlanRegistryService(
            batchStore,
            new JsonMailMessageActionPlanExchangeService(),
            new FakePreviewService(),
            new PassThroughPlanService(),
            new MailMessageActionBatchService(new PassThroughPlanService()),
            profileStore);

        await service.SaveAsync(new MailMessageActionPlanBatch {
            Id = "cleanup",
            Name = "Cleanup",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Name = "Delete spam",
                    Summary = "Delete spam (1 message)",
                    Action = "delete",
                    ExecutionKind = "Delete",
                    ProfileId = "work-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-1" }
                }
            }
        });
        await service.SaveAsync(new MailMessageActionPlanBatch {
            Id = "review",
            Name = "Review",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Name = "Archive newsletter",
                    Summary = "Archive newsletter (1 message)",
                    Action = "move",
                    ExecutionKind = "Move",
                    ProfileId = "work-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    RequestedDestinationFolderId = "Archive",
                    MessageIds = { "msg-2" }
                }
            }
        });

        var compact = await service.GetBatchesCompactAsync(new MailMessageActionPlanBatchQuery {
            Actions = { "delete" }
        });

        var batch = Assert.Single(compact);
        Assert.Equal("cleanup", batch.Id);
        Assert.Equal(new[] { "Delete spam" }, batch.PlanNames);
    }

    [Fact]
    public async Task GetBatchSummaryProvidesProfileIdsAndActionCounts() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-gmail",
            DisplayName = "Work Gmail",
            Kind = MailProfileKind.Gmail
        });
        await profileStore.SaveAsync(new MailProfile {
            Id = "ops-imap",
            DisplayName = "Ops IMAP",
            Kind = MailProfileKind.Imap
        });
        var batchStore = new FileMailMessageActionPlanBatchStore(CreateTemporaryFilePath("action-plan-batches.json"));
        var service = new MailMessageActionPlanRegistryService(
            batchStore,
            new JsonMailMessageActionPlanExchangeService(),
            new FakePreviewService(),
            new PassThroughPlanService(),
            new MailMessageActionBatchService(new PassThroughPlanService()),
            profileStore);

        await service.SaveAsync(new MailMessageActionPlanBatch {
            Id = "cleanup",
            Name = "Cleanup",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Name = "Delete spam",
                    Summary = "Delete spam (1 message)",
                    Action = "delete",
                    ExecutionKind = "Delete",
                    ProfileId = "work-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-1" }
                },
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Name = "Archive alerts",
                    Summary = "Archive alerts (1 message)",
                    Action = "move",
                    ExecutionKind = "Move",
                    ProfileId = "ops-imap",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    RequestedDestinationFolderId = "Archive",
                    MessageIds = { "msg-2" }
                },
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Name = "Delete junk",
                    Summary = "Delete junk (1 message)",
                    Action = "delete",
                    ExecutionKind = "Delete",
                    ProfileId = "work-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-3" }
                }
            }
        });

        var summary = await service.GetBatchSummaryAsync("cleanup");

        Assert.NotNull(summary);
        Assert.Equal(new[] { "ops-imap", "work-gmail" }, summary!.ProfileIds);
        Assert.Equal(2, summary.ActionCounts["delete"]);
        Assert.Equal(1, summary.ActionCounts["move"]);
        Assert.Equal(3, summary.PlanCount);
    }

    [Fact]
    public async Task GetBatchesSummaryCanSortByPlanCountDescending() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-gmail",
            DisplayName = "Work Gmail",
            Kind = MailProfileKind.Gmail
        });
        var batchStore = new FileMailMessageActionPlanBatchStore(CreateTemporaryFilePath("action-plan-batches.json"));
        var service = new MailMessageActionPlanRegistryService(
            batchStore,
            new JsonMailMessageActionPlanExchangeService(),
            new FakePreviewService(),
            new PassThroughPlanService(),
            new MailMessageActionBatchService(new PassThroughPlanService()),
            profileStore);

        await service.SaveAsync(new MailMessageActionPlanBatch {
            Id = "small",
            Name = "Small",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Action = "delete",
                    ExecutionKind = "Delete",
                    ProfileId = "work-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-1" }
                }
            }
        });
        await service.SaveAsync(new MailMessageActionPlanBatch {
            Id = "large",
            Name = "Large",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Action = "delete",
                    ExecutionKind = "Delete",
                    ProfileId = "work-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-2" }
                },
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Action = "move",
                    ExecutionKind = "Move",
                    ProfileId = "work-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    RequestedDestinationFolderId = "Archive",
                    MessageIds = { "msg-3" }
                }
            }
        });

        var summaries = await service.GetBatchesSummaryAsync(new MailMessageActionPlanBatchQuery {
            SortBy = MailMessageActionPlanBatchSortBy.PlanCount,
            Descending = true
        });

        Assert.Equal(new[] { "large", "small" }, summaries.Select(summary => summary.Id));
    }

    [Fact]
    public async Task AppendAndRemovePlanMutateStoredBatch() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-gmail",
            DisplayName = "Work Gmail",
            Kind = MailProfileKind.Gmail
        });
        var batchStore = new FileMailMessageActionPlanBatchStore(CreateTemporaryFilePath("action-plan-batches.json"));
        var planService = new PassThroughPlanService();
        var previewService = new FakePreviewService();
        var service = new MailMessageActionPlanRegistryService(
            batchStore,
            new JsonMailMessageActionPlanExchangeService(),
            previewService,
            planService,
            new MailMessageActionBatchService(planService),
            profileStore);

        await service.SaveAsync(new MailMessageActionPlanBatch {
            Id = "cleanup",
            Name = "Cleanup",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Action = "mark-read",
                    ExecutionKind = "SetReadState",
                    ProfileId = "work-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    DesiredState = true,
                    MessageIds = { "msg-1" }
                },
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Action = "delete",
                    ExecutionKind = "Delete",
                    ProfileId = "work-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-2" }
                }
            }
        });

        var appendResult = await service.AppendPlanAsync("cleanup", new MessageActionExecutionPlan {
            Succeeded = true,
            Action = "move",
            ExecutionKind = "Move",
            ProfileId = "work-gmail",
            RequestedCount = 1,
            UniqueMessageCount = 1,
            RequestedDestinationFolderId = "Archive",
            MessageIds = { "msg-3" }
        });
        var afterAppend = await service.GetBatchAsync("cleanup");
        var removeResult = await service.RemovePlanAtAsync("cleanup", 1);
        var afterRemove = await service.GetBatchAsync("cleanup");

        Assert.True(appendResult.Succeeded);
        Assert.NotNull(afterAppend);
        Assert.Equal(3, afterAppend!.Plans.Count);
        Assert.True(removeResult.Succeeded);
        Assert.NotNull(afterRemove);
        Assert.Equal(2, afterRemove!.Plans.Count);
        Assert.DoesNotContain(afterRemove.Plans, plan => plan.Action == "delete");
    }

    [Fact]
    public async Task CloneAndReplacePlanMutateStoredBatch() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-gmail",
            DisplayName = "Work Gmail",
            Kind = MailProfileKind.Gmail
        });
        var batchStore = new FileMailMessageActionPlanBatchStore(CreateTemporaryFilePath("action-plan-batches.json"));
        var exchange = new JsonMailMessageActionPlanExchangeService();
        var planService = new PassThroughPlanService();
        var previewService = new FakePreviewService();
        var service = new MailMessageActionPlanRegistryService(
            batchStore,
            exchange,
            previewService,
            planService,
            new MailMessageActionBatchService(planService),
            profileStore);

        await service.SaveAsync(new MailMessageActionPlanBatch {
            Id = "cleanup",
            Name = "Cleanup",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Action = "delete",
                    ExecutionKind = "Delete",
                    ProfileId = "work-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-1" }
                }
            }
        });

        var cloneResult = await service.CloneAsync("cleanup", "cleanup-copy", "Cleanup copy");
        var cloned = await service.GetBatchAsync("cleanup-copy");
        var replaceResult = await service.ReplacePlanAtAsync("cleanup-copy", 0, new MessageActionExecutionPlan {
            Succeeded = true,
            Action = "move",
            ExecutionKind = "Move",
            ProfileId = "work-gmail",
            RequestedCount = 1,
            UniqueMessageCount = 1,
            RequestedDestinationFolderId = "Archive",
            MessageIds = { "msg-2" }
        });
        var replaced = await service.GetBatchAsync("cleanup-copy");

        Assert.True(cloneResult.Succeeded);
        Assert.NotNull(cloned);
        Assert.Equal("Cleanup copy", cloned!.Name);
        Assert.True(replaceResult.Succeeded);
        Assert.NotNull(replaced);
        Assert.Single(replaced!.Plans);
        Assert.Equal("move", replaced.Plans[0].Action);
    }

    [Fact]
    public async Task CreateCommonBatchBuildsAndStoresSupportedPlans() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-gmail",
            DisplayName = "Work Gmail",
            Kind = MailProfileKind.Gmail
        });
        var batchStore = new FileMailMessageActionPlanBatchStore(CreateTemporaryFilePath("action-plan-batches.json"));
        var exchange = new JsonMailMessageActionPlanExchangeService();
        var planService = new RecordingPlanService();
        var previewService = new FakePreviewService {
            NextCommonPreview = new CommonMessageActionsPreview {
                Succeeded = true,
                ProfileId = "work-gmail",
                MailboxId = "primary",
                FolderId = "Inbox",
                RequestedDestinationFolderId = "Archive",
                RequestedCount = 2,
                UniqueMessageCount = 1,
                DuplicateOrEmptyCount = 1,
                MessageIds = { "msg-1" },
                IncludedActionCount = 3,
                SucceededActionCount = 2,
                FailedActionCount = 1,
                Actions = {
                    new MessageActionPreviewItem {
                        Action = "mark-read",
                        DisplayName = "Mark as read",
                        Succeeded = true,
                        ConfirmationToken = "token-read"
                    },
                    new MessageActionPreviewItem {
                        Action = "move",
                        DisplayName = "Move to Archive",
                        Succeeded = true,
                        RequestedDestinationFolderId = "Archive",
                        Destination = new MailFolderTargetResolution {
                            ProfileId = "work-gmail",
                            MailboxId = "primary",
                            RequestedValue = "Archive",
                            EffectiveFolderId = "Archive",
                            IsSupported = true,
                            IsResolved = true,
                            Summary = "Archive"
                        },
                        ConfirmationToken = "token-move"
                    },
                    new MessageActionPreviewItem {
                        Action = "unsupported",
                        DisplayName = "Unsupported",
                        Succeeded = false,
                        Code = "action_not_supported"
                    }
                }
            }
        };
        var service = new MailMessageActionPlanRegistryService(
            batchStore,
            exchange,
            previewService,
            planService,
            new MailMessageActionBatchService(planService),
            profileStore);

        var result = await service.CreateCommonBatchAsync(
            "cleanup",
            "Cleanup",
            new CommonMessageActionsPreviewRequest {
                ProfileId = "work-gmail",
                MailboxId = "primary",
                FolderId = "Inbox",
                MessageIds = { "msg-1", "MSG-1" },
                DestinationFolderId = "Archive"
            },
            new[] { "mark-read", "move", "unsupported" },
            "Created from common actions");
        var stored = await service.GetBatchAsync("cleanup");

        Assert.True(result.Succeeded);
        Assert.NotNull(previewService.LastCommonRequest);
        Assert.Equal("work-gmail", previewService.LastCommonRequest!.ProfileId);
        Assert.Equal("primary", previewService.LastCommonRequest.MailboxId);
        Assert.Equal("Inbox", previewService.LastCommonRequest.FolderId);
        Assert.Equal("Archive", previewService.LastCommonRequest.DestinationFolderId);
        Assert.Equal(new[] { "msg-1", "MSG-1" }, previewService.LastCommonRequest.MessageIds);
        Assert.Equal(2, planService.Requests.Count);
        Assert.NotNull(stored);
        Assert.Equal("Created from common actions", stored!.Description);
        Assert.Equal(new[] { "mark-read", "move" }, stored.Plans.Select(plan => plan.Action));
        Assert.Equal("Archive", stored.Plans.Single(plan => plan.Action == "move").RequestedDestinationFolderId);
        Assert.Equal(new[] { "msg-1" }, stored.Plans[0].MessageIds);
        Assert.Equal("token-read", planService.Requests[0].ConfirmationToken);
        Assert.Equal("token-move", planService.Requests[1].ConfirmationToken);
    }

    [Fact]
    public async Task CreateCommonBatchFromPreviewUsesPreviewedActionsWithoutRestatingSelection() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-gmail",
            DisplayName = "Work Gmail",
            Kind = MailProfileKind.Gmail
        });
        var batchStore = new FileMailMessageActionPlanBatchStore(CreateTemporaryFilePath("action-plan-batches.json"));
        var exchange = new JsonMailMessageActionPlanExchangeService();
        var planService = new RecordingPlanService();
        var service = new MailMessageActionPlanRegistryService(
            batchStore,
            exchange,
            new FakePreviewService(),
            planService,
            new MailMessageActionBatchService(planService),
            profileStore);

        var result = await service.CreateCommonBatchFromPreviewAsync(
            "cleanup-previewed",
            "Cleanup Previewed",
            new CommonMessageActionsPreview {
                Succeeded = true,
                ProfileId = "work-gmail",
                MailboxId = "primary",
                FolderId = "Inbox",
                RequestedDestinationFolderId = "Projects/2026",
                RequestedCount = 3,
                UniqueMessageCount = 2,
                DuplicateOrEmptyCount = 1,
                MessageIds = { "msg-1", "msg-2" },
                Actions = {
                    new MessageActionPreviewItem {
                        Action = "flag",
                        DisplayName = "Flag",
                        Succeeded = true,
                        DesiredState = true,
                        ConfirmationToken = "token-flag"
                    },
                    new MessageActionPreviewItem {
                        Action = "move",
                        DisplayName = "Move to Projects",
                        Succeeded = true,
                        RequestedDestinationFolderId = "Projects/2026",
                        Destination = new MailFolderTargetResolution {
                            ProfileId = "work-gmail",
                            MailboxId = "primary",
                            RequestedValue = "Projects/2026",
                            EffectiveFolderId = "Projects/2026",
                            IsSupported = true,
                            IsResolved = true,
                            Summary = "Projects/2026"
                        },
                        ConfirmationToken = "token-move"
                    },
                    new MessageActionPreviewItem {
                        Action = "delete",
                        DisplayName = "Delete",
                        Succeeded = false,
                        Code = "delete_not_supported"
                    }
                }
            },
            description: "Built from preview");
        var stored = await service.GetBatchAsync("cleanup-previewed");

        Assert.True(result.Succeeded);
        Assert.Equal(2, planService.Requests.Count);
        Assert.Equal(new[] { "flag", "move" }, planService.Requests.Select(request => request.Action));
        Assert.All(planService.Requests, request => Assert.Equal(new[] { "msg-1", "msg-2" }, request.MessageIds));
        Assert.Equal("token-flag", planService.Requests[0].ConfirmationToken);
        Assert.Equal("token-move", planService.Requests[1].ConfirmationToken);
        Assert.Equal("Projects/2026", planService.Requests[1].DestinationFolderId);
        Assert.NotNull(stored);
        Assert.Equal("Built from preview", stored!.Description);
        Assert.Equal(2, stored.Plans.Count);
    }

    [Fact]
    public async Task TransformCloneRewritesTargetsAndRegeneratesConfirmationTokens() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "source-gmail",
            DisplayName = "Source Gmail",
            Kind = MailProfileKind.Gmail
        });
        await profileStore.SaveAsync(new MailProfile {
            Id = "target-gmail",
            DisplayName = "Target Gmail",
            Kind = MailProfileKind.Gmail
        });
        var batchStore = new FileMailMessageActionPlanBatchStore(CreateTemporaryFilePath("action-plan-batches.json"));
        var exchange = new JsonMailMessageActionPlanExchangeService();
        var planService = new RecordingPlanService();
        var service = new MailMessageActionPlanRegistryService(
            batchStore,
            exchange,
            new FakePreviewService(),
            planService,
            new MailMessageActionBatchService(planService),
            profileStore);

        await service.SaveAsync(new MailMessageActionPlanBatch {
            Id = "cleanup",
            Name = "Cleanup",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Action = "mark-read",
                    ExecutionKind = "SetReadState",
                    ProfileId = "source-gmail",
                    MailboxId = "source@example.com",
                    FolderId = "Inbox",
                    RequestedCount = 2,
                    UniqueMessageCount = 2,
                    DesiredState = true,
                    MessageIds = { "msg-1", "msg-2" },
                    ConfirmationToken = MessageActionConfirmationTokens.CreateReadStateToken("source-gmail", "source@example.com", "Inbox", new[] { "msg-1", "msg-2" }, true),
                    ConfirmationProvided = true,
                    ConfirmationValidated = true
                },
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Name = "Archive newsletter",
                    Summary = "Archive newsletter (1 message)",
                    Action = "move",
                    ExecutionKind = "Move",
                    ProfileId = "source-gmail",
                    MailboxId = "source@example.com",
                    FolderId = "Inbox",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-3" },
                    RequestedDestinationFolderId = "Archive",
                    Destination = new MailFolderTargetResolution {
                        ProfileId = "source-gmail",
                        MailboxId = "source@example.com",
                        RequestedValue = "Archive",
                        EffectiveFolderId = "Archive",
                        IsSupported = true,
                        IsResolved = true,
                        Summary = "Archive"
                    },
                    ConfirmationToken = MessageActionConfirmationTokens.CreateMoveToken("source-gmail", "source@example.com", "Inbox", new[] { "msg-3" }, "Archive"),
                    ConfirmationProvided = true,
                    ConfirmationValidated = true
                },
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Action = "delete",
                    ExecutionKind = "Delete",
                    ProfileId = "source-gmail",
                    MailboxId = "source@example.com",
                    FolderId = "Inbox",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-4" },
                    ConfirmationToken = MessageActionConfirmationTokens.CreateDeleteToken("source-gmail", "source@example.com", "Inbox", new[] { "msg-4" }),
                    ConfirmationProvided = true,
                    ConfirmationValidated = true
                }
            }
        });

        var result = await service.TransformCloneAsync(
            "cleanup",
            "cleanup-target",
            "Cleanup Target",
            new MessageActionPlanBatchTransformRequest {
                ProfileId = "target-gmail",
                MailboxId = "target@example.com",
                FolderId = "Projects",
                DestinationFolderId = "Projects/Archive"
            },
            "Remapped batch");
        var transformed = await service.GetBatchAsync("cleanup-target");

        Assert.True(result.Succeeded);
        Assert.NotNull(transformed);
        Assert.Equal("Remapped batch", transformed!.Description);
        Assert.All(transformed.Plans, plan => {
            Assert.Equal("target-gmail", plan.ProfileId);
            Assert.Equal("target@example.com", plan.MailboxId);
            Assert.Equal("Projects", plan.FolderId);
            Assert.False(plan.ConfirmationProvided);
            Assert.True(plan.ConfirmationValidated);
            Assert.False(string.IsNullOrWhiteSpace(plan.ConfirmationToken));
        });

        var transformedMove = transformed.Plans.Single(plan => plan.Action == "move");
        Assert.Equal("Projects/Archive", transformedMove.RequestedDestinationFolderId);
        Assert.Null(transformedMove.Destination);
        Assert.Equal(
            MessageActionConfirmationTokens.CreateMoveToken("target-gmail", "target@example.com", "Projects", new[] { "msg-3" }, "Projects/Archive"),
            transformedMove.ConfirmationToken);

        var transformedRead = transformed.Plans.Single(plan => plan.Action == "mark-read");
        Assert.Equal(
            MessageActionConfirmationTokens.CreateReadStateToken("target-gmail", "target@example.com", "Projects", new[] { "msg-1", "msg-2" }, true),
            transformedRead.ConfirmationToken);

        var transformedDelete = transformed.Plans.Single(plan => plan.Action == "delete");
        Assert.Equal(
            MessageActionConfirmationTokens.CreateDeleteToken("target-gmail", "target@example.com", "Projects", new[] { "msg-4" }),
            transformedDelete.ConfirmationToken);
    }

    [Fact]
    public async Task PreviewTransformCloneReportsChangesAndValidationState() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "source-gmail",
            DisplayName = "Source Gmail",
            Kind = MailProfileKind.Gmail
        });
        await profileStore.SaveAsync(new MailProfile {
            Id = "target-gmail",
            DisplayName = "Target Gmail",
            Kind = MailProfileKind.Gmail
        });
        var batchStore = new FileMailMessageActionPlanBatchStore(CreateTemporaryFilePath("action-plan-batches.json"));
        var exchange = new JsonMailMessageActionPlanExchangeService();
        var planService = new RecordingPlanService();
        var service = new MailMessageActionPlanRegistryService(
            batchStore,
            exchange,
            new FakePreviewService(),
            planService,
            new MailMessageActionBatchService(planService),
            profileStore);

        await service.SaveAsync(new MailMessageActionPlanBatch {
            Id = "cleanup",
            Name = "Cleanup",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Action = "mark-read",
                    ExecutionKind = "SetReadState",
                    ProfileId = "source-gmail",
                    MailboxId = "source@example.com",
                    FolderId = "Inbox",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    DesiredState = true,
                    MessageIds = { "msg-1" },
                    ConfirmationToken = MessageActionConfirmationTokens.CreateReadStateToken("source-gmail", "source@example.com", "Inbox", new[] { "msg-1" }, true),
                    ConfirmationProvided = true,
                    ConfirmationValidated = true
                },
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Action = "move",
                    ExecutionKind = "Move",
                    ProfileId = "source-gmail",
                    MailboxId = "source@example.com",
                    FolderId = "Inbox",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-2" },
                    RequestedDestinationFolderId = "Archive",
                    Destination = new MailFolderTargetResolution {
                        ProfileId = "source-gmail",
                        MailboxId = "source@example.com",
                        RequestedValue = "Archive",
                        EffectiveFolderId = "Archive",
                        IsSupported = true,
                        IsResolved = true,
                        Summary = "Archive"
                    },
                    ConfirmationToken = MessageActionConfirmationTokens.CreateMoveToken("source-gmail", "source@example.com", "Inbox", new[] { "msg-2" }, "Archive"),
                    ConfirmationProvided = true,
                    ConfirmationValidated = true
                }
            }
        });

        var preview = await service.PreviewTransformCloneAsync(
            "cleanup",
            new MessageActionPlanBatchTransformRequest {
                ProfileId = "target-gmail",
                MailboxId = "target@example.com",
                FolderId = "Projects",
                DestinationFolderId = "Projects/Archive"
            });

        Assert.True(preview.Succeeded);
        Assert.Equal("cleanup", preview.SourceBatchId);
        Assert.Equal("Cleanup", preview.SourceBatchName);
        Assert.Equal(2, preview.PlanCount);
        Assert.Equal(2, preview.ChangedPlanCount);
        Assert.Equal(2, preview.ConfirmationTokenChangedCount);
        Assert.True(preview.TargetProfileExists);
        Assert.Equal(MailProfileKind.Gmail, preview.TargetProfileKind);
        Assert.Empty(preview.Errors);
        Assert.Equal(2, preview.Plans.Count);
        Assert.All(preview.Plans, plan => Assert.True(plan.WillChange));
        Assert.Contains(preview.Plans, plan => plan.Action == "move" && plan.TargetDestinationFolderId == "Projects/Archive" && plan.ConfirmationTokenWillChange);
    }

    [Fact]
    public async Task PreviewTransformCloneFailsWhenTargetProfileIsMissing() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "source-gmail",
            DisplayName = "Source Gmail",
            Kind = MailProfileKind.Gmail
        });
        var batchStore = new FileMailMessageActionPlanBatchStore(CreateTemporaryFilePath("action-plan-batches.json"));
        var exchange = new JsonMailMessageActionPlanExchangeService();
        var planService = new RecordingPlanService();
        var service = new MailMessageActionPlanRegistryService(
            batchStore,
            exchange,
            new FakePreviewService(),
            planService,
            new MailMessageActionBatchService(planService),
            profileStore);

        await service.SaveAsync(new MailMessageActionPlanBatch {
            Id = "cleanup",
            Name = "Cleanup",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Action = "delete",
                    ExecutionKind = "Delete",
                    ProfileId = "source-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-1" }
                }
            }
        });

        var preview = await service.PreviewTransformCloneAsync(
            "cleanup",
            new MessageActionPlanBatchTransformRequest {
                ProfileId = "missing-profile"
            });

        Assert.False(preview.Succeeded);
        Assert.Equal("action_plan_profile_not_found", preview.Code);
        Assert.False(preview.TargetProfileExists);
        Assert.Contains(preview.Errors, error => error.Contains("missing-profile", StringComparison.Ordinal));
        Assert.Single(preview.Plans);
    }

    [Fact]
    public async Task TransformCloneCanLimitSelectionToSpecificPlanIndexes() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "source-gmail",
            DisplayName = "Source Gmail",
            Kind = MailProfileKind.Gmail
        });
        await profileStore.SaveAsync(new MailProfile {
            Id = "target-gmail",
            DisplayName = "Target Gmail",
            Kind = MailProfileKind.Gmail
        });
        var batchStore = new FileMailMessageActionPlanBatchStore(CreateTemporaryFilePath("action-plan-batches.json"));
        var exchange = new JsonMailMessageActionPlanExchangeService();
        var planService = new RecordingPlanService();
        var service = new MailMessageActionPlanRegistryService(
            batchStore,
            exchange,
            new FakePreviewService(),
            planService,
            new MailMessageActionBatchService(planService),
            profileStore);

        await service.SaveAsync(new MailMessageActionPlanBatch {
            Id = "cleanup",
            Name = "Cleanup",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Action = "mark-read",
                    ExecutionKind = "SetReadState",
                    ProfileId = "source-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    DesiredState = true,
                    MessageIds = { "msg-1" }
                },
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Action = "move",
                    ExecutionKind = "Move",
                    ProfileId = "source-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-2" },
                    RequestedDestinationFolderId = "Archive"
                },
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Action = "delete",
                    ExecutionKind = "Delete",
                    ProfileId = "source-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-3" }
                }
            }
        });

        var preview = await service.PreviewTransformCloneAsync(
            "cleanup",
            new MessageActionPlanBatchTransformRequest {
                PlanIndexes = { 1, 2 },
                ProfileId = "target-gmail",
                DestinationFolderId = "Projects/Archive"
            });
        var result = await service.TransformCloneAsync(
            "cleanup",
            "cleanup-subset",
            "Cleanup Subset",
            new MessageActionPlanBatchTransformRequest {
                PlanIndexes = { 1, 2 },
                ProfileId = "target-gmail",
                DestinationFolderId = "Projects/Archive"
            });
        var transformed = await service.GetBatchAsync("cleanup-subset");

        Assert.True(preview.Succeeded);
        Assert.Equal(2, preview.PlanCount);
        Assert.Equal(new[] { 1, 2 }, preview.Plans.Select(plan => plan.Index));
        Assert.True(result.Succeeded);
        Assert.NotNull(transformed);
        Assert.Equal(2, transformed!.Plans.Count);
        Assert.Equal(new[] { "move", "delete" }, transformed.Plans.Select(plan => plan.Action));
    }

    [Fact]
    public async Task TransformCloneCanLimitSelectionToSpecificPlanNames() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "source-gmail",
            DisplayName = "Source Gmail",
            Kind = MailProfileKind.Gmail
        });
        await profileStore.SaveAsync(new MailProfile {
            Id = "target-gmail",
            DisplayName = "Target Gmail",
            Kind = MailProfileKind.Gmail
        });
        var batchStore = new FileMailMessageActionPlanBatchStore(CreateTemporaryFilePath("action-plan-batches.json"));
        var exchange = new JsonMailMessageActionPlanExchangeService();
        var planService = new RecordingPlanService();
        var service = new MailMessageActionPlanRegistryService(
            batchStore,
            exchange,
            new FakePreviewService(),
            planService,
            new MailMessageActionBatchService(planService),
            profileStore);

        await service.SaveAsync(new MailMessageActionPlanBatch {
            Id = "cleanup",
            Name = "Cleanup",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Name = "Read invoices",
                    Summary = "Read invoices (1 message)",
                    Action = "mark-read",
                    ExecutionKind = "SetReadState",
                    ProfileId = "source-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    DesiredState = true,
                    MessageIds = { "msg-1" }
                },
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Name = "Archive newsletter",
                    Summary = "Archive newsletter (1 message)",
                    Action = "move",
                    ExecutionKind = "Move",
                    ProfileId = "source-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-2" },
                    RequestedDestinationFolderId = "Archive"
                },
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Name = "Delete spam",
                    Summary = "Delete spam (1 message)",
                    Action = "delete",
                    ExecutionKind = "Delete",
                    ProfileId = "source-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    MessageIds = { "msg-3" }
                }
            }
        });

        var preview = await service.PreviewTransformCloneAsync(
            "cleanup",
            new MessageActionPlanBatchTransformRequest {
                PlanNames = { "Archive newsletter", "Delete spam" },
                ProfileId = "target-gmail"
            });
        var result = await service.TransformCloneAsync(
            "cleanup",
            "cleanup-named",
            "Cleanup Named",
            new MessageActionPlanBatchTransformRequest {
                PlanNames = { "Archive newsletter", "Delete spam" },
                ProfileId = "target-gmail"
            });
        var transformed = await service.GetBatchAsync("cleanup-named");

        Assert.True(preview.Succeeded);
        Assert.Equal(2, preview.PlanCount);
        Assert.Equal(new[] { "Archive newsletter", "Delete spam" }, preview.Plans.Select(plan => plan.Action == "move" ? "Archive newsletter" : "Delete spam"));
        Assert.True(result.Succeeded);
        Assert.NotNull(transformed);
        Assert.Equal(2, transformed!.Plans.Count);
        Assert.Equal(new[] { "Archive newsletter", "Delete spam" }, transformed.Plans.Select(plan => plan.Name));
    }

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
                    UniqueMessageCount = request.MessageIds.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
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
                UniqueMessageCount = request.MessageIds.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                MessageIds = request.MessageIds.ToList(),
                RequestedDestinationFolderId = request.DestinationFolderId,
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
