using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationDraftStoreTests {
    [Fact]
    public async Task FileDraftStoreRoundTripsDrafts() {
        var store = new FileMailDraftStore(CreateTemporaryFilePath("drafts.json"));
        var draft = new MailDraft {
            Id = "report-draft",
            Name = "Quarterly report",
            Message = new DraftMessage {
                ProfileId = "work-gmail",
                Subject = "Report",
                TextBody = "Hello",
                To = {
                    new MessageRecipient { Address = "alice@example.com" }
                }
            }
        };

        await store.SaveAsync(draft);
        var saved = await store.GetByIdAsync("report-draft");

        Assert.NotNull(saved);
        Assert.Equal("Quarterly report", saved!.Name);
        Assert.Equal("work-gmail", saved.Message.ProfileId);
        Assert.Equal("alice@example.com", saved.Message.To[0].Address);
        Assert.NotEqual(default, saved.CreatedAt);
        Assert.NotEqual(default, saved.UpdatedAt);
    }

    private static string CreateTemporaryFilePath(string fileName) {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }
}