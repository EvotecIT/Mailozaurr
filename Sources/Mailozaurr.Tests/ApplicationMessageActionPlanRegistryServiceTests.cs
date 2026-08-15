using Mailozaurr.Hosting;

namespace Mailozaurr.Tests;

public sealed partial class ApplicationMessageActionPlanRegistryServiceTests {
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


}