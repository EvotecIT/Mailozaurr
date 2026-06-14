using Mailozaurr.Application;
using System.Text.Json;

namespace Mailozaurr.Tests;

public sealed class JsonMailDraftExchangeServiceTests {
    [Fact]
    public async Task SaveAndLoadRoundTripsDraftJson() {
        var service = new JsonMailDraftExchangeService();
        var path = CreateTemporaryFilePath("draft.json");
        var draft = new MailDraft {
            Id = "draft-1",
            Name = "Quarterly report",
            Message = new DraftMessage {
                ProfileId = "work-gmail",
                Subject = "Hello",
                To = {
                    new MessageRecipient { Address = "alice@example.com" }
                }
            }
        };

        await service.SaveAsync(path, draft);
        var loaded = await service.LoadAsync(path);

        Assert.Equal("draft-1", loaded.Id);
        Assert.Equal("Quarterly report", loaded.Name);
        Assert.Equal("work-gmail", loaded.Message.ProfileId);
        Assert.Equal("alice@example.com", loaded.Message.To[0].Address);
    }

    private static string CreateTemporaryFilePath(string fileName) {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }
}