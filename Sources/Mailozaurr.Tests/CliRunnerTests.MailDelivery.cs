#if NET8_0_OR_GREATER
using System.Text.Json;
using Mailozaurr.Hosting;
using Mailozaurr.Cli;

namespace Mailozaurr.Tests;

public sealed partial class CliRunnerTests {
    [Fact]
    public async Task MailArchiveUsesSharedArchiveAlias() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        var confirmationToken = "mact_v1_archive";

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "archive",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--folder", "Inbox",
                "--message-id", "msg-42",
                "--confirm-token", confirmationToken,
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.MessageActionService.LastMoveRequest);
        Assert.Equal(MailFolderAliases.Archive, fixture.MessageActionService.LastMoveRequest!.DestinationFolderId);
        Assert.Equal("shared@example.com", fixture.MessageActionService.LastMoveRequest.MailboxId);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastMoveRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailTrashUsesSharedTrashAlias() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        var confirmationToken = "mact_v1_trash";

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "trash",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--folder", "Inbox",
                "--message-id", "msg-42",
                "--confirm-token", confirmationToken,
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.MessageActionService.LastMoveRequest);
        Assert.Equal(MailFolderAliases.Trash, fixture.MessageActionService.LastMoveRequest!.DestinationFolderId);
        Assert.Equal("shared@example.com", fixture.MessageActionService.LastMoveRequest.MailboxId);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastMoveRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailMoveUsesApplicationMessageActionService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        var confirmationToken = "mact_v1_move";

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "move",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--folder", "Inbox",
                "--message-id", "msg-42",
                "--target-folder", "Archive",
                "--confirm-token", confirmationToken,
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.MessageActionService.LastMoveRequest);
        Assert.Equal("Archive", fixture.MessageActionService.LastMoveRequest!.DestinationFolderId);
        Assert.Equal("shared@example.com", fixture.MessageActionService.LastMoveRequest.MailboxId);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastMoveRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailDeleteUsesApplicationMessageActionService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        var confirmationToken = "mact_v1_delete";

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "delete",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--folder", "Inbox",
                "--message-id", "msg-42",
                "--message-id", "msg-84",
                "--confirm-token", confirmationToken,
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.MessageActionService.LastDeleteRequest);
        Assert.Equal("shared@example.com", fixture.MessageActionService.LastDeleteRequest!.MailboxId);
        Assert.Equal(new[] { "msg-42", "msg-84" }, fixture.MessageActionService.LastDeleteRequest.MessageIds);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastDeleteRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailAttachmentsUsesApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "attachments",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--message-id", "msg-42",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastListAttachmentsRequest);
        Assert.Equal("shared@example.com", fixture.ReadService.LastListAttachmentsRequest!.MailboxId);
        Assert.Equal("msg-42", fixture.ReadService.LastListAttachmentsRequest.MessageId);
        Assert.Contains("\"report.pdf\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailSaveAttachmentsManyUsesApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "save-attachments-many",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--folder", "Inbox",
                "--message-id", "msg-42",
                "--message-id", "msg-84",
                "--path", @"C:\Temp",
                "--attachment-id", "att-1",
                "--name-contains", "report",
                "--content-type", "pdf",
                "--overwrite",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastSaveAttachmentsManyRequest);
        Assert.Equal("shared@example.com", fixture.ReadService.LastSaveAttachmentsManyRequest!.MailboxId);
        Assert.Equal("Inbox", fixture.ReadService.LastSaveAttachmentsManyRequest.FolderId);
        Assert.Equal(@"C:\Temp", fixture.ReadService.LastSaveAttachmentsManyRequest.DestinationPath);
        Assert.Equal(new[] { "msg-42", "msg-84" }, fixture.ReadService.LastSaveAttachmentsManyRequest.MessageIds);
        Assert.Equal(new[] { "att-1" }, fixture.ReadService.LastSaveAttachmentsManyRequest.AttachmentIds);
        Assert.Equal("report", fixture.ReadService.LastSaveAttachmentsManyRequest.FileNameContains);
        Assert.Equal("pdf", fixture.ReadService.LastSaveAttachmentsManyRequest.ContentTypeContains);
        Assert.True(fixture.ReadService.LastSaveAttachmentsManyRequest.Overwrite);
        Assert.Contains("\"SavedCount\": 2", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailSaveAttachmentPassesMailboxToApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "save-attachment",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--message-id", "msg-42",
                "--attachment-id", "1",
                "--path", "C:\\Temp\\report.pdf",
                "--overwrite",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastSaveAttachmentRequest);
        Assert.Equal("shared@example.com", fixture.ReadService.LastSaveAttachmentRequest!.MailboxId);
        Assert.Equal("1", fixture.ReadService.LastSaveAttachmentRequest.AttachmentId);
        Assert.True(fixture.ReadService.LastSaveAttachmentRequest.Overwrite);
    }

    [Fact]
    public async Task MailSaveAttachmentsUsesApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "save-attachments",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--message-id", "msg-42",
                "--path", "C:\\Temp",
                "--attachment-id", "att-1",
                "--name-contains", "report",
                "--content-type", "pdf",
                "--overwrite",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastSaveAttachmentsRequest);
        Assert.Equal("shared@example.com", fixture.ReadService.LastSaveAttachmentsRequest!.MailboxId);
        Assert.Equal("msg-42", fixture.ReadService.LastSaveAttachmentsRequest.MessageId);
        Assert.Equal("C:\\Temp", fixture.ReadService.LastSaveAttachmentsRequest.DestinationPath);
        Assert.Contains("att-1", fixture.ReadService.LastSaveAttachmentsRequest.AttachmentIds);
        Assert.Equal("report", fixture.ReadService.LastSaveAttachmentsRequest.FileNameContains);
        Assert.Equal("pdf", fixture.ReadService.LastSaveAttachmentsRequest.ContentTypeContains);
        Assert.True(fixture.ReadService.LastSaveAttachmentsRequest.Overwrite);
    }
}
#endif