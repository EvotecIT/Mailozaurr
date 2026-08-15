#if NET8_0_OR_GREATER
using Mailozaurr.Hosting;
using Mailozaurr.Cli.Mcp;

namespace Mailozaurr.Tests;

public sealed partial class MailMcpToolsTests {
    [Fact]
    public async Task MailSearchDelegatesToApplicationReadService() {
        using var fixture = new TestFixture();

        var results = await fixture.Tools.mail_search(
            "gmail-work",
            mailboxId: "primary",
            folderId: "Inbox",
            queryText: "invoice",
            subjectContains: "Quarterly",
            fromContains: "billing@example.com",
            toContains: "team@example.com",
            hasAttachments: true,
            limit: 5);

        var result = Assert.Single(results);
        Assert.Equal("message-1", result.Id);
        Assert.NotNull(fixture.ReadService.LastSearchRequest);
        Assert.Equal("gmail-work", fixture.ReadService.LastSearchRequest!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastSearchRequest.MailboxId);
        Assert.Equal("Inbox", fixture.ReadService.LastSearchRequest.FolderId);
        Assert.Equal("invoice", fixture.ReadService.LastSearchRequest.QueryText);
        Assert.True(fixture.ReadService.LastSearchRequest.HasAttachments);
        Assert.Equal(5, fixture.ReadService.LastSearchRequest.Limit);
    }

    [Fact]
    public async Task MailFoldersCompactDelegatesToApplicationReadService() {
        using var fixture = new TestFixture();

        var results = await fixture.Tools.mail_folders_compact_list(
            "gmail-work",
            mailboxId: "primary",
            parentFolderId: "root",
            rootOnly: true);

        var result = Assert.Single(results);
        Assert.Equal("Inbox", result.Id);
        Assert.NotNull(fixture.ReadService.LastFolderCompactQuery);
        Assert.Equal("gmail-work", fixture.ReadService.LastFolderCompactQuery!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastFolderCompactQuery.MailboxId);
        Assert.Equal("root", fixture.ReadService.LastFolderCompactQuery.ParentFolderId);
        Assert.True(fixture.ReadService.LastFolderCompactQuery.RootOnly);
    }

    [Fact]
    public async Task MailFolderAliasesListDelegatesToSharedFolderAliasService() {
        using var fixture = new TestFixture();

        var results = await fixture.Tools.mail_folder_aliases_list("gmail-work", mailboxId: "primary");

        var archive = Assert.Single(results, result => result.Alias == MailFolderAliases.Archive);
        Assert.True(archive.IsResolved);
        Assert.Equal("archive", archive.FolderId);
        Assert.NotNull(fixture.ReadService.LastFolderQuery);
        Assert.Equal("gmail-work", fixture.ReadService.LastFolderQuery!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastFolderQuery.MailboxId);
    }

    [Fact]
    public async Task MailFolderResolveDelegatesToSharedFolderAliasService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_folder_resolve("gmail-work", "archive", mailboxId: "primary");

        Assert.True(result.IsAlias);
        Assert.Equal(MailFolderAliases.Archive, result.Alias);
        Assert.Equal("archive", result.EffectiveFolderId);
        Assert.NotNull(fixture.ReadService.LastFolderQuery);
        Assert.Equal("gmail-work", fixture.ReadService.LastFolderQuery!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastFolderQuery.MailboxId);
    }

    [Fact]
    public async Task MailMovePreviewUsesSharedPreviewService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_move_preview(
            "gmail-work",
            new[] { "message-1", "MESSAGE-1" },
            "archive",
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.RequestedCount);
        Assert.Equal(2, result.UniqueMessageCount);
        Assert.NotNull(result.Destination);
        Assert.Equal("archive", result.Destination!.EffectiveFolderId);
        Assert.NotNull(result.ConfirmationToken);
    }

    [Fact]
    public async Task MailActionsPreviewUsesSharedPreviewService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_actions_preview(
            "gmail-work",
            new[] { "message-1", "MESSAGE-1" },
            destinationFolderId: "Projects/2026",
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal(4, result.IncludedActionCount);
        Assert.Equal(4, result.SucceededActionCount);
        Assert.Equal(0, result.FailedActionCount);
        Assert.Equal(2, result.UniqueMessageCount);
        Assert.Contains(result.Actions, action => action.Action == "archive" && action.Destination!.EffectiveFolderId == "archive");
        Assert.Contains(result.Actions, action => action.Action == "move" && action.Destination!.EffectiveFolderId == "Projects/2026");
        Assert.Contains(result.Actions, action => action.Action == "delete" && action.Succeeded);
        Assert.All(result.Actions, action => Assert.NotNull(action.ConfirmationToken));
    }

    [Fact]
    public async Task MailActionsBundlePreviewUsesSharedPreviewService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_actions_bundle_preview(
            "gmail-work",
            new[] { "message-1", "MESSAGE-1" },
            destinationFolderId: "Projects/2026",
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal(8, result.IncludedActionCount);
        Assert.Equal(8, result.SucceededActionCount);
        Assert.Equal(2, result.UniqueMessageCount);
        Assert.Contains(result.Actions, action => action.Action == "mark-read" && action.DesiredState == true);
        Assert.Contains(result.Actions, action => action.Action == "mark-unread" && action.DesiredState == false);
        Assert.Contains(result.Actions, action => action.Action == "flag" && action.DesiredState == true);
        Assert.Contains(result.Actions, action => action.Action == "unflag" && action.DesiredState == false);
        Assert.Contains(result.Actions, action => action.Action == "move" && action.Destination!.EffectiveFolderId == "Projects/2026");
    }

    [Fact]
    public async Task MailActionPlanUsesSharedPlanningService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_plan(
            action: "move",
            profileId: "gmail-work",
            messageIds: new[] { "message-1", "MESSAGE-1" },
            destinationFolderId: "Projects/2026",
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal("Move", result.ExecutionKind);
        Assert.Equal(2, result.UniqueMessageCount);
        Assert.Equal("Projects/2026", result.RequestedDestinationFolderId);
        Assert.NotNull(result.ConfirmationToken);
    }

    [Fact]
    public async Task MailActionPlanExportUsesSharedPlanExchangeService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_plan_export(
            action: "move",
            profileId: "gmail-work",
            messageIds: new[] { "message-1", "MESSAGE-1" },
            path: @"C:\Temp\plan.json",
            destinationFolderId: "Projects/2026",
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal(@"C:\Temp\plan.json", fixture.PlanExchangeService.LastSavedPath);
        Assert.NotNull(fixture.PlanExchangeService.LastSavedPlan);
        Assert.Equal("move", fixture.PlanExchangeService.LastSavedPlan!.Action);
    }

    [Fact]
    public async Task MailActionPlanImportUsesSharedPlanExchangeService() {
        using var fixture = new TestFixture();
        fixture.PlanExchangeService.NextPlan = new MessageActionExecutionPlan {
            Succeeded = true,
            Action = "delete",
            ExecutionKind = "Delete",
            ProfileId = "gmail-work",
            RequestedCount = 1,
            UniqueMessageCount = 1,
            MessageIds = { "message-1" }
        };

        var result = await fixture.Tools.mail_action_plan_import(@"C:\Temp\plan.json");

        Assert.Equal("delete", result.Action);
        Assert.Equal(@"C:\Temp\plan.json", fixture.PlanExchangeService.LastLoadedPath);
    }

    [Fact]
    public async Task MailActionBatchImportUsesSharedPlanExchangeService() {
        using var fixture = new TestFixture();
        fixture.PlanExchangeService.NextBatchPlans = new[] {
            new MessageActionExecutionPlan {
                Succeeded = true,
                Action = "mark-read",
                ExecutionKind = "SetReadState",
                ProfileId = "gmail-work",
                RequestedCount = 1,
                UniqueMessageCount = 1,
                DesiredState = true,
                MessageIds = { "message-1" }
            }
        };

        var result = await fixture.Tools.mail_action_batch_import(@"C:\Temp\plans.json");

        Assert.Single(result);
        Assert.Equal("mark-read", result[0].Action);
        Assert.Equal(@"C:\Temp\plans.json", fixture.PlanExchangeService.LastLoadedBatchPath);
    }

    [Fact]
    public async Task MailActionBatchStoreListUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_compact_list();

        var batch = Assert.Single(result);
        Assert.Equal("cleanup", batch.Id);
        Assert.Equal(new[] { "Delete spam" }, batch.PlanNames);
        Assert.Equal(1, fixture.PlanRegistryService.ListCompactCalls);
    }

    [Fact]
    public async Task MailActionBatchStoreSummaryListUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_summary_list();

        var batch = Assert.Single(result);
        Assert.Equal("cleanup", batch.Id);
        Assert.Equal(1, batch.ActionCounts["delete"]);
        Assert.Equal(new[] { "gmail-work" }, batch.ProfileIds);
        Assert.Equal(1, fixture.PlanRegistryService.ListSummaryCalls);
    }

    [Fact]
    public async Task MailActionBatchStoreSummaryListPassesSortToSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_summary_list(sortBy: "plans", descending: true);

        var batch = Assert.Single(result);
        Assert.Equal("cleanup", batch.Id);
        Assert.NotNull(fixture.PlanRegistryService.LastBatchQuery);
        Assert.Equal(MailMessageActionPlanBatchSortBy.PlanCount, fixture.PlanRegistryService.LastBatchQuery!.SortBy);
        Assert.True(fixture.PlanRegistryService.LastBatchQuery.Descending);
    }

    [Fact]
    public async Task MailActionBatchStoreSummaryListPassesExplicitIdSortToSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_summary_list(sortBy: "id");

        var batch = Assert.Single(result);
        Assert.Equal("cleanup", batch.Id);
        Assert.NotNull(fixture.PlanRegistryService.LastBatchQuery);
        Assert.Equal(MailMessageActionPlanBatchSortBy.Id, fixture.PlanRegistryService.LastBatchQuery!.SortBy);
        Assert.False(fixture.PlanRegistryService.LastBatchQuery.Descending);
    }

    [Fact]
    public async Task MailActionBatchStoreListPassesPlanNameFilterToSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_compact_list(new[] { "Delete spam" });

        var batch = Assert.Single(result);
        Assert.Equal("cleanup", batch.Id);
        Assert.NotNull(fixture.PlanRegistryService.LastBatchQuery);
        Assert.Equal(new[] { "Delete spam" }, fixture.PlanRegistryService.LastBatchQuery!.PlanNames);
    }

    [Fact]
    public async Task MailActionBatchStoreListPassesProfileFilterToSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_compact_list(profileIds: new[] { "gmail-work" });

        var batch = Assert.Single(result);
        Assert.Equal("cleanup", batch.Id);
        Assert.NotNull(fixture.PlanRegistryService.LastBatchQuery);
        Assert.Equal(new[] { "gmail-work" }, fixture.PlanRegistryService.LastBatchQuery!.ProfileIds);
    }

    [Fact]
    public async Task MailActionBatchStoreListPassesActionFilterToSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_compact_list(actions: new[] { "delete" });

        var batch = Assert.Single(result);
        Assert.Equal("cleanup", batch.Id);
        Assert.NotNull(fixture.PlanRegistryService.LastBatchQuery);
        Assert.Equal(new[] { "delete" }, fixture.PlanRegistryService.LastBatchQuery!.Actions);
    }

    [Fact]
    public async Task MailActionBatchStoreImportUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_import("cleanup", "Cleanup batch", @"C:\Temp\plans.json", "Quarterly cleanup");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastImportedBatchId);
        Assert.Equal(@"C:\Temp\plans.json", fixture.PlanRegistryService.LastImportedPath);
    }

    [Fact]
    public async Task MailActionBatchStoreCreateCommonUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_create_common(
            batchId: "cleanup",
            name: "Cleanup batch",
            profileId: "gmail-work",
            messageIds: new[] { "message-1", "MESSAGE-1" },
            actions: new[] { "archive", "delete" },
            destinationFolderId: "Archive",
            mailboxId: "primary",
            folderId: "Inbox",
            description: "Common action batch");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastCreatedCommonBatchId);
        Assert.Equal("Cleanup batch", fixture.PlanRegistryService.LastCreatedCommonName);
        Assert.Equal("Common action batch", fixture.PlanRegistryService.LastCreatedCommonDescription);
        Assert.Equal(new[] { "archive", "delete" }, fixture.PlanRegistryService.LastCreatedCommonActions);
        Assert.NotNull(fixture.PlanRegistryService.LastCreatedCommonRequest);
        Assert.Equal("gmail-work", fixture.PlanRegistryService.LastCreatedCommonRequest!.ProfileId);
        Assert.Equal("primary", fixture.PlanRegistryService.LastCreatedCommonRequest.MailboxId);
        Assert.Equal("Inbox", fixture.PlanRegistryService.LastCreatedCommonRequest.FolderId);
        Assert.Equal("Archive", fixture.PlanRegistryService.LastCreatedCommonRequest.DestinationFolderId);
        Assert.Equal(new[] { "message-1", "MESSAGE-1" }, fixture.PlanRegistryService.LastCreatedCommonRequest.MessageIds);
    }

    [Fact]
    public async Task MailActionBatchStoreCreateFromPreviewUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var preview = await fixture.Tools.mail_actions_bundle_preview(
            profileId: "gmail-work",
            messageIds: new[] { "message-1", "MESSAGE-1" },
            destinationFolderId: "Projects/2026",
            mailboxId: "primary",
            folderId: "Inbox");
        var result = await fixture.Tools.mail_action_batch_store_create_from_preview(
            batchId: "cleanup-previewed",
            name: "Cleanup Previewed",
            preview: preview,
            actions: new[] { "move", "delete" },
            description: "Built from preview");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup-previewed", fixture.PlanRegistryService.LastCreatedFromPreviewBatchId);
        Assert.Equal("Cleanup Previewed", fixture.PlanRegistryService.LastCreatedFromPreviewName);
        Assert.Equal("Built from preview", fixture.PlanRegistryService.LastCreatedFromPreviewDescription);
        Assert.Equal(new[] { "move", "delete" }, fixture.PlanRegistryService.LastCreatedFromPreviewActions);
        Assert.NotNull(fixture.PlanRegistryService.LastCreatedFromPreview);
        Assert.Equal("gmail-work", fixture.PlanRegistryService.LastCreatedFromPreview!.ProfileId);
        Assert.Equal("primary", fixture.PlanRegistryService.LastCreatedFromPreview.MailboxId);
        Assert.Equal("Inbox", fixture.PlanRegistryService.LastCreatedFromPreview.FolderId);
        Assert.Equal("Projects/2026", fixture.PlanRegistryService.LastCreatedFromPreview.RequestedDestinationFolderId);
        Assert.Equal(new[] { "message-1", "MESSAGE-1" }, fixture.PlanRegistryService.LastCreatedFromPreview.MessageIds);
        Assert.Contains(fixture.PlanRegistryService.LastCreatedFromPreview.Actions, action => action.Action == "move");
    }

    [Fact]
    public async Task MailActionBatchStoreExecuteUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_execute("cleanup");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastExecutedBatchId);
        Assert.Equal(1, result.SucceededPlanCount);
    }

    [Fact]
    public async Task MailActionBatchStoreExecutePassesConfirmationTokensToSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_execute(
            "cleanup",
            confirmationTokens: new[] { "token-1", "token-2" });

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "token-1", "token-2" }, fixture.PlanRegistryService.LastExecutionConfirmationTokens);
    }

    [Fact]
    public async Task MailActionBatchStoreAppendPlanUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_append_plan(
            batchId: "cleanup",
            action: "move",
            profileId: "gmail-work",
            messageIds: new[] { "message-1" },
            destinationFolderId: "Archive",
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastAppendedBatchId);
        Assert.NotNull(fixture.PlanRegistryService.LastAppendedPlan);
        Assert.Equal("move", fixture.PlanRegistryService.LastAppendedPlan!.Action);
    }

    [Fact]
    public async Task MailActionBatchStoreRemovePlanUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_remove_plan("cleanup", 1);

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastRemovedBatchId);
        Assert.Equal(1, fixture.PlanRegistryService.LastRemovedIndex);
    }

    [Fact]
    public async Task MailActionBatchStoreCloneUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_clone("cleanup", "cleanup-copy", "Cleanup copy", "Cloned batch");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastClonedSourceBatchId);
        Assert.Equal("cleanup-copy", fixture.PlanRegistryService.LastClonedTargetBatchId);
    }

    [Fact]
    public async Task MailActionBatchStoreTransformCloneUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_transform_clone(
            sourceBatchId: "cleanup",
            targetBatchId: "cleanup-target",
            name: "Cleanup Target",
            indexes: new[] { 1, 2 },
            planNames: new[] { "Archive newsletter" },
            profileId: "gmail-target",
            mailboxId: "shared@example.com",
            folderId: "Projects",
            destinationFolderId: "Projects/Archive",
            description: "Remapped batch");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastTransformedSourceBatchId);
        Assert.Equal("cleanup-target", fixture.PlanRegistryService.LastTransformedTargetBatchId);
        Assert.Equal("Cleanup Target", fixture.PlanRegistryService.LastTransformedName);
        Assert.Equal("Remapped batch", fixture.PlanRegistryService.LastTransformedDescription);
        Assert.NotNull(fixture.PlanRegistryService.LastTransformRequest);
        Assert.Equal("gmail-target", fixture.PlanRegistryService.LastTransformRequest!.ProfileId);
        Assert.Equal(new[] { 1, 2 }, fixture.PlanRegistryService.LastTransformRequest.PlanIndexes);
        Assert.Equal(new[] { "Archive newsletter" }, fixture.PlanRegistryService.LastTransformRequest.PlanNames);
        Assert.Equal("shared@example.com", fixture.PlanRegistryService.LastTransformRequest.MailboxId);
        Assert.Equal("Projects", fixture.PlanRegistryService.LastTransformRequest.FolderId);
        Assert.Equal("Projects/Archive", fixture.PlanRegistryService.LastTransformRequest.DestinationFolderId);
    }

    [Fact]
    public async Task MailActionBatchStoreTransformPreviewUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_transform_preview(
            sourceBatchId: "cleanup",
            indexes: new[] { 1 },
            planNames: new[] { "Archive newsletter" },
            profileId: "gmail-target",
            mailboxId: "shared@example.com",
            folderId: "Projects",
            destinationFolderId: "Projects/Archive");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastPreviewedTransformSourceBatchId);
        Assert.NotNull(fixture.PlanRegistryService.LastPreviewedTransformRequest);
        Assert.Equal("gmail-target", fixture.PlanRegistryService.LastPreviewedTransformRequest!.ProfileId);
        Assert.Equal(new[] { 1 }, fixture.PlanRegistryService.LastPreviewedTransformRequest.PlanIndexes);
        Assert.Equal(new[] { "Archive newsletter" }, fixture.PlanRegistryService.LastPreviewedTransformRequest.PlanNames);
        Assert.Equal("shared@example.com", fixture.PlanRegistryService.LastPreviewedTransformRequest.MailboxId);
        Assert.Equal("Projects", fixture.PlanRegistryService.LastPreviewedTransformRequest.FolderId);
        Assert.Equal("Projects/Archive", fixture.PlanRegistryService.LastPreviewedTransformRequest.DestinationFolderId);
        Assert.Equal(1, result.ChangedPlanCount);
    }

    [Fact]
    public async Task MailActionBatchStoreReplacePlanUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_replace_plan(
            batchId: "cleanup",
            index: 0,
            action: "move",
            profileId: "gmail-work",
            messageIds: new[] { "message-1" },
            destinationFolderId: "Archive",
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastReplacedBatchId);
        Assert.Equal(0, fixture.PlanRegistryService.LastReplacedIndex);
        Assert.NotNull(fixture.PlanRegistryService.LastReplacedPlan);
        Assert.Equal("move", fixture.PlanRegistryService.LastReplacedPlan!.Action);
    }

    [Fact]
    public async Task MailActionExecuteUsesSharedBatchService() {
        using var fixture = new TestFixture();
        var confirmationToken = MessageActionConfirmationTokens.CreateMoveToken(
            "gmail-work",
            "primary",
            "Inbox",
            new[] { "message-1", "MESSAGE-1" },
            "Projects/2026");

        var result = await fixture.Tools.mail_action_execute(
            action: "move",
            profileId: "gmail-work",
            messageIds: new[] { "message-1", "MESSAGE-1" },
            destinationFolderId: "Projects/2026",
            mailboxId: "primary",
            folderId: "Inbox",
            confirmationToken: confirmationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.RequestedPlanCount);
        Assert.Equal(1, result.SucceededPlanCount);
        Assert.NotNull(fixture.MessageActionService.LastMoveRequest);
        Assert.Equal("primary", fixture.MessageActionService.LastMoveRequest!.MailboxId);
        Assert.Equal("Projects/2026", fixture.MessageActionService.LastMoveRequest.DestinationFolderId);
    }

    [Fact]
    public async Task MailActionBatchExecuteUsesSharedBatchService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_execute(new[] {
            new MessageActionExecutionPlan {
                Succeeded = true,
                Action = "mark-read",
                ExecutionKind = "SetReadState",
                ProfileId = "gmail-work",
                MailboxId = "primary",
                FolderId = "Inbox",
                RequestedCount = 1,
                UniqueMessageCount = 1,
                DesiredState = true,
                ConfirmationToken = MessageActionConfirmationTokens.CreateReadStateToken("gmail-work", "primary", "Inbox", new[] { "message-1" }, true),
                ConfirmationProvided = true,
                ConfirmationValidated = true,
                MessageIds = { "message-1" }
            },
            new MessageActionExecutionPlan {
                Succeeded = true,
                Action = "move",
                ExecutionKind = "Move",
                ProfileId = "gmail-work",
                MailboxId = "primary",
                FolderId = "Inbox",
                RequestedCount = 1,
                UniqueMessageCount = 1,
                RequestedDestinationFolderId = "Projects/2026",
                ConfirmationToken = MessageActionConfirmationTokens.CreateMoveToken("gmail-work", "primary", "Inbox", new[] { "message-2" }, "Projects/2026"),
                ConfirmationProvided = true,
                ConfirmationValidated = true,
                MessageIds = { "message-2" }
            }
        });

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.AttemptedPlanCount);
        Assert.Equal(2, result.SucceededPlanCount);
        Assert.NotNull(fixture.MessageActionService.LastSetReadStateRequest);
        Assert.NotNull(fixture.MessageActionService.LastMoveRequest);
        Assert.Equal("Projects/2026", fixture.MessageActionService.LastMoveRequest!.DestinationFolderId);
    }

    [Fact]
    public async Task MailDeletePreviewUsesSharedPreviewService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_delete_preview(
            "gmail-work",
            new[] { "message-1", "MESSAGE-1" },
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.RequestedCount);
        Assert.Equal(2, result.UniqueMessageCount);
        Assert.Equal("gmail-work", result.ProfileId);
        Assert.NotNull(result.ConfirmationToken);
    }
}
#endif
