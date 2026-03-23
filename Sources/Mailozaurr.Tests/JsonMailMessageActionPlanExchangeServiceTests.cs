using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class JsonMailMessageActionPlanExchangeServiceTests {
    [Fact]
    public async Task SaveAndLoadRoundTripsSinglePlanJson() {
        var service = new JsonMailMessageActionPlanExchangeService();
        var path = CreateTemporaryFilePath("plan.json");
        var plan = new MessageActionExecutionPlan {
            Succeeded = true,
            Action = "move",
            ExecutionKind = "Move",
            ProfileId = "work-gmail",
            MailboxId = "primary",
            FolderId = "Inbox",
            RequestedCount = 2,
            UniqueMessageCount = 1,
            RequestedDestinationFolderId = "Archive",
            MessageIds = { "msg-1" }
        };

        await service.SaveAsync(path, plan);
        var loaded = await service.LoadAsync(path);

        Assert.Equal("move", loaded.Action);
        Assert.Equal("Move", loaded.ExecutionKind);
        Assert.Equal("work-gmail", loaded.ProfileId);
        Assert.Equal("Archive", loaded.RequestedDestinationFolderId);
        Assert.Equal(new[] { "msg-1" }, loaded.MessageIds);
    }

    [Fact]
    public async Task SaveAndLoadRoundTripsBatchJson() {
        var service = new JsonMailMessageActionPlanExchangeService();
        var path = CreateTemporaryFilePath("plans.json");
        var plans = new[] {
            new MessageActionExecutionPlan {
                Succeeded = true,
                Action = "mark-read",
                ExecutionKind = "SetReadState",
                ProfileId = "work-gmail",
                RequestedCount = 1,
                UniqueMessageCount = 1,
                DesiredState = true,
                MessageIds = { "msg-1" }
            },
            new MessageActionExecutionPlan {
                Succeeded = true,
                Action = "delete",
                ExecutionKind = "Delete",
                ProfileId = "work-gmail",
                RequestedCount = 1,
                UniqueMessageCount = 1,
                MessageIds = { "msg-2" }
            }
        };

        await service.SaveBatchAsync(path, plans);
        var loaded = await service.LoadBatchAsync(path);

        Assert.Equal(2, loaded.Count);
        Assert.Equal("mark-read", loaded[0].Action);
        Assert.Equal("delete", loaded[1].Action);
    }

    private static string CreateTemporaryFilePath(string fileName) {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }
}
