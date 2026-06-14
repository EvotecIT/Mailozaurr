using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationFolderAliasServiceTests {
    [Fact]
    public async Task FolderAliasServiceResolvesKnownAliasesFromFolderMetadata() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "graph-work",
            DisplayName = "Graph Work",
            Kind = MailProfileKind.Graph,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.ClientId] = "client-id",
                [MailProfileSettingsKeys.TenantId] = "tenant-id",
                [MailProfileSettingsKeys.Mailbox] = "shared@example.com"
            }
        });

        var aliases = await new MailFolderAliasService(store, new FakeReadService()).GetAliasesAsync("graph-work", "shared@example.com");

        var archive = Assert.Single(aliases, alias => alias.Alias == MailFolderAliases.Archive);
        Assert.True(archive.IsSupported);
        Assert.True(archive.IsResolved);
        Assert.Equal("archive", archive.FolderId);
        Assert.Equal("Archive", archive.FolderDisplayName);
        Assert.Equal("Archive -> Archive", archive.Summary);
    }

    [Fact]
    public async Task FolderAliasServiceFallsBackToAliasOnlyWhenFolderLookupFails() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "imap-work",
            DisplayName = "IMAP Work",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Server] = "imap.example.com"
            }
        });

        var aliases = await new MailFolderAliasService(store, new ThrowingReadService()).GetAliasesAsync("imap-work");

        var archive = Assert.Single(aliases, alias => alias.Alias == MailFolderAliases.Archive);
        Assert.True(archive.IsSupported);
        Assert.False(archive.IsResolved);
        Assert.Null(archive.FolderId);
        Assert.Equal("Archive [alias-only]", archive.Summary);
    }

    [Fact]
    public async Task FolderAliasServiceResolvesExplicitFolderTargetsWithoutAliasLookup() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "imap-work",
            DisplayName = "IMAP Work",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Server] = "imap.example.com"
            }
        });

        var resolution = await new MailFolderAliasService(store, new FakeReadService()).ResolveAsync("imap-work", "Projects/2026");

        Assert.False(resolution.IsAlias);
        Assert.True(resolution.IsSupported);
        Assert.True(resolution.IsResolved);
        Assert.Equal("Projects/2026", resolution.EffectiveFolderId);
        Assert.Equal("Projects/2026 [explicit]", resolution.Summary);
    }

    [Fact]
    public async Task FolderAliasServiceResolvesKnownAliasesToEffectiveFolderTargets() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        await store.SaveAsync(new MailProfile {
            Id = "graph-work",
            DisplayName = "Graph Work",
            Kind = MailProfileKind.Graph,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.ClientId] = "client-id",
                [MailProfileSettingsKeys.TenantId] = "tenant-id",
                [MailProfileSettingsKeys.Mailbox] = "shared@example.com"
            }
        });

        var resolution = await new MailFolderAliasService(store, new FakeReadService()).ResolveAsync("graph-work", "archive", "shared@example.com");

        Assert.True(resolution.IsAlias);
        Assert.True(resolution.IsSupported);
        Assert.True(resolution.IsResolved);
        Assert.Equal(MailFolderAliases.Archive, resolution.Alias);
        Assert.Equal("archive", resolution.EffectiveFolderId);
        Assert.Equal("Archive -> Archive", resolution.Summary);
    }

    private static string CreateTemporaryFilePath(string fileName) {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    private sealed class FakeReadService : IMailReadService {
        public Task<IReadOnlyList<FolderRef>> GetFoldersAsync(MailFolderQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FolderRef>>(new[] {
                new FolderRef {
                    ProfileId = query.ProfileId,
                    MailboxId = query.MailboxId,
                    Id = "inbox",
                    DisplayName = "Inbox",
                    Path = "Inbox",
                    SpecialUse = "inbox"
                },
                new FolderRef {
                    ProfileId = query.ProfileId,
                    MailboxId = query.MailboxId,
                    Id = "archive",
                    DisplayName = "Archive",
                    Path = "Archive",
                    SpecialUse = "archive"
                },
                new FolderRef {
                    ProfileId = query.ProfileId,
                    MailboxId = query.MailboxId,
                    Id = "trash",
                    DisplayName = "Trash",
                    Path = "Trash",
                    SpecialUse = "trash"
                }
            });

        public Task<IReadOnlyList<FolderRefCompact>> GetFoldersCompactAsync(MailFolderQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FolderRefCompact>>(Array.Empty<FolderRefCompact>());

        public Task<IReadOnlyList<MessageSummary>> SearchAsync(MailSearchRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MessageSummary>>(Array.Empty<MessageSummary>());

        public Task<IReadOnlyList<MessageSummaryCompact>> SearchCompactAsync(MailSearchRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MessageSummaryCompact>>(Array.Empty<MessageSummaryCompact>());

        public Task<IReadOnlyList<AttachmentSummary>> GetAttachmentsAsync(ListAttachmentsRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AttachmentSummary>>(Array.Empty<AttachmentSummary>());

        public Task<MessageDetail?> GetMessageAsync(GetMessageRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<MessageDetail?>(null);

        public Task<IReadOnlyList<MessageDetail>> GetMessagesAsync(GetMessagesRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MessageDetail>>(Array.Empty<MessageDetail>());

        public Task<MessageDetailCompact?> GetMessageCompactAsync(GetMessageRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<MessageDetailCompact?>(null);

        public Task<IReadOnlyList<MessageDetailCompact>> GetMessagesCompactAsync(GetMessagesRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MessageDetailCompact>>(Array.Empty<MessageDetailCompact>());

        public Task<SaveAttachmentsResult> SaveAttachmentsAsync(SaveAttachmentsRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SaveAttachmentsResult());

        public Task<SaveAttachmentsManyResult> SaveAttachmentsManyAsync(SaveAttachmentsManyRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SaveAttachmentsManyResult());

        public Task<OperationResult> SaveAttachmentAsync(SaveAttachmentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success());
    }

    private sealed class ThrowingReadService : IMailReadService {
        public Task<IReadOnlyList<FolderRef>> GetFoldersAsync(MailFolderQuery query, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Lookup failed.");

        public Task<IReadOnlyList<FolderRefCompact>> GetFoldersCompactAsync(MailFolderQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FolderRefCompact>>(Array.Empty<FolderRefCompact>());

        public Task<IReadOnlyList<MessageSummary>> SearchAsync(MailSearchRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MessageSummary>>(Array.Empty<MessageSummary>());

        public Task<IReadOnlyList<MessageSummaryCompact>> SearchCompactAsync(MailSearchRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MessageSummaryCompact>>(Array.Empty<MessageSummaryCompact>());

        public Task<IReadOnlyList<AttachmentSummary>> GetAttachmentsAsync(ListAttachmentsRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AttachmentSummary>>(Array.Empty<AttachmentSummary>());

        public Task<MessageDetail?> GetMessageAsync(GetMessageRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<MessageDetail?>(null);

        public Task<IReadOnlyList<MessageDetail>> GetMessagesAsync(GetMessagesRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MessageDetail>>(Array.Empty<MessageDetail>());

        public Task<MessageDetailCompact?> GetMessageCompactAsync(GetMessageRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<MessageDetailCompact?>(null);

        public Task<IReadOnlyList<MessageDetailCompact>> GetMessagesCompactAsync(GetMessagesRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MessageDetailCompact>>(Array.Empty<MessageDetailCompact>());

        public Task<SaveAttachmentsResult> SaveAttachmentsAsync(SaveAttachmentsRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SaveAttachmentsResult());

        public Task<SaveAttachmentsManyResult> SaveAttachmentsManyAsync(SaveAttachmentsManyRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SaveAttachmentsManyResult());

        public Task<OperationResult> SaveAttachmentAsync(SaveAttachmentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success());
    }
}