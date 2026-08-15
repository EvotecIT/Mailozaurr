#if NET8_0_OR_GREATER
using System.Text.Json;
using Mailozaurr.Hosting;
using Mailozaurr.Cli;

namespace Mailozaurr.Tests;

public sealed partial class CliRunnerTests {
    [Fact]
    public async Task MailFoldersUsesApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "folders", "--profile", "work-imap", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Inbox\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailFoldersCompactUsesApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "folders", "--profile", "work-imap", "--compact", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastFolderCompactQuery);
        Assert.Equal("work-imap", fixture.ReadService.LastFolderCompactQuery!.ProfileId);
        Assert.Contains("\"Summary\": \"inbox Inbox\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailFolderAliasesUseSharedFolderAliasService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "folder-aliases", "--profile", "work-imap", "--mailbox", "shared@example.com", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastFolderQuery);
        Assert.Equal("work-imap", fixture.ReadService.LastFolderQuery!.ProfileId);
        Assert.Equal("shared@example.com", fixture.ReadService.LastFolderQuery.MailboxId);
        Assert.Contains("\"Alias\": \"Archive\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"IsResolved\": true", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailResolveFolderUsesSharedFolderAliasService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "resolve-folder", "--profile", "work-imap", "--mailbox", "shared@example.com", "--target-folder", "archive", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastFolderQuery);
        Assert.Equal("shared@example.com", fixture.ReadService.LastFolderQuery!.MailboxId);
        Assert.Contains("\"Alias\": \"Archive\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"EffectiveFolderId\": \"archive\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailPreviewMoveUsesSharedPreviewService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "preview-move",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--folder", "Inbox",
                "--message-id", "msg-42",
                "--message-id", "MSG-42",
                "--target-folder", "archive",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"UniqueMessageCount\": 2", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"EffectiveFolderId\": \"archive\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"ConfirmationToken\":", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailPreviewActionsUsesSharedPreviewService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "preview-actions",
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
        Assert.Contains("\"IncludedActionCount\": 4", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"SucceededActionCount\": 4", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"Action\": \"archive\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"Action\": \"move\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"EffectiveFolderId\": \"projects/2026\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"ConfirmationToken\":", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailPreviewDeleteUsesSharedPreviewService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "preview-delete",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--folder", "Inbox",
                "--message-id", "msg-42",
                "--message-id", "MSG-42",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"UniqueMessageCount\": 2", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"RequestedCount\": 2", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailSearchUsesApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "search", "--profile", "work-imap", "--query", "reports", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"msg-1\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailSearchCompactUsesApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "search", "--profile", "work-imap", "--query", "reports", "--compact", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastSearchCompactRequest);
        Assert.Equal("work-imap", fixture.ReadService.LastSearchCompactRequest!.ProfileId);
        Assert.Equal("reports", fixture.ReadService.LastSearchCompactRequest.QueryText);
        Assert.Contains("\"Summary\": \"msg-1 reports\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailGetPassesMailboxToApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "get",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--message-id", "msg-42",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastGetRequest);
        Assert.Equal("shared@example.com", fixture.ReadService.LastGetRequest!.MailboxId);
        Assert.Equal("msg-42", fixture.ReadService.LastGetRequest.MessageId);
    }

    [Fact]
    public async Task MailGetCompactUsesApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "get",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--message-id", "msg-42",
                "--compact",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastGetCompactRequest);
        Assert.Equal("shared@example.com", fixture.ReadService.LastGetCompactRequest!.MailboxId);
        Assert.Equal("msg-42", fixture.ReadService.LastGetCompactRequest.MessageId);
        Assert.Contains("\"SummaryText\": \"msg-42 Subject\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailGetManyCompactUsesApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "get-many",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--folder", "Inbox",
                "--message-id", "msg-42",
                "--message-id", "msg-84",
                "--compact",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastGetManyCompactRequest);
        Assert.Equal("shared@example.com", fixture.ReadService.LastGetManyCompactRequest!.MailboxId);
        Assert.Equal("Inbox", fixture.ReadService.LastGetManyCompactRequest.FolderId);
        Assert.Equal(new[] { "msg-42", "msg-84" }, fixture.ReadService.LastGetManyCompactRequest.MessageIds);
        Assert.Contains("\"Id\": \"msg-42\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"Id\": \"msg-84\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailMarkReadUsesApplicationMessageActionService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        var confirmationToken = "mact_v1_mark";

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "mark-read",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--folder", "Inbox",
                "--message-id", "msg-42",
                "--message-id", "msg-84",
                "--unread",
                "--confirm-token", confirmationToken,
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.MessageActionService.LastSetReadStateRequest);
        Assert.Equal("shared@example.com", fixture.MessageActionService.LastSetReadStateRequest!.MailboxId);
        Assert.Equal("Inbox", fixture.MessageActionService.LastSetReadStateRequest.FolderId);
        Assert.False(fixture.MessageActionService.LastSetReadStateRequest.IsRead);
        Assert.Equal(new[] { "msg-42", "msg-84" }, fixture.MessageActionService.LastSetReadStateRequest.MessageIds);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastSetReadStateRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailFlagUsesApplicationMessageActionService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        var confirmationToken = "mact_v1_flag";

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "flag",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--folder", "Inbox",
                "--message-id", "msg-42",
                "--message-id", "msg-84",
                "--unflag",
                "--confirm-token", confirmationToken,
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.MessageActionService.LastSetFlaggedStateRequest);
        Assert.Equal("shared@example.com", fixture.MessageActionService.LastSetFlaggedStateRequest!.MailboxId);
        Assert.Equal("Inbox", fixture.MessageActionService.LastSetFlaggedStateRequest.FolderId);
        Assert.False(fixture.MessageActionService.LastSetFlaggedStateRequest.IsFlagged);
        Assert.Equal(new[] { "msg-42", "msg-84" }, fixture.MessageActionService.LastSetFlaggedStateRequest.MessageIds);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastSetFlaggedStateRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailPreviewMarkReadUsesSharedPreviewService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "preview-mark-read",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--folder", "Inbox",
                "--message-id", "msg-42",
                "--message-id", "MSG-42",
                "--unread",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Action\": \"read-state\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"DesiredState\": false", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"ConfirmationToken\":", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailPreviewFlagUsesSharedPreviewService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "preview-flag",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--folder", "Inbox",
                "--message-id", "msg-42",
                "--message-id", "MSG-42",
                "--unflag",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Action\": \"flagged-state\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"DesiredState\": false", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"ConfirmationToken\":", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailPreviewAllUsesSharedBundlePreviewService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "preview-all",
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
        Assert.Contains("\"IncludedActionCount\": 8", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"Action\": \"mark-read\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"Action\": \"flag\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"Action\": \"archive\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"Action\": \"move\"", stdout.ToString(), StringComparison.Ordinal);
    }
}
#endif