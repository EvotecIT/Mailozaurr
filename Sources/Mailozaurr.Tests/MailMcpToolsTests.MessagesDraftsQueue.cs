#if NET8_0_OR_GREATER
using Mailozaurr.Application;
using Mailozaurr.Cli.Mcp;

namespace Mailozaurr.Tests;

public sealed partial class MailMcpToolsTests {
    [Fact]
    public async Task MailSearchCompactDelegatesToApplicationReadService() {
        using var fixture = new TestFixture();

        var results = await fixture.Tools.mail_search_compact(
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
        Assert.NotNull(fixture.ReadService.LastSearchCompactRequest);
        Assert.Equal("gmail-work", fixture.ReadService.LastSearchCompactRequest!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastSearchCompactRequest.MailboxId);
        Assert.Equal("Inbox", fixture.ReadService.LastSearchCompactRequest.FolderId);
        Assert.Equal("invoice", fixture.ReadService.LastSearchCompactRequest.QueryText);
        Assert.True(fixture.ReadService.LastSearchCompactRequest.HasAttachments);
        Assert.Equal(5, fixture.ReadService.LastSearchCompactRequest.Limit);
    }

    [Fact]
    public async Task MailAttachmentsListDelegatesToApplicationReadService() {
        using var fixture = new TestFixture();

        var results = await fixture.Tools.mail_attachments_list(
            "gmail-work",
            "message-1",
            mailboxId: "primary",
            folderId: "Inbox");

        var result = Assert.Single(results);
        Assert.Equal("attachment-1", result.Id);
        Assert.NotNull(fixture.ReadService.LastListAttachmentsRequest);
        Assert.Equal("gmail-work", fixture.ReadService.LastListAttachmentsRequest!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastListAttachmentsRequest.MailboxId);
        Assert.Equal("Inbox", fixture.ReadService.LastListAttachmentsRequest.FolderId);
        Assert.Equal("message-1", fixture.ReadService.LastListAttachmentsRequest.MessageId);
    }

    [Fact]
    public async Task MailAttachmentsSaveDelegatesToApplicationReadService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_attachments_save(
            "gmail-work",
            "message-1",
            @"C:\Temp",
            mailboxId: "primary",
            folderId: "Inbox",
            attachmentIds: new[] { "attachment-1" },
            fileNameContains: "invoice",
            contentTypeContains: "pdf",
            overwrite: true);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.ReadService.LastSaveAttachmentsRequest);
        Assert.Equal("gmail-work", fixture.ReadService.LastSaveAttachmentsRequest!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastSaveAttachmentsRequest.MailboxId);
        Assert.Equal("Inbox", fixture.ReadService.LastSaveAttachmentsRequest.FolderId);
        Assert.Equal(@"C:\Temp", fixture.ReadService.LastSaveAttachmentsRequest.DestinationPath);
        Assert.Contains("attachment-1", fixture.ReadService.LastSaveAttachmentsRequest.AttachmentIds);
        Assert.Equal("invoice", fixture.ReadService.LastSaveAttachmentsRequest.FileNameContains);
        Assert.Equal("pdf", fixture.ReadService.LastSaveAttachmentsRequest.ContentTypeContains);
        Assert.True(fixture.ReadService.LastSaveAttachmentsRequest.Overwrite);
    }

    [Fact]
    public async Task MailAttachmentsSaveManyDelegatesToApplicationReadService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_attachments_save_many(
            "gmail-work",
            new[] { "message-1", "message-2" },
            @"C:\Temp",
            mailboxId: "primary",
            folderId: "Inbox",
            attachmentIds: new[] { "attachment-1" },
            fileNameContains: "invoice",
            contentTypeContains: "pdf",
            overwrite: true);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.ReadService.LastSaveAttachmentsManyRequest);
        Assert.Equal("gmail-work", fixture.ReadService.LastSaveAttachmentsManyRequest!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastSaveAttachmentsManyRequest.MailboxId);
        Assert.Equal("Inbox", fixture.ReadService.LastSaveAttachmentsManyRequest.FolderId);
        Assert.Equal(@"C:\Temp", fixture.ReadService.LastSaveAttachmentsManyRequest.DestinationPath);
        Assert.Equal(new[] { "message-1", "message-2" }, fixture.ReadService.LastSaveAttachmentsManyRequest.MessageIds);
        Assert.Equal(new[] { "attachment-1" }, fixture.ReadService.LastSaveAttachmentsManyRequest.AttachmentIds);
        Assert.Equal("invoice", fixture.ReadService.LastSaveAttachmentsManyRequest.FileNameContains);
        Assert.Equal("pdf", fixture.ReadService.LastSaveAttachmentsManyRequest.ContentTypeContains);
        Assert.True(fixture.ReadService.LastSaveAttachmentsManyRequest.Overwrite);
    }

    [Fact]
    public async Task MailGetCompactDelegatesToApplicationReadService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_get_compact(
            "gmail-work",
            "message-1",
            mailboxId: "primary",
            folderId: "Inbox",
            includeRawContent: true);

        Assert.Equal("message-1", result.Id);
        Assert.NotNull(fixture.ReadService.LastGetCompactRequest);
        Assert.Equal("gmail-work", fixture.ReadService.LastGetCompactRequest!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastGetCompactRequest.MailboxId);
        Assert.Equal("Inbox", fixture.ReadService.LastGetCompactRequest.FolderId);
        Assert.Equal("message-1", fixture.ReadService.LastGetCompactRequest.MessageId);
        Assert.True(fixture.ReadService.LastGetCompactRequest.IncludeRawContent);
    }

    [Fact]
    public async Task MailGetManyCompactDelegatesToApplicationReadService() {
        using var fixture = new TestFixture();

        var results = await fixture.Tools.mail_get_many_compact(
            "gmail-work",
            new[] { "message-1", "message-2" },
            mailboxId: "primary",
            folderId: "Inbox",
            includeRawContent: true);

        Assert.Equal(2, results.Count);
        Assert.NotNull(fixture.ReadService.LastGetManyCompactRequest);
        Assert.Equal("gmail-work", fixture.ReadService.LastGetManyCompactRequest!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastGetManyCompactRequest.MailboxId);
        Assert.Equal("Inbox", fixture.ReadService.LastGetManyCompactRequest.FolderId);
        Assert.Equal(new[] { "message-1", "message-2" }, fixture.ReadService.LastGetManyCompactRequest.MessageIds);
        Assert.True(fixture.ReadService.LastGetManyCompactRequest.IncludeRawContent);
    }

    [Fact]
    public async Task MailMarkReadDelegatesToApplicationMessageActionService() {
        using var fixture = new TestFixture();
        const string confirmationToken = "mact_v1_mark";

        var result = await fixture.Tools.mail_mark_read(
            "gmail-work",
            new[] { "message-1", "message-2" },
            isRead: false,
            mailboxId: "primary",
            folderId: "Inbox",
            confirmationToken: confirmationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.MessageActionService.LastSetReadStateRequest);
        Assert.Equal("gmail-work", fixture.MessageActionService.LastSetReadStateRequest!.ProfileId);
        Assert.Equal("primary", fixture.MessageActionService.LastSetReadStateRequest.MailboxId);
        Assert.Equal("Inbox", fixture.MessageActionService.LastSetReadStateRequest.FolderId);
        Assert.False(fixture.MessageActionService.LastSetReadStateRequest.IsRead);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastSetReadStateRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailFlagDelegatesToApplicationMessageActionService() {
        using var fixture = new TestFixture();
        const string confirmationToken = "mact_v1_flag";

        var result = await fixture.Tools.mail_flag(
            "gmail-work",
            new[] { "message-1", "message-2" },
            isFlagged: false,
            mailboxId: "primary",
            folderId: "Inbox",
            confirmationToken: confirmationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.MessageActionService.LastSetFlaggedStateRequest);
        Assert.Equal("gmail-work", fixture.MessageActionService.LastSetFlaggedStateRequest!.ProfileId);
        Assert.Equal("primary", fixture.MessageActionService.LastSetFlaggedStateRequest.MailboxId);
        Assert.Equal("Inbox", fixture.MessageActionService.LastSetFlaggedStateRequest.FolderId);
        Assert.False(fixture.MessageActionService.LastSetFlaggedStateRequest.IsFlagged);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastSetFlaggedStateRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailMarkReadPreviewUsesSharedPreviewService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_mark_read_preview(
            "gmail-work",
            new[] { "message-1", "MESSAGE-1" },
            isRead: false,
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal("read-state", result.Action);
        Assert.False(result.DesiredState);
        Assert.Equal(2, result.UniqueMessageCount);
        Assert.NotNull(result.ConfirmationToken);
    }

    [Fact]
    public async Task MailFlagPreviewUsesSharedPreviewService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_flag_preview(
            "gmail-work",
            new[] { "message-1", "MESSAGE-1" },
            isFlagged: false,
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal("flagged-state", result.Action);
        Assert.False(result.DesiredState);
        Assert.Equal(2, result.UniqueMessageCount);
        Assert.NotNull(result.ConfirmationToken);
    }

    [Fact]
    public async Task MailArchiveDelegatesToSharedArchiveAlias() {
        using var fixture = new TestFixture();
        const string confirmationToken = "mact_v1_archive";

        var result = await fixture.Tools.mail_archive(
            "gmail-work",
            new[] { "message-1" },
            mailboxId: "primary",
            folderId: "Inbox",
            confirmationToken: confirmationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.MessageActionService.LastMoveRequest);
        Assert.Equal(MailFolderAliases.Archive, fixture.MessageActionService.LastMoveRequest!.DestinationFolderId);
        Assert.Equal("primary", fixture.MessageActionService.LastMoveRequest.MailboxId);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastMoveRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailTrashDelegatesToSharedTrashAlias() {
        using var fixture = new TestFixture();
        const string confirmationToken = "mact_v1_trash";

        var result = await fixture.Tools.mail_trash(
            "gmail-work",
            new[] { "message-1" },
            mailboxId: "primary",
            folderId: "Inbox",
            confirmationToken: confirmationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.MessageActionService.LastMoveRequest);
        Assert.Equal(MailFolderAliases.Trash, fixture.MessageActionService.LastMoveRequest!.DestinationFolderId);
        Assert.Equal("primary", fixture.MessageActionService.LastMoveRequest.MailboxId);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastMoveRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailMoveDelegatesToApplicationMessageActionService() {
        using var fixture = new TestFixture();
        const string confirmationToken = "mact_v1_move";

        var result = await fixture.Tools.mail_move(
            "gmail-work",
            new[] { "message-1" },
            "Archive",
            mailboxId: "primary",
            folderId: "Inbox",
            confirmationToken: confirmationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.MessageActionService.LastMoveRequest);
        Assert.Equal("Archive", fixture.MessageActionService.LastMoveRequest!.DestinationFolderId);
        Assert.Equal("primary", fixture.MessageActionService.LastMoveRequest.MailboxId);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastMoveRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailDeleteDelegatesToApplicationMessageActionService() {
        using var fixture = new TestFixture();
        const string confirmationToken = "mact_v1_delete";

        var result = await fixture.Tools.mail_delete(
            "gmail-work",
            new[] { "message-1", "message-2" },
            mailboxId: "primary",
            folderId: "Inbox",
            confirmationToken: confirmationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.MessageActionService.LastDeleteRequest);
        Assert.Equal("gmail-work", fixture.MessageActionService.LastDeleteRequest!.ProfileId);
        Assert.Equal(new[] { "message-1", "message-2" }, fixture.MessageActionService.LastDeleteRequest.MessageIds);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastDeleteRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailSendBuildsExplicitQueueOnFailureRequest() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_send(
            "gmail-work",
            to: new[] { "alice@example.com" },
            subject: "Status update",
            textBody: "Queued body",
            cc: new[] { "bob@example.com" },
            attachmentPaths: new[] { "C:\\Temp\\status.txt" },
            queueOnFailure: true);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.SendService.LastRequest);
        Assert.Equal("gmail-work", fixture.SendService.LastRequest!.ProfileId);
        Assert.True(fixture.SendService.LastRequest.QueueOnFailure);
        Assert.Equal("alice@example.com", fixture.SendService.LastRequest.Message.To[0].Address);
        Assert.Equal("bob@example.com", fixture.SendService.LastRequest.Message.Cc[0].Address);
        Assert.Equal("C:\\Temp\\status.txt", fixture.SendService.LastRequest.Message.Attachments[0].Path);
    }

    [Fact]
    public async Task MailDraftSaveAndListRoundTripsThroughDraftService() {
        using var fixture = new TestFixture();

        var saveResult = await fixture.Tools.mail_draft_save(
            draftId: "draft-1",
            name: "Weekly update",
            profileId: "gmail-work",
            to: new[] { "alice@example.com" },
            subject: "Weekly update",
            textBody: "Draft body",
            cc: new[] { "bob@example.com" });

        Assert.True(saveResult.Succeeded);

        var drafts = await fixture.Tools.mail_draft_list();

        var draft = Assert.Single(drafts);
        Assert.Equal("draft-1", draft.Id);
        Assert.Equal("Weekly update", draft.Name);
        Assert.Equal("gmail-work", draft.Message.ProfileId);
        Assert.Equal("alice@example.com", draft.Message.To[0].Address);
        Assert.Equal("bob@example.com", draft.Message.Cc[0].Address);
    }

    [Fact]
    public async Task MailDraftCompactListReturnsLightweightProjection() {
        using var fixture = new TestFixture();
        await fixture.Tools.mail_draft_save(
            draftId: "draft-1",
            name: "Weekly update",
            profileId: "gmail-work",
            to: new[] { "alice@example.com" },
            subject: "Weekly update");

        var drafts = await fixture.Tools.mail_draft_compact_list();

        var draft = Assert.Single(drafts);
        Assert.Equal("draft-1", draft.Id);
        Assert.Equal("gmail-work", draft.ProfileId);
        Assert.Equal("Weekly update", draft.Subject);
    }

    [Fact]
    public async Task MailDraftGetReturnsStoredDraft() {
        using var fixture = new TestFixture();
        await fixture.Tools.mail_draft_save(
            draftId: "draft-1",
            name: "Weekly update",
            profileId: "gmail-work",
            to: new[] { "alice@example.com" },
            subject: "Weekly update");

        var draft = await fixture.Tools.mail_draft_get("draft-1");

        Assert.Equal("draft-1", draft.Id);
        Assert.Equal("Weekly update", draft.Name);
        Assert.Equal("alice@example.com", draft.Message.To[0].Address);
    }

    [Fact]
    public async Task MailDraftCompactGetReturnsLightweightProjection() {
        using var fixture = new TestFixture();
        await fixture.Tools.mail_draft_save(
            draftId: "draft-1",
            name: "Weekly update",
            profileId: "gmail-work",
            to: new[] { "alice@example.com" },
            subject: "Weekly update");

        var draft = await fixture.Tools.mail_draft_compact_get("draft-1");

        Assert.Equal("draft-1", draft.Id);
        Assert.Equal("gmail-work", draft.ProfileId);
        Assert.Equal("Weekly update", draft.Subject);
    }

    [Fact]
    public async Task MailDraftSendUsesStoredDraft() {
        using var fixture = new TestFixture();
        await fixture.Tools.mail_draft_save(
            draftId: "draft-1",
            name: "Weekly update",
            profileId: "gmail-work",
            to: new[] { "alice@example.com" },
            subject: "Weekly update",
            textBody: "Draft body");

        var result = await fixture.Tools.mail_draft_send("draft-1", queueOnFailure: true);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.SendService.LastRequest);
        Assert.Equal("gmail-work", fixture.SendService.LastRequest!.ProfileId);
        Assert.True(fixture.SendService.LastRequest.QueueOnFailure);
        Assert.Equal("alice@example.com", fixture.SendService.LastRequest.Message.To[0].Address);
        Assert.Equal("Weekly update", fixture.SendService.LastRequest.Message.Subject);
    }

    [Fact]
    public async Task MailDraftImportLoadsDraftFileIntoSharedStore() {
        using var fixture = new TestFixture();
        var path = fixture.CreatePath("imported-draft.json");
        await fixture.Application.DraftExchange.SaveAsync(path, new MailDraft {
            Id = "external-draft",
            Name = "Imported draft",
            Message = new DraftMessage {
                ProfileId = "gmail-work",
                Subject = "Imported subject",
                To = {
                    new MessageRecipient { Address = "imported@example.com" }
                }
            }
        });

        var imported = await fixture.Tools.mail_draft_import(path, draftId: "draft-1", name: "Imported into store");
        var stored = await fixture.Tools.mail_draft_get("draft-1");

        Assert.Equal("draft-1", imported.Id);
        Assert.Equal("Imported into store", imported.Name);
        Assert.Equal("draft-1", stored.Id);
        Assert.Equal("Imported into store", stored.Name);
        Assert.Equal("imported@example.com", stored.Message.To[0].Address);
    }

    [Fact]
    public async Task MailDraftExportWritesStoredDraftFile() {
        using var fixture = new TestFixture();
        var path = fixture.CreatePath("exported-draft.json");
        await fixture.Tools.mail_draft_save(
            draftId: "draft-1",
            name: "Weekly update",
            profileId: "gmail-work",
            to: new[] { "alice@example.com" },
            subject: "Weekly update");

        var result = await fixture.Tools.mail_draft_export("draft-1", path);
        var exported = await fixture.Application.DraftExchange.LoadAsync(path);

        Assert.True(result.Succeeded);
        Assert.Equal("draft-1", exported.Id);
        Assert.Equal("Weekly update", exported.Name);
        Assert.Equal("alice@example.com", exported.Message.To[0].Address);
    }

    [Fact]
    public async Task MailDraftDeleteRemovesStoredDraft() {
        using var fixture = new TestFixture();
        await fixture.Tools.mail_draft_save(
            draftId: "draft-1",
            name: "Weekly update",
            profileId: "gmail-work",
            to: new[] { "alice@example.com" },
            subject: "Weekly update");

        var deleteResult = await fixture.Tools.mail_draft_delete("draft-1");
        var drafts = await fixture.Tools.mail_draft_list();

        Assert.True(deleteResult.Succeeded);
        Assert.Empty(drafts);
    }

    [Fact]
    public async Task MailQueueProcessDelegatesToQueueService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_queue_process();

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.AttemptedCount);
        Assert.Equal(1, result.SentCount);
        Assert.True(fixture.QueueService.ProcessCalled);
    }

    [Fact]
    public async Task MailQueueCompactListReturnsLightweightProjection() {
        using var fixture = new TestFixture();

        var queued = await fixture.Tools.mail_queue_compact_list();

        var message = Assert.Single(queued);
        Assert.Equal("queued-1", message.MessageId);
        Assert.Equal("gmail", message.Provider);
    }

    [Fact]
    public async Task MailQueueCompactGetReturnsLightweightProjection() {
        using var fixture = new TestFixture();

        var message = await fixture.Tools.mail_queue_compact_get("queued-1");

        Assert.Equal("queued-1", message.MessageId);
        Assert.Equal("gmail", message.Provider);
    }
}
#endif
