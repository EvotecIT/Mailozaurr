using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationMessageActionPreviewServiceTests {
    [Fact]
    public async Task PreviewReadStateNormalizesMessageIdsAndProducesToken() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Server] = "imap.example.com"
            }
        });

        var service = new MailMessageActionPreviewService(profileStore, new FakeFolderAliasService());
        var preview = await service.PreviewReadStateAsync(new SetReadStateRequest {
            ProfileId = "work-imap",
            MailboxId = "shared@example.com",
            FolderId = "Inbox",
            MessageIds = { "msg-1", "MSG-1", "", "msg-2" },
            IsRead = false
        });

        Assert.True(preview.Succeeded);
        Assert.Equal("read-state", preview.Action);
        Assert.False(preview.DesiredState);
        Assert.Equal(3, preview.UniqueMessageCount);
        Assert.Equal(
            MessageActionConfirmationTokens.CreateReadStateToken("work-imap", "shared@example.com", "Inbox", new[] { "msg-1", "MSG-1", "msg-2" }, isRead: false),
            preview.ConfirmationToken);
    }

    [Fact]
    public async Task PreviewFlaggedStateNormalizesMessageIdsAndProducesToken() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Server] = "imap.example.com"
            }
        });

        var service = new MailMessageActionPreviewService(profileStore, new FakeFolderAliasService());
        var preview = await service.PreviewFlaggedStateAsync(new SetFlaggedStateRequest {
            ProfileId = "work-imap",
            MailboxId = "shared@example.com",
            FolderId = "Inbox",
            MessageIds = { "msg-1", "MSG-1", "", "msg-2" },
            IsFlagged = false
        });

        Assert.True(preview.Succeeded);
        Assert.Equal("flagged-state", preview.Action);
        Assert.False(preview.DesiredState);
        Assert.Equal(3, preview.UniqueMessageCount);
        Assert.Equal(
            MessageActionConfirmationTokens.CreateFlaggedStateToken("work-imap", "shared@example.com", "Inbox", new[] { "msg-1", "MSG-1", "msg-2" }, isFlagged: false),
            preview.ConfirmationToken);
    }

    [Fact]
    public async Task PreviewMoveNormalizesMessageIdsAndResolvesDestination() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Server] = "imap.example.com"
            }
        });

        var service = new MailMessageActionPreviewService(profileStore, new FakeFolderAliasService());
        var preview = await service.PreviewMoveAsync(new MoveMessagesPreviewRequest {
            ProfileId = "work-imap",
            MailboxId = "shared@example.com",
            MessageIds = { "msg-1", "MSG-1", "", "msg-2" },
            DestinationFolderId = "archive"
        });

        Assert.True(preview.Succeeded);
        Assert.Equal(4, preview.RequestedCount);
        Assert.Equal(3, preview.UniqueMessageCount);
        Assert.Equal(1, preview.DuplicateOrEmptyCount);
        Assert.Equal(new[] { "msg-1", "MSG-1", "msg-2" }, preview.MessageIds);
        Assert.NotNull(preview.Destination);
        Assert.Equal("archive-folder", preview.Destination!.EffectiveFolderId);
        Assert.Equal(
            MessageActionConfirmationTokens.CreateMoveToken("work-imap", "shared@example.com", null, new[] { "msg-1", "MSG-1", "msg-2" }, "archive-folder"),
            preview.ConfirmationToken);
        Assert.Contains(preview.Warnings, warning => warning.IndexOf("Ignored 1 duplicate or empty", StringComparison.Ordinal) >= 0);
    }

    [Fact]
    public async Task PreviewMoveFailsWhenDestinationIsUnsupported() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Server] = "imap.example.com"
            }
        });

        var service = new MailMessageActionPreviewService(profileStore, new UnsupportedFolderAliasService());
        var preview = await service.PreviewMoveAsync(new MoveMessagesPreviewRequest {
            ProfileId = "work-imap",
            MessageIds = { "msg-1" },
            DestinationFolderId = MailFolderAliases.Archive
        });

        Assert.False(preview.Succeeded);
        Assert.Equal("destination_not_supported", preview.Code);
    }

    [Fact]
    public async Task PreviewDeleteNormalizesMessageIds() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Server] = "imap.example.com"
            }
        });

        var service = new MailMessageActionPreviewService(profileStore, new FakeFolderAliasService());
        var preview = await service.PreviewDeleteAsync(new DeleteMessagesPreviewRequest {
            ProfileId = "work-imap",
            MessageIds = { "msg-1", "MSG-1", "", "msg-2" }
        });

        Assert.True(preview.Succeeded);
        Assert.Equal(4, preview.RequestedCount);
        Assert.Equal(3, preview.UniqueMessageCount);
        Assert.Equal(1, preview.DuplicateOrEmptyCount);
        Assert.Equal(new[] { "msg-1", "MSG-1", "msg-2" }, preview.MessageIds);
        Assert.Equal(
            MessageActionConfirmationTokens.CreateDeleteToken("work-imap", null, null, new[] { "msg-1", "MSG-1", "msg-2" }),
            preview.ConfirmationToken);
        Assert.Contains(preview.Warnings, warning => warning.IndexOf("Ignored 1 duplicate or empty", StringComparison.Ordinal) >= 0);
    }

    [Fact]
    public async Task PreviewDeleteFailsWhenCapabilityIsUnsupported() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-smtp",
            DisplayName = "Work SMTP",
            Kind = MailProfileKind.Smtp,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Server] = "smtp.example.com"
            }
        });

        var service = new MailMessageActionPreviewService(profileStore, new FakeFolderAliasService());
        var preview = await service.PreviewDeleteAsync(new DeleteMessagesPreviewRequest {
            ProfileId = "work-smtp",
            MessageIds = { "msg-1" }
        });

        Assert.False(preview.Succeeded);
        Assert.Equal("delete_not_supported", preview.Code);
    }

    [Fact]
    public async Task PreviewStandardActionsCombinesArchiveTrashMoveAndDelete() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Server] = "imap.example.com"
            }
        });

        var service = new MailMessageActionPreviewService(profileStore, new FakeFolderAliasService());
        var preview = await service.PreviewStandardActionsAsync(new StandardMessageActionsPreviewRequest {
            ProfileId = "work-imap",
            MailboxId = "shared@example.com",
            MessageIds = { "msg-1", "MSG-1", "", "msg-2" },
            DestinationFolderId = "Projects/2026"
        });

        Assert.True(preview.Succeeded);
        Assert.Equal(4, preview.RequestedCount);
        Assert.Equal(3, preview.UniqueMessageCount);
        Assert.Equal(1, preview.DuplicateOrEmptyCount);
        Assert.Equal(4, preview.IncludedActionCount);
        Assert.Equal(4, preview.SucceededActionCount);
        Assert.Equal(0, preview.FailedActionCount);
        Assert.Contains(preview.Warnings, warning => warning.IndexOf("Ignored 1 duplicate or empty", StringComparison.Ordinal) >= 0);

        var archive = Assert.Single(preview.Actions, action => action.Action == "archive");
        Assert.Equal("archive-folder", archive.Destination!.EffectiveFolderId);
        Assert.NotNull(archive.ConfirmationToken);

        var trash = Assert.Single(preview.Actions, action => action.Action == "trash");
        Assert.Equal("trash-folder", trash.Destination!.EffectiveFolderId);
        Assert.NotNull(trash.ConfirmationToken);

        var move = Assert.Single(preview.Actions, action => action.Action == "move");
        Assert.Equal("Projects/2026", move.Destination!.EffectiveFolderId);
        Assert.NotNull(move.ConfirmationToken);

        var delete = Assert.Single(preview.Actions, action => action.Action == "delete");
        Assert.True(delete.Succeeded);
        Assert.NotNull(delete.ConfirmationToken);
    }

    [Fact]
    public async Task PreviewStandardActionsReportsUnsupportedActions() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-smtp",
            DisplayName = "Work SMTP",
            Kind = MailProfileKind.Smtp,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Server] = "smtp.example.com"
            }
        });

        var service = new MailMessageActionPreviewService(profileStore, new FakeFolderAliasService());
        var preview = await service.PreviewStandardActionsAsync(new StandardMessageActionsPreviewRequest {
            ProfileId = "work-smtp",
            MessageIds = { "msg-1" },
            DestinationFolderId = "Archive"
        });

        Assert.False(preview.Succeeded);
        Assert.Equal("no_supported_actions", preview.Code);
        Assert.Equal(4, preview.IncludedActionCount);
        Assert.Equal(0, preview.SucceededActionCount);
        Assert.Equal(4, preview.FailedActionCount);
        Assert.All(preview.Actions, action => Assert.False(action.Succeeded));
        Assert.Contains(preview.Actions, action => action.Action == "move" && action.Code == "move_not_supported");
        Assert.Contains(preview.Actions, action => action.Action == "delete" && action.Code == "delete_not_supported");
    }

    [Fact]
    public async Task PreviewCommonActionsIncludesStateAndMailboxActions() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await profileStore.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Server] = "imap.example.com"
            }
        });

        var service = new MailMessageActionPreviewService(profileStore, new FakeFolderAliasService());
        var preview = await service.PreviewCommonActionsAsync(new CommonMessageActionsPreviewRequest {
            ProfileId = "work-imap",
            MailboxId = "shared@example.com",
            FolderId = "Inbox",
            MessageIds = { "msg-1", "MSG-1", "msg-2" },
            DestinationFolderId = "Projects/2026"
        });

        Assert.True(preview.Succeeded);
        Assert.Equal(8, preview.IncludedActionCount);
        Assert.Equal(8, preview.SucceededActionCount);
        Assert.Equal(0, preview.FailedActionCount);
        Assert.Equal(3, preview.UniqueMessageCount);
        Assert.Contains(preview.Actions, action => action.Action == "mark-read" && action.DesiredState == true);
        Assert.Contains(preview.Actions, action => action.Action == "mark-unread" && action.DesiredState == false);
        Assert.Contains(preview.Actions, action => action.Action == "flag" && action.DesiredState == true);
        Assert.Contains(preview.Actions, action => action.Action == "unflag" && action.DesiredState == false);
        Assert.Contains(preview.Actions, action => action.Action == "archive" && action.Destination!.EffectiveFolderId == "archive-folder");
        Assert.Contains(preview.Actions, action => action.Action == "move" && action.Destination!.EffectiveFolderId == "Projects/2026");
        Assert.All(preview.Actions, action => Assert.NotNull(action.ConfirmationToken));
    }

    private static string CreateTemporaryFilePath(string fileName) {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
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
                },
                new MailFolderAliasSummary {
                    ProfileId = profileId,
                    MailboxId = mailboxId,
                    Alias = MailFolderAliases.Trash,
                    DisplayName = "Trash",
                    IsSupported = true,
                    IsResolved = true,
                    FolderId = "trash-folder",
                    FolderDisplayName = "Trash",
                    FolderPath = "Trash",
                    Summary = "Trash -> Trash"
                }
            });

        public Task<MailFolderTargetResolution> ResolveAsync(string profileId, string targetFolderId, string? mailboxId = null, CancellationToken cancellationToken = default) {
            if (string.Equals(targetFolderId, MailFolderAliases.Trash, StringComparison.OrdinalIgnoreCase)) {
                return Task.FromResult(new MailFolderTargetResolution {
                    ProfileId = profileId,
                    MailboxId = mailboxId,
                    RequestedValue = targetFolderId,
                    IsAlias = true,
                    Alias = MailFolderAliases.Trash,
                    IsSupported = true,
                    IsResolved = true,
                    EffectiveFolderId = "trash-folder",
                    FolderDisplayName = "Trash",
                    FolderPath = "Trash",
                    Summary = "Trash -> Trash"
                });
            }

            if (string.Equals(targetFolderId, "Projects/2026", StringComparison.OrdinalIgnoreCase)) {
                return Task.FromResult(new MailFolderTargetResolution {
                    ProfileId = profileId,
                    MailboxId = mailboxId,
                    RequestedValue = targetFolderId,
                    IsAlias = false,
                    IsSupported = true,
                    IsResolved = true,
                    EffectiveFolderId = "Projects/2026",
                    FolderDisplayName = "Projects/2026",
                    FolderPath = "Projects/2026",
                    Summary = "Projects/2026"
                });
            }

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

    private sealed class UnsupportedFolderAliasService : IMailFolderAliasService {
        public Task<IReadOnlyList<MailFolderAliasSummary>> GetAliasesAsync(string profileId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MailFolderAliasSummary>>(Array.Empty<MailFolderAliasSummary>());

        public Task<MailFolderTargetResolution> ResolveAsync(string profileId, string targetFolderId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MailFolderTargetResolution {
                ProfileId = profileId,
                MailboxId = mailboxId,
                RequestedValue = targetFolderId,
                IsAlias = true,
                Alias = MailFolderAliases.Archive,
                IsSupported = false,
                IsResolved = false,
                EffectiveFolderId = MailFolderAliases.Archive,
                Summary = "Archive [unsupported]"
            });
    }
}