using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationMessageActionPlanServiceTests {
    [Fact]
    public async Task CreatePlanResolvesMoveDestinationAndValidatesToken() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Server] = "imap.example.com"
            }
        });

        var previewService = new MailMessageActionPreviewService(profileStore, new FakeFolderAliasService());
        var actionService = new CapturingMessageActionService();
        var planningService = new MailMessageActionPlanService(previewService, actionService);
        var preview = await previewService.PreviewMoveAsync(new MoveMessagesPreviewRequest {
            ProfileId = "work-imap",
            MailboxId = "shared@example.com",
            FolderId = "Inbox",
            MessageIds = { "msg-1", "MSG-1" },
            DestinationFolderId = MailFolderAliases.Archive
        });

        var plan = await planningService.CreatePlanAsync(new MessageActionExecutionPlanRequest {
            Action = "archive",
            ProfileId = "work-imap",
            MailboxId = "shared@example.com",
            FolderId = "Inbox",
            MessageIds = { "msg-1", "MSG-1" },
            ConfirmationToken = preview.ConfirmationToken
        });

        Assert.True(plan.Succeeded);
        Assert.Equal("Move", plan.ExecutionKind);
        Assert.Equal(1, plan.UniqueMessageCount);
        Assert.True(plan.ConfirmationProvided);
        Assert.True(plan.ConfirmationValidated);
        Assert.Equal("archive-folder", plan.Destination!.EffectiveFolderId);
    }

    [Fact]
    public async Task CreatePlanRejectsMismatchedToken() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Server] = "imap.example.com"
            }
        });

        var planningService = new MailMessageActionPlanService(
            new MailMessageActionPreviewService(profileStore, new FakeFolderAliasService()),
            new CapturingMessageActionService());

        var plan = await planningService.CreatePlanAsync(new MessageActionExecutionPlanRequest {
            Action = "delete",
            ProfileId = "work-imap",
            MessageIds = { "msg-1" },
            ConfirmationToken = "mact_v1_invalid"
        });

        Assert.False(plan.Succeeded);
        Assert.Equal("confirmation_token_mismatch", plan.Code);
        Assert.True(plan.ConfirmationProvided);
        Assert.False(plan.ConfirmationValidated);
    }

    [Fact]
    public async Task ExecutePlanDispatchesNormalizedReadStateRequest() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Server] = "imap.example.com"
            }
        });

        var previewService = new MailMessageActionPreviewService(profileStore, new FakeFolderAliasService());
        var actionService = new CapturingMessageActionService();
        var planningService = new MailMessageActionPlanService(previewService, actionService);
        var plan = await planningService.CreatePlanAsync(new MessageActionExecutionPlanRequest {
            Action = "mark-unread",
            ProfileId = "work-imap",
            MailboxId = "shared@example.com",
            FolderId = "Inbox",
            MessageIds = { "msg-1", "MSG-1" }
        });

        var result = await planningService.ExecuteAsync(plan);

        Assert.True(result.Succeeded);
        Assert.NotNull(actionService.LastReadStateRequest);
        Assert.Equal("work-imap", actionService.LastReadStateRequest!.ProfileId);
        Assert.Equal("shared@example.com", actionService.LastReadStateRequest.MailboxId);
        Assert.Equal("Inbox", actionService.LastReadStateRequest.FolderId);
        Assert.False(actionService.LastReadStateRequest.IsRead);
        Assert.Equal(new[] { "msg-1" }, actionService.LastReadStateRequest.MessageIds);
        Assert.Equal(plan.ConfirmationToken, actionService.LastReadStateRequest.ConfirmationToken);
    }

    private static string CreateTemporaryFilePath(string fileName) {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    private sealed class CapturingMessageActionService : IMailMessageActionService {
        public SetReadStateRequest? LastReadStateRequest { get; private set; }

        public Task<MessageActionResult> SetReadStateAsync(SetReadStateRequest request, CancellationToken cancellationToken = default) {
            LastReadStateRequest = request;
            return Task.FromResult(new MessageActionResult {
                Succeeded = true,
                ProfileId = request.ProfileId,
                RequestedCount = request.MessageIds.Count,
                SucceededCount = request.MessageIds.Count
            });
        }

        public Task<MessageActionResult> SetFlaggedStateAsync(SetFlaggedStateRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MessageActionResult {
                Succeeded = true,
                ProfileId = request.ProfileId,
                RequestedCount = request.MessageIds.Count,
                SucceededCount = request.MessageIds.Count
            });

        public Task<MessageActionResult> MoveAsync(MoveMessagesRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MessageActionResult {
                Succeeded = true,
                ProfileId = request.ProfileId,
                RequestedCount = request.MessageIds.Count,
                SucceededCount = request.MessageIds.Count
            });

        public Task<MessageActionResult> DeleteAsync(DeleteMessagesRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MessageActionResult {
                Succeeded = true,
                ProfileId = request.ProfileId,
                RequestedCount = request.MessageIds.Count,
                SucceededCount = request.MessageIds.Count
            });
    }

    private sealed class FakeFolderAliasService : IMailFolderAliasService {
        public Task<IReadOnlyList<MailFolderAliasSummary>> GetAliasesAsync(string profileId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MailFolderAliasSummary>>(new[] {
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

        public Task<MailFolderTargetResolution> ResolveAsync(string profileId, string targetFolderId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MailFolderTargetResolution {
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
