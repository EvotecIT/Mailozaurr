using Mailozaurr;

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
            processAsync:
            (pendingRepository, deadLetters, observer, cancellationToken) => {
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

    [Fact]
    public async Task DeadLetterOperationsExposeTerminalFailures() {
        var repository = new FilePendingMessageRepository(new PendingMessageRepositoryOptions {
            DirectoryPath = CreateTemporaryDirectory()
        });
        var deadLetters = new FilePendingMessageDeadLetterRepository(Path.Combine(CreateTemporaryDirectory(), "dead-letter.log"));
        await deadLetters.SaveAsync(new PendingMessageDeadLetterRecord {
            Message = new PendingMessageRecord {
                MessageId = "dead-1",
                Provider = EmailProvider.Gmail,
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-30),
                AttemptCount = 3
            },
            Reason = PendingMessageDropReason.PermanentFailure,
            Attempt = 3,
            DeadLetteredAt = DateTimeOffset.UtcNow,
            ErrorMessage = "Authentication failed."
        });

        var service = new PendingMailQueueService(repository, deadLetters);

        var list = await service.ListDeadLettersAsync();
        var get = await service.GetDeadLetterAsync("dead-1");
        var remove = await service.RemoveDeadLetterAsync("dead-1");

        Assert.Single(list);
        Assert.NotNull(get);
        Assert.True(get!.IsDeadLetter);
        Assert.Equal("PermanentFailure", get.DeadLetterReason);
        Assert.Equal("Authentication failed.", get.ErrorMessage);
        Assert.True(remove.Succeeded);
        Assert.Empty(await service.ListDeadLettersAsync());
    }

    private static string CreateTemporaryDirectory() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
