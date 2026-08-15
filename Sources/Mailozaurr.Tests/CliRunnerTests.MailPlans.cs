#if NET8_0_OR_GREATER
using System.Text.Json;
using Mailozaurr;
using Mailozaurr.Cli;

namespace Mailozaurr.Tests;

public sealed partial class CliRunnerTests {
    [Fact]
    public async Task MailPlanActionUsesSharedPlanningService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "plan-action",
                "--action", "move",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--folder", "Inbox",
                "--message-id", "msg-42",
                "--message-id", "MSG-42",
                "--target-folder", "projects/2026",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"ExecutionKind\": \"Move\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"UniqueMessageCount\": 2", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"RequestedDestinationFolderId\": \"projects/2026\"", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"ConfirmationToken\":", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailExecutePlanUsesSharedPlanningAndBatchServices() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        var confirmationToken = MessageActionConfirmationTokens.CreateMoveToken(
            "work-imap",
            "shared@example.com",
            "Inbox",
            new[] { "msg-42", "MSG-42" },
            "projects/2026");

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "execute-plan",
                "--action", "move",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--folder", "Inbox",
                "--message-id", "msg-42",
                "--message-id", "MSG-42",
                "--target-folder", "projects/2026",
                "--confirm-token", confirmationToken,
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.MessageActionService.LastMoveRequest);
        Assert.Equal("shared@example.com", fixture.MessageActionService.LastMoveRequest!.MailboxId);
        Assert.Equal("Inbox", fixture.MessageActionService.LastMoveRequest.FolderId);
        Assert.Equal("projects/2026", fixture.MessageActionService.LastMoveRequest.DestinationFolderId);
        Assert.Contains("\"RequestedPlanCount\": 1", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"SucceededPlanCount\": 1", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailExportPlanUsesSharedPlanExchangeService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "export-plan",
                "--action", "move",
                "--profile", "work-imap",
                "--message-id", "msg-42",
                "--target-folder", "projects/2026",
                "--path", @"C:\Temp\plan.json",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal(@"C:\Temp\plan.json", fixture.MessageActionPlanExchangeService.LastSavedPath);
        Assert.NotNull(fixture.MessageActionPlanExchangeService.LastSavedPlan);
        Assert.Equal("move", fixture.MessageActionPlanExchangeService.LastSavedPlan!.Action);
        Assert.Contains("Action plan exported", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailShowPlanUsesSharedPlanExchangeService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        fixture.MessageActionPlanExchangeService.NextPlan = new MessageActionExecutionPlan {
            Succeeded = true,
            Action = "delete",
            ExecutionKind = "Delete",
            ProfileId = "work-imap",
            RequestedCount = 1,
            UniqueMessageCount = 1,
            MessageIds = { "msg-42" }
        };

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "show-plan", "--path", @"C:\Temp\plan.json", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal(@"C:\Temp\plan.json", fixture.MessageActionPlanExchangeService.LastLoadedPath);
        Assert.Contains("\"Action\": \"delete\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailExecutePlanFileUsesSharedPlanExchangeService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        fixture.MessageActionPlanExchangeService.NextPlan = new MessageActionExecutionPlan {
            Succeeded = true,
            Action = "move",
            ExecutionKind = "Move",
            ProfileId = "work-imap",
            MailboxId = "shared@example.com",
            FolderId = "Inbox",
            RequestedCount = 1,
            UniqueMessageCount = 1,
            RequestedDestinationFolderId = "projects/2026",
            ConfirmationToken = MessageActionConfirmationTokens.CreateMoveToken("work-imap", "shared@example.com", "Inbox", new[] { "msg-42" }, "projects/2026"),
            ConfirmationProvided = true,
            ConfirmationValidated = true,
            MessageIds = { "msg-42" }
        };

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "execute-plan-file", "--path", @"C:\Temp\plan.json", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal(@"C:\Temp\plan.json", fixture.MessageActionPlanExchangeService.LastLoadedPath);
        Assert.NotNull(fixture.MessageActionService.LastMoveRequest);
        Assert.Equal("projects/2026", fixture.MessageActionService.LastMoveRequest!.DestinationFolderId);
        Assert.Contains("\"SucceededPlanCount\": 1", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailListPlanBatchesUsesSharedRegistryService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "list-plan-batches", "--compact", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal(1, fixture.MessageActionPlanRegistryService.ListCompactCalls);
        Assert.Contains("\"Id\": \"cleanup\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"PlanNames\": [", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"Delete spam\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailListPlanBatchesSummaryUsesSharedRegistryService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "list-plan-batches", "--summary", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal(1, fixture.MessageActionPlanRegistryService.ListSummaryCalls);
        Assert.Contains("\"ActionCounts\": {", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"delete\": 1", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailListPlanBatchesPassesPlanNameFilterToSharedRegistryService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "list-plan-batches", "--compact", "--plan-name", "Delete spam", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.MessageActionPlanRegistryService.LastBatchQuery);
        Assert.Equal(new[] { "Delete spam" }, fixture.MessageActionPlanRegistryService.LastBatchQuery!.PlanNames);
    }

    [Fact]
    public async Task MailListPlanBatchesPassesProfileFilterToSharedRegistryService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "list-plan-batches", "--compact", "--profile", "gmail-work", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.MessageActionPlanRegistryService.LastBatchQuery);
        Assert.Equal(new[] { "gmail-work" }, fixture.MessageActionPlanRegistryService.LastBatchQuery!.ProfileIds);
    }

    [Fact]
    public async Task MailListPlanBatchesPassesActionFilterToSharedRegistryService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "list-plan-batches", "--compact", "--action", "delete", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.MessageActionPlanRegistryService.LastBatchQuery);
        Assert.Equal(new[] { "delete" }, fixture.MessageActionPlanRegistryService.LastBatchQuery!.Actions);
    }

    [Fact]
    public async Task MailListPlanBatchesPassesSortToSharedRegistryService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "list-plan-batches", "--summary", "--sort", "plans", "--desc", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.MessageActionPlanRegistryService.LastBatchQuery);
        Assert.Equal(MailMessageActionPlanBatchSortBy.PlanCount, fixture.MessageActionPlanRegistryService.LastBatchQuery!.SortBy);
        Assert.True(fixture.MessageActionPlanRegistryService.LastBatchQuery.Descending);
    }

    [Fact]
    public async Task MailListPlanBatchesPassesExplicitIdSortToSharedRegistryService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "list-plan-batches", "--summary", "--sort", "id", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.MessageActionPlanRegistryService.LastBatchQuery);
        Assert.Equal(MailMessageActionPlanBatchSortBy.Id, fixture.MessageActionPlanRegistryService.LastBatchQuery!.SortBy);
        Assert.False(fixture.MessageActionPlanRegistryService.LastBatchQuery.Descending);
    }

    [Fact]
    public async Task MailImportPlanBatchUsesSharedRegistryService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "import-plan-batch",
                "--batch", "cleanup",
                "--name", "Cleanup batch",
                "--path", @"C:\Temp\plans.json",
                "--description", "Quarterly cleanup",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("cleanup", fixture.MessageActionPlanRegistryService.LastImportedBatchId);
        Assert.Equal("Cleanup batch", fixture.MessageActionPlanRegistryService.LastImportedName);
        Assert.Equal(@"C:\Temp\plans.json", fixture.MessageActionPlanRegistryService.LastImportedPath);
    }

    [Fact]
    public async Task MailCreateCommonPlanBatchUsesSharedRegistryService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "create-common-plan-batch",
                "--batch", "cleanup",
                "--name", "Cleanup batch",
                "--profile", "work-imap",
                "--message-id", "msg-1",
                "--message-id", "MSG-1",
                "--action", "archive",
                "--action", "delete",
                "--target-folder", "Archive",
                "--mailbox", "shared@example.com",
                "--folder", "Inbox",
                "--description", "Common action batch",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("cleanup", fixture.MessageActionPlanRegistryService.LastCreatedCommonBatchId);
        Assert.Equal("Cleanup batch", fixture.MessageActionPlanRegistryService.LastCreatedCommonName);
        Assert.Equal("Common action batch", fixture.MessageActionPlanRegistryService.LastCreatedCommonDescription);
        Assert.Equal(new[] { "archive", "delete" }, fixture.MessageActionPlanRegistryService.LastCreatedCommonActions);
        Assert.NotNull(fixture.MessageActionPlanRegistryService.LastCreatedCommonRequest);
        Assert.Equal("work-imap", fixture.MessageActionPlanRegistryService.LastCreatedCommonRequest!.ProfileId);
        Assert.Equal("shared@example.com", fixture.MessageActionPlanRegistryService.LastCreatedCommonRequest.MailboxId);
        Assert.Equal("Inbox", fixture.MessageActionPlanRegistryService.LastCreatedCommonRequest.FolderId);
        Assert.Equal("Archive", fixture.MessageActionPlanRegistryService.LastCreatedCommonRequest.DestinationFolderId);
        Assert.Equal(new[] { "msg-1", "MSG-1" }, fixture.MessageActionPlanRegistryService.LastCreatedCommonRequest.MessageIds);
    }

    [Fact]
    public async Task MailExecuteStoredPlanBatchUsesSharedRegistryService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "execute-plan-batch-stored", "--batch", "cleanup", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("cleanup", fixture.MessageActionPlanRegistryService.LastExecutedBatchId);
        Assert.Contains("\"SucceededPlanCount\": 1", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailExecuteStoredPlanBatchPassesConfirmationTokensToSharedRegistryService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "execute-plan-batch-stored",
                "--batch", "cleanup",
                "--confirm-token", "token-1",
                "--confirm-token", "token-2",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal(new[] { "token-1", "token-2" }, fixture.MessageActionPlanRegistryService.LastExecutionConfirmationTokens);
    }

    [Fact]
    public async Task MailAddPlanToBatchUsesSharedRegistryService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "add-plan-to-batch",
                "--batch", "cleanup",
                "--action", "move",
                "--profile", "work-imap",
                "--message-id", "msg-42",
                "--target-folder", "projects/2026",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("cleanup", fixture.MessageActionPlanRegistryService.LastAppendedBatchId);
        Assert.NotNull(fixture.MessageActionPlanRegistryService.LastAppendedPlan);
        Assert.Equal("move", fixture.MessageActionPlanRegistryService.LastAppendedPlan!.Action);
    }

    [Fact]
    public async Task MailRemovePlanFromBatchUsesSharedRegistryService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "remove-plan-from-batch", "--batch", "cleanup", "--index", "1", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("cleanup", fixture.MessageActionPlanRegistryService.LastRemovedBatchId);
        Assert.Equal(1, fixture.MessageActionPlanRegistryService.LastRemovedIndex);
    }

    [Fact]
    public async Task MailClonePlanBatchUsesSharedRegistryService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "clone-plan-batch",
                "--source-batch", "cleanup",
                "--target-batch", "cleanup-copy",
                "--name", "Cleanup copy",
                "--description", "Cloned batch",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("cleanup", fixture.MessageActionPlanRegistryService.LastClonedSourceBatchId);
        Assert.Equal("cleanup-copy", fixture.MessageActionPlanRegistryService.LastClonedTargetBatchId);
    }

    [Fact]
    public async Task MailTransformPlanBatchUsesSharedRegistryService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "transform-plan-batch",
                "--source-batch", "cleanup",
                "--target-batch", "cleanup-target",
                "--name", "Cleanup Target",
                "--index", "1",
                "--index", "2",
                "--plan-name", "Archive newsletter",
                "--target-profile", "work-imap-target",
                "--mailbox", "shared@example.com",
                "--folder", "Projects",
                "--target-folder", "Projects/Archive",
                "--description", "Remapped batch",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("cleanup", fixture.MessageActionPlanRegistryService.LastTransformedSourceBatchId);
        Assert.Equal("cleanup-target", fixture.MessageActionPlanRegistryService.LastTransformedTargetBatchId);
        Assert.Equal("Cleanup Target", fixture.MessageActionPlanRegistryService.LastTransformedName);
        Assert.Equal("Remapped batch", fixture.MessageActionPlanRegistryService.LastTransformedDescription);
        Assert.NotNull(fixture.MessageActionPlanRegistryService.LastTransformRequest);
        Assert.Equal("work-imap-target", fixture.MessageActionPlanRegistryService.LastTransformRequest!.ProfileId);
        Assert.Equal(new[] { 1, 2 }, fixture.MessageActionPlanRegistryService.LastTransformRequest.PlanIndexes);
        Assert.Equal(new[] { "Archive newsletter" }, fixture.MessageActionPlanRegistryService.LastTransformRequest.PlanNames);
        Assert.Equal("shared@example.com", fixture.MessageActionPlanRegistryService.LastTransformRequest.MailboxId);
        Assert.Equal("Projects", fixture.MessageActionPlanRegistryService.LastTransformRequest.FolderId);
        Assert.Equal("Projects/Archive", fixture.MessageActionPlanRegistryService.LastTransformRequest.DestinationFolderId);
    }

    [Fact]
    public async Task MailPreviewTransformPlanBatchUsesSharedRegistryService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "preview-transform-plan-batch",
                "--source-batch", "cleanup",
                "--index", "1",
                "--plan-name", "Archive newsletter",
                "--target-profile", "work-imap-target",
                "--mailbox", "shared@example.com",
                "--folder", "Projects",
                "--target-folder", "Projects/Archive",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("cleanup", fixture.MessageActionPlanRegistryService.LastPreviewedTransformSourceBatchId);
        Assert.NotNull(fixture.MessageActionPlanRegistryService.LastPreviewedTransformRequest);
        Assert.Equal("work-imap-target", fixture.MessageActionPlanRegistryService.LastPreviewedTransformRequest!.ProfileId);
        Assert.Equal(new[] { 1 }, fixture.MessageActionPlanRegistryService.LastPreviewedTransformRequest.PlanIndexes);
        Assert.Equal(new[] { "Archive newsletter" }, fixture.MessageActionPlanRegistryService.LastPreviewedTransformRequest.PlanNames);
        Assert.Equal("shared@example.com", fixture.MessageActionPlanRegistryService.LastPreviewedTransformRequest.MailboxId);
        Assert.Equal("Projects", fixture.MessageActionPlanRegistryService.LastPreviewedTransformRequest.FolderId);
        Assert.Equal("Projects/Archive", fixture.MessageActionPlanRegistryService.LastPreviewedTransformRequest.DestinationFolderId);
        Assert.Contains("\"ChangedPlanCount\": 1", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailReplacePlanInBatchUsesSharedRegistryService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "replace-plan-in-batch",
                "--batch", "cleanup",
                "--index", "0",
                "--action", "move",
                "--profile", "work-imap",
                "--message-id", "msg-42",
                "--target-folder", "projects/2026",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("cleanup", fixture.MessageActionPlanRegistryService.LastReplacedBatchId);
        Assert.Equal(0, fixture.MessageActionPlanRegistryService.LastReplacedIndex);
        Assert.NotNull(fixture.MessageActionPlanRegistryService.LastReplacedPlan);
        Assert.Equal("move", fixture.MessageActionPlanRegistryService.LastReplacedPlan!.Action);
    }

    [Fact]
    public async Task MailExecutePlanBatchUsesSharedBatchService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        fixture.MessageActionPlanExchangeService.NextBatchPlans = new[] {
            new MessageActionExecutionPlan {
                Succeeded = true,
                Action = "mark-read",
                ExecutionKind = "SetReadState",
                ProfileId = "work-imap",
                MailboxId = "shared@example.com",
                FolderId = "Inbox",
                RequestedCount = 1,
                UniqueMessageCount = 1,
                ConfirmationToken = MessageActionConfirmationTokens.CreateReadStateToken("work-imap", "shared@example.com", "Inbox", new[] { "msg-1" }, true),
                ConfirmationProvided = true,
                ConfirmationValidated = true,
                DesiredState = true,
                MessageIds = { "msg-1" }
            },
            new MessageActionExecutionPlan {
                Succeeded = true,
                Action = "move",
                ExecutionKind = "Move",
                ProfileId = "work-imap",
                MailboxId = "shared@example.com",
                FolderId = "Inbox",
                RequestedCount = 1,
                UniqueMessageCount = 1,
                RequestedDestinationFolderId = "projects/2026",
                ConfirmationToken = MessageActionConfirmationTokens.CreateMoveToken("work-imap", "shared@example.com", "Inbox", new[] { "msg-2" }, "projects/2026"),
                ConfirmationProvided = true,
                ConfirmationValidated = true,
                MessageIds = { "msg-2" }
            }
        };
        var path = @"C:\Temp\action-plans.json";

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "execute-plan-batch", "--path", path, "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal(path, fixture.MessageActionPlanExchangeService.LastLoadedBatchPath);
        Assert.NotNull(fixture.MessageActionService.LastSetReadStateRequest);
        Assert.NotNull(fixture.MessageActionService.LastMoveRequest);
        Assert.Equal("projects/2026", fixture.MessageActionService.LastMoveRequest!.DestinationFolderId);
        Assert.Contains("\"AttemptedPlanCount\": 2", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"SucceededPlanCount\": 2", stdout.ToString(), StringComparison.Ordinal);
    }
}
#endif
