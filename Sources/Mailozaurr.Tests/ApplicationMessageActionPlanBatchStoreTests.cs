using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationMessageActionPlanBatchStoreTests {
    [Fact]
    public async Task FileActionPlanBatchStoreRoundTripsBatches() {
        var store = new FileMailMessageActionPlanBatchStore(CreateTemporaryFilePath("action-plan-batches.json"));
        var batch = new MailMessageActionPlanBatch {
            Id = "quarterly-cleanup",
            Name = "Quarterly cleanup",
            Plans = {
                new MessageActionExecutionPlan {
                    Succeeded = true,
                    Name = "Archive newsletter",
                    Summary = "Archive newsletter (1 message)",
                    Action = "move",
                    ExecutionKind = "Move",
                    ProfileId = "work-gmail",
                    RequestedCount = 1,
                    UniqueMessageCount = 1,
                    RequestedDestinationFolderId = "Archive",
                    MessageIds = { "msg-1" }
                }
            }
        };

        await store.SaveAsync(batch);
        var saved = await store.GetByIdAsync("quarterly-cleanup");

        Assert.NotNull(saved);
        Assert.Equal("Quarterly cleanup", saved!.Name);
        Assert.Single(saved.Plans);
        Assert.Equal("Archive newsletter", saved.Plans[0].Name);
        Assert.Equal("Archive newsletter (1 message)", saved.Plans[0].Summary);
        Assert.Equal("move", saved.Plans[0].Action);
        Assert.NotEqual(default, saved.CreatedAt);
        Assert.NotEqual(default, saved.UpdatedAt);
    }

    private static string CreateTemporaryFilePath(string fileName) {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }
}