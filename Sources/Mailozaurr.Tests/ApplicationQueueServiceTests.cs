using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationQueueServiceTests {
    [Fact]
    public async Task ListAsyncReturnsQueuedMessagesInScheduleOrder() {
        var repository = new FilePendingMessageRepository(new PendingMessageRepositoryOptions {
            DirectoryPath = CreateTemporaryDirectory()
        });
        await repository.SaveAsync(new PendingMessageRecord {
            MessageId = "b",
            Provider = EmailProvider.Gmail,
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10),
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(10),
            AttemptCount = 1,
            MimeMessage = Convert.ToBase64String(Array.Empty<byte>())
        });
        await repository.SaveAsync(new PendingMessageRecord {
            MessageId = "a",
            Provider = EmailProvider.None,
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-20),
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(1),
            AttemptCount = 0,
            MimeMessage = Convert.ToBase64String(Array.Empty<byte>())
        });

        var service = new PendingMailQueueService(repository);
        var queued = await service.ListAsync();

        Assert.Equal(2, queued.Count);
        Assert.Equal("a", queued[0].MessageId);
        Assert.Equal(MailProfileKind.Smtp, queued[0].ProfileKind);
        Assert.Equal(MailProfileKind.Gmail, queued[1].ProfileKind);
    }

    [Fact]
    public async Task RemoveAsyncReturnsFailureWhenMessageDoesNotExist() {
        var repository = new FilePendingMessageRepository(new PendingMessageRepositoryOptions {
            DirectoryPath = CreateTemporaryDirectory()
        });

        var service = new PendingMailQueueService(repository);
        var result = await service.RemoveAsync("missing");

        Assert.False(result.Succeeded);
        Assert.Equal("queue_message_not_found", result.Code);
    }

    [Fact]
    public async Task ProcessAsyncReturnsObserverCounts() {
        var repository = new FilePendingMessageRepository(new PendingMessageRepositoryOptions {
            DirectoryPath = CreateTemporaryDirectory()
        });
        var record = new PendingMessageRecord {
            MessageId = "queued-1",
            Provider = EmailProvider.Gmail,
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-30),
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            AttemptCount = 0,
            MimeMessage = Convert.ToBase64String(Array.Empty<byte>())
        };
        await repository.SaveAsync(record);

        var service = new PendingMailQueueService(
            repository,
            (pendingRepository, observer, cancellationToken) => {
                observer.MessageAttemptStarted(record, 1);
                observer.MessageSent(record, 1, TimeSpan.FromSeconds(1));
                return Task.CompletedTask;
            });

        var result = await service.ProcessAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.AttemptedCount);
        Assert.Equal(1, result.SentCount);
        Assert.Equal(0, result.FailedCount);
    }

    private static string CreateTemporaryDirectory() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}