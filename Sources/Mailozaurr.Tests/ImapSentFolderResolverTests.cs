using System.Collections.Generic;
using Xunit;

namespace Mailozaurr.Tests;

public class ImapSentFolderResolverTests {
    [Fact]
    public void ResolveSentFolderName_PrefersConfiguredFolder() {
        var folders = new List<ImapSentFolderResolver.ImapFolderInfo> {
            new() { FullName = "INBOX", Name = "INBOX" },
            new() { FullName = "Sent", Name = "Sent", IsSent = true }
        };

        var resolved = ImapSentFolderResolver.ResolveSentFolderName(
            requestedFolder: null,
            configuredFolder: "CustomSent",
            folders: folders,
            fallbackFolder: "Sent");

        Assert.Equal("CustomSent", resolved);
    }

    [Fact]
    public void ResolveSentFolderName_UsesHeuristic_WhenNoConfiguredFolder() {
        var folders = new List<ImapSentFolderResolver.ImapFolderInfo> {
            new() { FullName = "INBOX", Name = "INBOX" },
            new() { FullName = "Sent Items", Name = "Sent Items" }
        };

        var resolved = ImapSentFolderResolver.ResolveSentFolderName(
            requestedFolder: null,
            configuredFolder: null,
            folders: folders,
            fallbackFolder: "Sent");

        Assert.Equal("Sent Items", resolved);
    }

    [Fact]
    public void ResolveSpecialFolderMappings_AppliesConfiguredOverrides() {
        var folders = new List<ImapSentFolderResolver.ImapFolderInfo> {
            new() { FullName = "INBOX", Name = "INBOX" },
            new() { FullName = "Sent", Name = "Sent", IsSent = true },
            new() { FullName = "Drafts", Name = "Drafts", IsDrafts = true },
            new() { FullName = "Trash", Name = "Trash", IsTrash = true }
        };

        var mappings = ImapSentFolderResolver.ResolveSpecialFolderMappings(
            configuredInboxFolder: "InboxOverride",
            configuredSentFolder: "SentOverride",
            configuredDraftsFolder: "DraftsOverride",
            configuredTrashFolder: null,
            configuredArchiveFolder: null,
            configuredJunkFolder: null,
            folders: folders,
            configuredImapFolder: null);

        Assert.Equal("InboxOverride", mappings.Inbox);
        Assert.Equal("SentOverride", mappings.Sent);
        Assert.Equal("DraftsOverride", mappings.Drafts);
        Assert.Equal("Trash", mappings.Trash);
    }
}
