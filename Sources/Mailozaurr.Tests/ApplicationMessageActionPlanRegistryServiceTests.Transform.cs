using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed partial class ApplicationMessageActionPlanRegistryServiceTests {
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
        Assert.Contains(preview.Errors, error => error.IndexOf("missing-profile", StringComparison.Ordinal) >= 0);
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
}