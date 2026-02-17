using System.Collections.Generic;
using Xunit;

namespace Mailozaurr.Tests;

public sealed class ImapSentFolderResolverTests {
    [Fact]
    public void ResolveSentFolderName_UsesRequestedFolder_WhenProvided() {
        var resolved = ImapSentFolderResolver.ResolveSentFolderName(
            requestedFolder: " Custom/Sent ",
            folders: new[] {
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "Sent", Name = "Sent", IsSent = true }
            });

        Assert.Equal("Custom/Sent", resolved);
    }

    [Fact]
    public void ResolveSentFolderName_UsesConfiguredFolder_WhenRequestedIsMissing() {
        var resolved = ImapSentFolderResolver.ResolveSentFolderName(
            requestedFolder: null,
            configuredFolder: " Team/Sent ",
            folders: new[] {
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "Sent", Name = "Sent", IsSent = true }
            });

        Assert.Equal("Team/Sent", resolved);
    }

    [Fact]
    public void ResolveSentFolderName_PrefersAttributeTaggedSentFolder() {
        var resolved = ImapSentFolderResolver.ResolveSentFolderName(
            requestedFolder: null,
            configuredFolder: null,
            folders: new[] {
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "INBOX", Name = "INBOX" },
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "Sent Items", Name = "Sent Items", IsSent = true }
            });

        Assert.Equal("Sent Items", resolved);
    }

    [Fact]
    public void ResolveSentFolderName_UsesSentHeuristic_WhenNoAttributeMatch() {
        var resolved = ImapSentFolderResolver.ResolveSentFolderName(
            requestedFolder: null,
            configuredFolder: null,
            folders: new[] {
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "INBOX", Name = "INBOX" },
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "Sent Mail", Name = "Sent Mail" }
            });

        Assert.Equal("Sent Mail", resolved);
    }

    [Fact]
    public void ResolveSentFolderName_IgnoresArchiveAndDraftLikeNames() {
        var resolved = ImapSentFolderResolver.ResolveSentFolderName(
            requestedFolder: null,
            configuredFolder: null,
            folders: new[] {
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "Archive", Name = "Archive" },
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "Draft Sent Ideas", Name = "Draft Sent Ideas" },
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "Sent", Name = "Sent" }
            });

        Assert.Equal("Sent", resolved);
    }

    [Fact]
    public void ResolveSentFolderName_UsesFallback_WhenNoCandidatesExist() {
        var resolved = ImapSentFolderResolver.ResolveSentFolderName(
            requestedFolder: null,
            configuredFolder: null,
            folders: new List<ImapSentFolderResolver.ImapFolderInfo>(),
            fallbackFolder: "Sent");

        Assert.Equal("Sent", resolved);
    }

    [Fact]
    public void ResolveDraftsFolderName_PrefersConfiguredOverride_WhenProvided() {
        var resolved = ImapSentFolderResolver.ResolveDraftsFolderName(
            requestedFolder: null,
            configuredFolder: " Team Drafts ",
            folders: new[] {
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "Drafts", Name = "Drafts", IsDrafts = true }
            });

        Assert.Equal("Team Drafts", resolved);
    }

    [Fact]
    public void ResolveDraftsFolderName_UsesHeuristic_WhenNoAttributeFlag() {
        var resolved = ImapSentFolderResolver.ResolveDraftsFolderName(
            requestedFolder: null,
            configuredFolder: null,
            folders: new[] {
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "INBOX", Name = "INBOX" },
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "[Gmail]/Drafts", Name = "Drafts" }
            });

        Assert.Equal("[Gmail]/Drafts", resolved);
    }

    [Fact]
    public void ResolveTrashFolderName_UsesSpecialUseFlag() {
        var resolved = ImapSentFolderResolver.ResolveTrashFolderName(
            requestedFolder: null,
            configuredFolder: null,
            folders: new[] {
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "Deleted Items", Name = "Deleted Items", IsTrash = true },
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "Junk", Name = "Junk", IsJunk = true }
            });

        Assert.Equal("Deleted Items", resolved);
    }

    [Fact]
    public void ResolveArchiveFolderName_UsesHeuristic() {
        var resolved = ImapSentFolderResolver.ResolveArchiveFolderName(
            requestedFolder: null,
            configuredFolder: null,
            folders: new[] {
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "INBOX", Name = "INBOX" },
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "[Gmail]/All Mail", Name = "All Mail" }
            });

        Assert.Equal("[Gmail]/All Mail", resolved);
    }

    [Fact]
    public void ResolveJunkFolderName_UsesConfiguredOverride_WhenProvided() {
        var resolved = ImapSentFolderResolver.ResolveJunkFolderName(
            requestedFolder: null,
            configuredFolder: "Spam Box",
            folders: new[] {
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "Junk", Name = "Junk", IsJunk = true }
            });

        Assert.Equal("Spam Box", resolved);
    }

    [Fact]
    public void ResolveSpecialFolderMappings_UsesOverridesBeforeDetection() {
        var mappings = ImapSentFolderResolver.ResolveSpecialFolderMappings(
            configuredInboxFolder: "INBOX.Client",
            configuredSentFolder: "Sent.Custom",
            configuredDraftsFolder: "Drafts.Custom",
            configuredTrashFolder: null,
            configuredArchiveFolder: null,
            configuredJunkFolder: null,
            folders: new[] {
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "Inbox", Name = "Inbox" },
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "Sent Items", Name = "Sent Items", IsSent = true },
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "Drafts", Name = "Drafts", IsDrafts = true },
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "Deleted Items", Name = "Deleted Items", IsTrash = true },
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "Archive", Name = "Archive", IsArchive = true },
                new ImapSentFolderResolver.ImapFolderInfo { FullName = "Spam", Name = "Spam", IsJunk = true }
            },
            configuredImapFolder: "INBOX");

        Assert.Equal("INBOX.Client", mappings.Inbox);
        Assert.Equal("Sent.Custom", mappings.Sent);
        Assert.Equal("Drafts.Custom", mappings.Drafts);
        Assert.Equal("Deleted Items", mappings.Trash);
        Assert.Equal("Archive", mappings.Archive);
        Assert.Equal("Spam", mappings.Junk);
    }
}
