#if NET8_0_OR_GREATER
namespace Mailozaurr.Tests;

public sealed partial class MailMcpToolsTests {
    [Fact]
    public async Task ChangeFeedToolsDelegateToApplicationService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_changes_get(
            "gmail-work",
            cursor: "100",
            mailboxId: "user@example.test",
            folderId: "INBOX",
            maxChanges: 25);

        Assert.Equal("next", result.NextCursor);
        Assert.NotNull(fixture.ChangeFeedService.LastFeedRequest);
        Assert.Equal("100", fixture.ChangeFeedService.LastFeedRequest!.Cursor);
        Assert.Equal("user@example.test", fixture.ChangeFeedService.LastFeedRequest.MailboxId);
        Assert.Equal(25, fixture.ChangeFeedService.LastFeedRequest.MaxChanges);
    }

    [Fact]
    public async Task ChangeSubscriptionToolDelegatesProviderInputs() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_changes_subscribe(
            "gmail-work",
            folderIds: new[] { "INBOX" },
            mailboxId: "user@example.test",
            topicName: "projects/test/topics/mail");

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.ChangeFeedService.LastSubscriptionRequest);
        Assert.Equal("projects/test/topics/mail", fixture.ChangeFeedService.LastSubscriptionRequest!.TopicName);
        Assert.Equal("user@example.test", fixture.ChangeFeedService.LastSubscriptionRequest.MailboxId);
    }

    [Fact]
    public async Task ChangeSubscriptionToolPreservesBlankFolderEvidence() {
        using var fixture = new TestFixture();

        await fixture.Tools.mail_changes_subscribe(
            "gmail-work",
            folderIds: new[] { " " },
            topicName: "projects/test/topics/mail");

        Assert.NotNull(fixture.ChangeFeedService.LastSubscriptionRequest);
        Assert.Equal(new[] { " " }, fixture.ChangeFeedService.LastSubscriptionRequest!.FolderIds);
    }
}
#endif
