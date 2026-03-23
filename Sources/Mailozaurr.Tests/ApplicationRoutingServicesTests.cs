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
    public async Task RoutedReadServiceBuildsBatchCompactMessageProjection() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var handler = new FakeReadHandler(MailProfileKind.Imap);
        var service = new RoutedMailReadService(store, new[] { handler });

        var results = await service.GetMessagesCompactAsync(new GetMessagesRequest {
            ProfileId = "work-imap",
            MessageIds = { "msg-1", "msg-2" }
        });

        Assert.Equal(2, results.Count);
        Assert.Equal("msg-1", results[0].Id);
        Assert.Equal("msg-2", results[1].Id);
        Assert.Equal("msg-1 Subject", results[0].SummaryText);
        Assert.Equal("msg-2 Subject", results[1].SummaryText);
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
    public async Task RoutedReadServiceSavesFilteredAttachmentsAcrossMultipleMessages() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var handler = new FakeReadHandler(MailProfileKind.Imap);
        var service = new RoutedMailReadService(store, new[] { handler });

        var result = await service.SaveAttachmentsManyAsync(new SaveAttachmentsManyRequest {
            ProfileId = "work-imap",
            MessageIds = { "msg-1", "msg-2" },
            DestinationPath = @"C:\Temp",
            FileNameContains = "report"
        });

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.RequestedMessageCount);
        Assert.Equal(2, result.AttemptedMessageCount);
        Assert.Equal(2, result.SucceededMessageCount);
        Assert.Equal(2, result.MatchedCount);
        Assert.Equal(2, result.SavedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(2, result.MessageResults.Count);
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
    public async Task RoutedMessageActionServiceDispatchesToMatchingHandler() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var handler = new FakeMessageActionHandler(MailProfileKind.Imap);
        var service = new RoutedMailMessageActionService(store, new[] { handler });

        var result = await service.SetReadStateAsync(new SetReadStateRequest {
            ProfileId = "work-imap",
            MessageIds = { "1", "2" },
            IsRead = true
        });

        Assert.True(result.Succeeded);
        Assert.Equal(1, handler.SetReadStateCalls);
    }

    [Fact]
    public async Task RoutedMessageActionServiceRejectsMismatchedReadStateConfirmationToken() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var handler = new FakeMessageActionHandler(MailProfileKind.Imap);
        var service = new RoutedMailMessageActionService(store, new[] { handler });

        var result = await service.SetReadStateAsync(new SetReadStateRequest {
            ProfileId = "work-imap",
            MailboxId = "shared@example.com",
            FolderId = "Inbox",
            MessageIds = { "1", "2" },
            IsRead = true,
            ConfirmationToken = "mact_v1_invalid"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("confirmation_token_mismatch", result.Code);
        Assert.Equal(0, handler.SetReadStateCalls);
    }

    [Fact]
    public async Task RoutedMessageActionServiceDispatchesFlaggedStateToMatchingHandler() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var handler = new FakeMessageActionHandler(MailProfileKind.Imap);
        var service = new RoutedMailMessageActionService(store, new[] { handler });

        var result = await service.SetFlaggedStateAsync(new SetFlaggedStateRequest {
            ProfileId = "work-imap",
            MessageIds = { "1", "2" },
            IsFlagged = true
        });

        Assert.True(result.Succeeded);
        Assert.Equal(1, handler.SetFlaggedStateCalls);
    }

    [Fact]
    public async Task RoutedMessageActionServiceRejectsMismatchedFlaggedStateConfirmationToken() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var handler = new FakeMessageActionHandler(MailProfileKind.Imap);
        var service = new RoutedMailMessageActionService(store, new[] { handler });

        var result = await service.SetFlaggedStateAsync(new SetFlaggedStateRequest {
            ProfileId = "work-imap",
            MailboxId = "shared@example.com",
            FolderId = "Inbox",
            MessageIds = { "1", "2" },
            IsFlagged = true,
            ConfirmationToken = "mact_v1_invalid"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("confirmation_token_mismatch", result.Code);
        Assert.Equal(0, handler.SetFlaggedStateCalls);
    }

    [Fact]
    public async Task RoutedMessageActionServiceCanonicalizesKnownFolderAlias() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var handler = new FakeMessageActionHandler(MailProfileKind.Imap);
        var service = new RoutedMailMessageActionService(store, new[] { handler });

        var result = await service.MoveAsync(new MoveMessagesRequest {
            ProfileId = "work-imap",
            MessageIds = { "1", "2" },
            DestinationFolderId = "archive"
        });

        Assert.True(result.Succeeded);
        Assert.NotNull(handler.LastMoveRequest);
        Assert.Equal(MailFolderAliases.Archive, handler.LastMoveRequest!.DestinationFolderId);
    }

    [Fact]
    public async Task RoutedMessageActionServiceResolvesKnownFolderAliasThroughSharedAliasService() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var handler = new FakeMessageActionHandler(MailProfileKind.Imap);
        var aliasService = new FakeFolderAliasService();
        var service = new RoutedMailMessageActionService(store, new[] { handler }, aliasService);

        var result = await service.MoveAsync(new MoveMessagesRequest {
            ProfileId = "work-imap",
            MailboxId = "shared@example.com",
            MessageIds = { "1", "2" },
            DestinationFolderId = MailFolderAliases.Archive
        });

        Assert.True(result.Succeeded);
        Assert.Equal("work-imap", aliasService.LastProfileId);
        Assert.Equal("shared@example.com", aliasService.LastMailboxId);
        Assert.NotNull(handler.LastMoveRequest);
        Assert.Equal("archive-folder", handler.LastMoveRequest!.DestinationFolderId);
    }

    [Fact]
    public async Task RoutedMessageActionServiceRejectsMismatchedMoveConfirmationToken() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var handler = new FakeMessageActionHandler(MailProfileKind.Imap);
        var aliasService = new FakeFolderAliasService();
        var service = new RoutedMailMessageActionService(store, new[] { handler }, aliasService);

        var result = await service.MoveAsync(new MoveMessagesRequest {
            ProfileId = "work-imap",
            MailboxId = "shared@example.com",
            FolderId = "Inbox",
            MessageIds = { "1", "2" },
            DestinationFolderId = MailFolderAliases.Archive,
            ConfirmationToken = "mact_v1_invalid"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("confirmation_token_mismatch", result.Code);
        Assert.Null(handler.LastMoveRequest);
    }

    [Fact]
    public async Task RoutedMessageActionServiceAcceptsMatchingMoveConfirmationToken() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var handler = new FakeMessageActionHandler(MailProfileKind.Imap);
        var aliasService = new FakeFolderAliasService();
        var service = new RoutedMailMessageActionService(store, new[] { handler }, aliasService);
        var confirmationToken = MessageActionConfirmationTokens.CreateMoveToken(
            "work-imap",
            "shared@example.com",
            "Inbox",
            new[] { "1", "2" },
            "archive-folder");

        var result = await service.MoveAsync(new MoveMessagesRequest {
            ProfileId = "work-imap",
            MailboxId = "shared@example.com",
            FolderId = "Inbox",
            MessageIds = { "1", "2" },
            DestinationFolderId = MailFolderAliases.Archive,
            ConfirmationToken = confirmationToken
        });

        Assert.True(result.Succeeded);
        Assert.NotNull(handler.LastMoveRequest);
        Assert.Equal(confirmationToken, handler.LastMoveRequest!.ConfirmationToken);
    }

    [Fact]
    public async Task RoutedMessageActionServiceAcceptsPreviewGeneratedDeleteConfirmationTokenWithCaseDistinctIds() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var previewService = new MailMessageActionPreviewService(store, new FakeFolderAliasService());
        var preview = await previewService.PreviewDeleteAsync(new DeleteMessagesPreviewRequest {
            ProfileId = "work-imap",
            MailboxId = "shared@example.com",
            FolderId = "Inbox",
            MessageIds = { "msg-1", "MSG-1", "msg-2" }
        });

        Assert.True(preview.Succeeded);
        Assert.NotNull(preview.ConfirmationToken);

        var handler = new FakeMessageActionHandler(MailProfileKind.Imap);
        var service = new RoutedMailMessageActionService(store, new[] { handler }, new FakeFolderAliasService());
        var result = await service.DeleteAsync(new DeleteMessagesRequest {
            ProfileId = "work-imap",
            MailboxId = "shared@example.com",
            FolderId = "Inbox",
            MessageIds = { "msg-1", "MSG-1", "msg-2" },
            ConfirmationToken = preview.ConfirmationToken
        });

        Assert.True(result.Succeeded);
        Assert.NotNull(handler.LastDeleteRequest);
        Assert.Equal(preview.ConfirmationToken, handler.LastDeleteRequest!.ConfirmationToken);
    }

    [Fact]
    public async Task RoutedMessageActionServiceRejectsMismatchedDeleteConfirmationToken() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });

        var handler = new FakeMessageActionHandler(MailProfileKind.Imap);
        var service = new RoutedMailMessageActionService(store, new[] { handler });

        var result = await service.DeleteAsync(new DeleteMessagesRequest {
            ProfileId = "work-imap",
            MailboxId = "shared@example.com",
            FolderId = "Inbox",
            MessageIds = { "1", "2" },
            ConfirmationToken = "mact_v1_invalid"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("confirmation_token_mismatch", result.Code);
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

    private sealed class FakeMessageActionHandler : IMailMessageActionHandler {
        public FakeMessageActionHandler(MailProfileKind kind) {
            Kind = kind;
        }

        public MailProfileKind Kind { get; }

        public int SetReadStateCalls { get; private set; }

        public int SetFlaggedStateCalls { get; private set; }

        public MoveMessagesRequest? LastMoveRequest { get; private set; }

        public DeleteMessagesRequest? LastDeleteRequest { get; private set; }

        public Task<MessageActionResult> SetReadStateAsync(MailProfile profile, SetReadStateRequest request, CancellationToken cancellationToken = default) {
            SetReadStateCalls++;
            return Task.FromResult(new MessageActionResult {
                Succeeded = true,
                ProfileId = profile.Id,
                RequestedCount = request.MessageIds.Count,
                SucceededCount = request.MessageIds.Count
            });
        }

        public Task<MessageActionResult> SetFlaggedStateAsync(MailProfile profile, SetFlaggedStateRequest request, CancellationToken cancellationToken = default) {
            SetFlaggedStateCalls++;
            return Task.FromResult(new MessageActionResult {
                Succeeded = true,
                ProfileId = profile.Id,
                RequestedCount = request.MessageIds.Count,
                SucceededCount = request.MessageIds.Count
            });
        }

        public Task<MessageActionResult> MoveAsync(MailProfile profile, MoveMessagesRequest request, CancellationToken cancellationToken = default) {
            LastMoveRequest = request;
            return Task.FromResult(new MessageActionResult {
                Succeeded = true,
                ProfileId = profile.Id,
                RequestedCount = request.MessageIds.Count,
                SucceededCount = request.MessageIds.Count
            });
        }

        public Task<MessageActionResult> DeleteAsync(MailProfile profile, DeleteMessagesRequest request, CancellationToken cancellationToken = default) {
            LastDeleteRequest = request;
            return Task.FromResult(new MessageActionResult {
                Succeeded = true,
                ProfileId = profile.Id,
                RequestedCount = request.MessageIds.Count,
                SucceededCount = request.MessageIds.Count
            });
        }
    }

    private sealed class FakeFolderAliasService : IMailFolderAliasService {
        public string? LastProfileId { get; private set; }

        public string? LastMailboxId { get; private set; }

        public Task<IReadOnlyList<MailFolderAliasSummary>> GetAliasesAsync(string profileId, string? mailboxId = null, CancellationToken cancellationToken = default) {
            LastProfileId = profileId;
            LastMailboxId = mailboxId;
            return Task.FromResult<IReadOnlyList<MailFolderAliasSummary>>(new[] {
                new MailFolderAliasSummary {
                    ProfileId = profileId,
                    MailboxId = mailboxId,
                    Alias = MailFolderAliases.Archive,
                    DisplayName = "Archive",
                    IsSupported = true,
                    IsResolved = true,
                    FolderId = "archive-folder",
                    FolderDisplayName = "Archive",
                    FolderPath = "Archive",
                    Summary = "Archive -> Archive"
                }
            });
        }

        public Task<MailFolderTargetResolution> ResolveAsync(string profileId, string targetFolderId, string? mailboxId = null, CancellationToken cancellationToken = default) {
            LastProfileId = profileId;
            LastMailboxId = mailboxId;
            return Task.FromResult(new MailFolderTargetResolution {
                ProfileId = profileId,
                MailboxId = mailboxId,
                RequestedValue = targetFolderId,
                IsAlias = true,
                Alias = MailFolderAliases.Archive,
                IsSupported = true,
                IsResolved = true,
                EffectiveFolderId = "archive-folder",
                FolderDisplayName = "Archive",
                FolderPath = "Archive",
                Summary = "Archive -> Archive"
            });
        }
    }
}
