using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationRoutingServicesTests {
    [Fact]
    public async Task RoutedReadServiceDispatchesToMatchingHandler() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var handler = new FakeReadHandler(MailProfileKind.Imap);
        var service = new RoutedMailReadService(store, new[] { handler });

        var results = await service.SearchAsync(new MailSearchRequest {
            ProfileId = "work-imap",
            QueryText = "reports"
        });

        Assert.Single(results);
        Assert.Equal("msg-1", results[0].Id);
        Assert.Equal(1, handler.SearchCalls);
    }

    [Fact]
    public async Task RoutedReadServiceBuildsCompactFolderProjection() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var handler = new FakeReadHandler(MailProfileKind.Imap);
        var service = new RoutedMailReadService(store, new[] { handler });

        var results = await service.GetFoldersCompactAsync(new MailFolderQuery {
            ProfileId = "work-imap"
        });

        var result = Assert.Single(results);
        Assert.Equal("inbox", result.Id);
        Assert.Equal("Inbox", result.DisplayName);
        Assert.Equal("inbox Inbox", result.Summary);
    }

    [Fact]
    public async Task RoutedReadServiceBuildsCompactSearchProjection() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var handler = new FakeReadHandler(MailProfileKind.Imap);
        var service = new RoutedMailReadService(store, new[] { handler });

        var results = await service.SearchCompactAsync(new MailSearchRequest {
            ProfileId = "work-imap",
            QueryText = "reports"
        });

        var result = Assert.Single(results);
        Assert.Equal("msg-1", result.Id);
        Assert.Equal("reports", result.Subject);
        Assert.Equal("sender@example.com", result.From);
        Assert.Equal("msg-1 reports", result.Summary);
    }

    [Fact]
    public async Task RoutedReadServiceListsAttachmentsThroughSharedMessageProjection() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var handler = new FakeReadHandler(MailProfileKind.Imap);
        var service = new RoutedMailReadService(store, new[] { handler });

        var results = await service.GetAttachmentsAsync(new ListAttachmentsRequest {
            ProfileId = "work-imap",
            MessageId = "msg-1"
        });

        var result = Assert.Single(results);
        Assert.Equal("att-1", result.Id);
        Assert.Equal("report.pdf", result.FileName);
    }

    [Fact]
    public async Task RoutedReadServiceBuildsCompactMessageProjection() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var handler = new FakeReadHandler(MailProfileKind.Imap);
        var service = new RoutedMailReadService(store, new[] { handler });

        var result = await service.GetMessageCompactAsync(new GetMessageRequest {
            ProfileId = "work-imap",
            MessageId = "msg-1",
            IncludeRawContent = true
        });

        Assert.NotNull(result);
        Assert.Equal("msg-1", result!.Id);
        Assert.Equal("msg-1 Subject", result.SummaryText);
        Assert.True(result.HasRawContent);
        Assert.Single(result.Attachments);
    }

    [Fact]
    public async Task RoutedReadServiceSavesFilteredAttachmentsThroughSharedBatchWorkflow() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var handler = new FakeReadHandler(MailProfileKind.Imap);
        var service = new RoutedMailReadService(store, new[] { handler });

        var result = await service.SaveAttachmentsAsync(new SaveAttachmentsRequest {
            ProfileId = "work-imap",
            MessageId = "msg-1",
            DestinationPath = @"C:\Temp",
            FileNameContains = "report"
        });

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.MatchedCount);
        Assert.Equal(1, result.SavedCount);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public async Task RoutedSendServiceDispatchesToMatchingHandler() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-smtp",
            DisplayName = "Work SMTP",
            Kind = MailProfileKind.Smtp,
            DefaultSender = "sender@example.com",
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "smtp.example.com"
            }
        });

        var handler = new FakeSendHandler(MailProfileKind.Smtp);
        var service = new RoutedMailSendService(store, new[] { handler });

        var result = await service.SendAsync(new SendMessageRequest {
            ProfileId = "work-smtp",
            Message = new DraftMessage {
                ProfileId = "work-smtp",
                Subject = "Hello"
            }
        });

        Assert.True(result.Succeeded);
        Assert.Equal(1, handler.SendCalls);
    }

    [Fact]
    public async Task RoutedReadServiceRejectsUnsupportedCapabilities() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-smtp",
            DisplayName = "Work SMTP",
            Kind = MailProfileKind.Smtp,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "smtp.example.com"
            }
        });

        var handler = new FakeReadHandler(MailProfileKind.Smtp);
        var service = new RoutedMailReadService(store, new[] { handler });

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => service.SearchAsync(new MailSearchRequest {
            ProfileId = "work-smtp"
        }));

        Assert.Contains("SearchMessages", exception.Message, StringComparison.Ordinal);
    }

    private static string CreateTemporaryFilePath(string fileName) {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    private sealed class FakeReadHandler : IMailReadHandler {
        public FakeReadHandler(MailProfileKind kind) {
            Kind = kind;
        }

        public MailProfileKind Kind { get; }

        public int SearchCalls { get; private set; }

        public int SaveAttachmentCalls { get; private set; }

        public Task<IReadOnlyList<FolderRef>> GetFoldersAsync(MailProfile profile, MailFolderQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FolderRef>>(new[] {
                new FolderRef {
                    ProfileId = profile.Id,
                    MailboxId = query.MailboxId,
                    Id = "inbox",
                    DisplayName = "Inbox",
                    Path = "Inbox"
                }
            });

        public Task<MessageDetail?> GetMessageAsync(MailProfile profile, GetMessageRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<MessageDetail?>(new MessageDetail {
                ProfileId = profile.Id,
                Id = request.MessageId,
                Summary = new MessageSummary {
                    ProfileId = profile.Id,
                    Id = request.MessageId,
                    Subject = "Subject"
                },
                TextBody = "Body",
                Attachments = {
                    new AttachmentSummary {
                        MessageId = request.MessageId,
                        Id = "att-1",
                        FileName = "report.pdf",
                        SizeInBytes = 1024
                    }
                },
                RawContent = request.IncludeRawContent ? "raw" : null
            });

        public Task<OperationResult> SaveAttachmentAsync(MailProfile profile, SaveAttachmentRequest request, CancellationToken cancellationToken = default) {
            SaveAttachmentCalls++;
            return Task.FromResult(OperationResult.Success());
        }

        public Task<IReadOnlyList<MessageSummary>> SearchAsync(MailProfile profile, MailSearchRequest request, CancellationToken cancellationToken = default) {
            SearchCalls++;
            IReadOnlyList<MessageSummary> results = new[] {
                new MessageSummary {
                    ProfileId = profile.Id,
                    Id = "msg-1",
                    Subject = request.QueryText,
                    From = {
                        new MessageRecipient {
                            Address = "sender@example.com"
                        }
                    }
                }
            };
            return Task.FromResult(results);
        }
    }

    private sealed class FakeSendHandler : IMailSendHandler {
        public FakeSendHandler(MailProfileKind kind) {
            Kind = kind;
        }

        public MailProfileKind Kind { get; }

        public int SendCalls { get; private set; }

        public Task<SendResult> SendAsync(MailProfile profile, SendMessageRequest request, CancellationToken cancellationToken = default) {
            SendCalls++;
            return Task.FromResult(new SendResult {
                Succeeded = true,
                ProfileId = profile.Id,
                ProfileKind = profile.Kind
            });
        }
    }
}
