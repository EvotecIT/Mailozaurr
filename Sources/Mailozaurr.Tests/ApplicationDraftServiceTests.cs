using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationDraftServiceTests {
    [Fact]
    public async Task SaveAsyncRejectsDraftWhenProfileIsMissing() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var draftStore = new FileMailDraftStore(CreateTemporaryFilePath("drafts.json"));
        var service = new MailDraftService(draftStore, profileStore);

        var result = await service.SaveAsync(new MailDraft {
            Id = "draft-1",
            Name = "Draft 1",
            Message = new DraftMessage {
                ProfileId = "missing-profile"
            }
        });

        Assert.False(result.Succeeded);
        Assert.Equal("draft_profile_not_found", result.Code);
    }

    [Fact]
    public async Task SaveAsyncStoresDraftWhenProfileExists() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var draftStore = new FileMailDraftStore(CreateTemporaryFilePath("drafts.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-gmail",
            DisplayName = "Work Gmail",
            Kind = MailProfileKind.Gmail,
            DefaultMailbox = "me"
        });
        var service = new MailDraftService(draftStore, profileStore);

        var result = await service.SaveAsync(new MailDraft {
            Id = "draft-1",
            Name = "Draft 1",
            Message = new DraftMessage {
                ProfileId = "work-gmail",
                Subject = "Hello"
            }
        });
        var saved = await service.GetDraftAsync("draft-1");

        Assert.True(result.Succeeded);
        Assert.NotNull(saved);
        Assert.Equal("Hello", saved!.Message.Subject);
    }

    [Fact]
    public async Task GetDraftsCompactReturnsLightweightProjection() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var draftStore = new FileMailDraftStore(CreateTemporaryFilePath("drafts.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-gmail",
            DisplayName = "Work Gmail",
            Kind = MailProfileKind.Gmail,
            DefaultMailbox = "me"
        });
        var service = new MailDraftService(draftStore, profileStore);
        await service.SaveAsync(new MailDraft {
            Id = "draft-1",
            Name = "Draft 1",
            Message = new DraftMessage {
                ProfileId = "work-gmail",
                Subject = "Hello"
            }
        });

        var drafts = await service.GetDraftsCompactAsync();

        var draft = Assert.Single(drafts);
        Assert.Equal("draft-1", draft.Id);
        Assert.Equal("work-gmail", draft.ProfileId);
        Assert.Equal("Hello", draft.Subject);
    }

    private static string CreateTemporaryFilePath(string fileName) {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }
}