#pragma warning disable CS1591
#pragma warning disable CS8600,CS8601,CS8602,CS8603,CS8604,CS8618,CS8625
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;

namespace Mailozaurr;

public static class ImapSentFolderResolver {
    public sealed class ImapFolderInfo {
        public string FullName { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public bool IsSent { get; init; }
        public bool IsTrash { get; init; }
        public bool IsArchive { get; init; }
        public bool IsJunk { get; init; }
        public bool IsDrafts { get; init; }
    }

    public sealed class ImapSpecialFolderMappings {
        public string Inbox { get; init; } = "INBOX";
        public string Sent { get; init; } = "Sent";
        public string? Drafts { get; init; }
        public string? Trash { get; init; }
        public string? Archive { get; init; }
        public string? Junk { get; init; }
    }

    public static async Task<IReadOnlyList<string>> ListFoldersAsync(ImapClient client, CancellationToken cancellationToken) {
        var folders = await ListFoldersInfoAsync(client, cancellationToken).ConfigureAwait(false);
        var output = new List<string>(folders.Count);
        foreach (var folder in folders) {
            if (!string.IsNullOrWhiteSpace(folder.FullName)) {
                output.Add(folder.FullName);
            }
        }
        return output;
    }

    public static Task<List<ImapFolderInfo>> ListFoldersInfoAsync(ImapClient client, CancellationToken cancellationToken) {
        if (client is null) {
            throw new ArgumentNullException(nameof(client));
        }
        return GetFoldersInfoAsync(client, cancellationToken);
    }

    public static async Task<string> ResolveSentFolderNameAsync(
        ImapClient client,
        string? requestedFolder,
        string? configuredFolder,
        string fallbackFolder,
        CancellationToken cancellationToken) {
        if (client is null) {
            throw new ArgumentNullException(nameof(client));
        }

        var requested = NormalizeOptionalFolder(requestedFolder);
        if (!string.IsNullOrWhiteSpace(requested)) {
            return requested;
        }

        var configured = NormalizeOptionalFolder(configuredFolder);
        if (!string.IsNullOrWhiteSpace(configured)) {
            return configured;
        }

        try {
            var folders = await ListFoldersInfoAsync(client, cancellationToken).ConfigureAwait(false);
            return ResolveSentFolderName(
                requestedFolder: null,
                configuredFolder: null,
                folders: folders,
                fallbackFolder: fallbackFolder);
        } catch {
            return string.IsNullOrWhiteSpace(fallbackFolder) ? "Sent" : fallbackFolder.Trim();
        }
    }

    public static string ResolveSentFolderName(
        string? requestedFolder,
        string? configuredFolder,
        IEnumerable<ImapFolderInfo>? folders,
        string fallbackFolder = "Sent") {
        var resolved = ResolveSpecialFolderName(
            requestedFolder: requestedFolder,
            configuredFolder: configuredFolder,
            folders: folders,
            attributePredicate: f => f.IsSent,
            looksLikePredicate: LooksLikeSentFolder,
            heuristicScoreSelector: SentFolderHeuristicScore,
            fallbackFolder: fallbackFolder);
        return resolved ?? (string.IsNullOrWhiteSpace(fallbackFolder) ? "Sent" : fallbackFolder.Trim());
    }

    public static string? ResolveDraftsFolderName(
        string? requestedFolder,
        string? configuredFolder,
        IEnumerable<ImapFolderInfo>? folders,
        string? fallbackFolder = null) {
        return ResolveSpecialFolderName(
            requestedFolder: requestedFolder,
            configuredFolder: configuredFolder,
            folders: folders,
            attributePredicate: f => f.IsDrafts,
            looksLikePredicate: LooksLikeDraftsFolder,
            heuristicScoreSelector: DraftsFolderHeuristicScore,
            fallbackFolder: fallbackFolder);
    }

    public static string? ResolveTrashFolderName(
        string? requestedFolder,
        string? configuredFolder,
        IEnumerable<ImapFolderInfo>? folders,
        string? fallbackFolder = null) {
        return ResolveSpecialFolderName(
            requestedFolder: requestedFolder,
            configuredFolder: configuredFolder,
            folders: folders,
            attributePredicate: f => f.IsTrash,
            looksLikePredicate: LooksLikeTrashFolder,
            heuristicScoreSelector: TrashFolderHeuristicScore,
            fallbackFolder: fallbackFolder);
    }

    public static string? ResolveArchiveFolderName(
        string? requestedFolder,
        string? configuredFolder,
        IEnumerable<ImapFolderInfo>? folders,
        string? fallbackFolder = null) {
        return ResolveSpecialFolderName(
            requestedFolder: requestedFolder,
            configuredFolder: configuredFolder,
            folders: folders,
            attributePredicate: f => f.IsArchive,
            looksLikePredicate: LooksLikeArchiveFolder,
            heuristicScoreSelector: ArchiveFolderHeuristicScore,
            fallbackFolder: fallbackFolder);
    }

    public static string? ResolveJunkFolderName(
        string? requestedFolder,
        string? configuredFolder,
        IEnumerable<ImapFolderInfo>? folders,
        string? fallbackFolder = null) {
        return ResolveSpecialFolderName(
            requestedFolder: requestedFolder,
            configuredFolder: configuredFolder,
            folders: folders,
            attributePredicate: f => f.IsJunk,
            looksLikePredicate: LooksLikeJunkFolder,
            heuristicScoreSelector: JunkFolderHeuristicScore,
            fallbackFolder: fallbackFolder);
    }

    public static ImapSpecialFolderMappings ResolveSpecialFolderMappings(
        string? configuredInboxFolder,
        string? configuredSentFolder,
        string? configuredDraftsFolder,
        string? configuredTrashFolder,
        string? configuredArchiveFolder,
        string? configuredJunkFolder,
        IEnumerable<ImapFolderInfo>? folders,
        string? configuredImapFolder) {
        var inboxFallback = ResolveFolderName(configuredInboxFolder, configuredImapFolder, "INBOX");
        return new ImapSpecialFolderMappings {
            Inbox = inboxFallback,
            Sent = ResolveSentFolderName(
                requestedFolder: null,
                configuredFolder: configuredSentFolder,
                folders: folders,
                fallbackFolder: "Sent"),
            Drafts = ResolveDraftsFolderName(
                requestedFolder: null,
                configuredFolder: configuredDraftsFolder,
                folders: folders),
            Trash = ResolveTrashFolderName(
                requestedFolder: null,
                configuredFolder: configuredTrashFolder,
                folders: folders),
            Archive = ResolveArchiveFolderName(
                requestedFolder: null,
                configuredFolder: configuredArchiveFolder,
                folders: folders),
            Junk = ResolveJunkFolderName(
                requestedFolder: null,
                configuredFolder: configuredJunkFolder,
                folders: folders)
        };
    }

    private static async Task<List<ImapFolderInfo>> GetFoldersInfoAsync(ImapClient client, CancellationToken cancellationToken) {
        var output = new List<ImapFolderInfo>();
        if (client.PersonalNamespaces.Count > 0) {
            foreach (var ns in client.PersonalNamespaces) {
                var root = client.GetFolder(ns);
                await CollectFoldersInfoAsync(root, output, cancellationToken).ConfigureAwait(false);
            }
        } else {
            await CollectFoldersInfoAsync(client.Inbox, output, cancellationToken).ConfigureAwait(false);
        }
        return output;
    }

    private static async Task CollectFoldersInfoAsync(IMailFolder folder, List<ImapFolderInfo> output, CancellationToken cancellationToken) {
        if (folder is null) {
            return;
        }

        try {
            var full = folder.FullName ?? folder.Name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(full) &&
                !output.Exists(f => string.Equals(f.FullName, full, StringComparison.OrdinalIgnoreCase))) {
                output.Add(new ImapFolderInfo {
                    FullName = full,
                    Name = folder.Name ?? full,
                    IsSent = folder.Attributes.HasFlag(FolderAttributes.Sent),
                    IsTrash = folder.Attributes.HasFlag(FolderAttributes.Trash),
                    IsArchive = folder.Attributes.HasFlag(FolderAttributes.Archive),
                    IsJunk = folder.Attributes.HasFlag(FolderAttributes.Junk),
                    IsDrafts = folder.Attributes.HasFlag(FolderAttributes.Drafts)
                });
            }
        } catch {
            // best-effort
        }

        IList<IMailFolder> subfolders;
        try {
            subfolders = await folder.GetSubfoldersAsync(false, cancellationToken).ConfigureAwait(false);
        } catch {
            return;
        }

        foreach (var subfolder in subfolders) {
            await CollectFoldersInfoAsync(subfolder, output, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string? ResolveSpecialFolderName(
        string? requestedFolder,
        string? configuredFolder,
        IEnumerable<ImapFolderInfo>? folders,
        Func<ImapFolderInfo, bool> attributePredicate,
        Func<string?, bool> looksLikePredicate,
        Func<ImapFolderInfo, int> heuristicScoreSelector,
        string? fallbackFolder) {
        var requested = NormalizeOptionalFolder(requestedFolder);
        if (!string.IsNullOrWhiteSpace(requested)) {
            return requested;
        }

        var configured = NormalizeOptionalFolder(configuredFolder);
        if (!string.IsNullOrWhiteSpace(configured)) {
            return configured;
        }

        var available = folders?
            .Where(f => !string.IsNullOrWhiteSpace(f.FullName))
            .Select(f => new ImapFolderInfo {
                FullName = f.FullName.Trim(),
                Name = (f.Name ?? string.Empty).Trim(),
                IsSent = f.IsSent,
                IsTrash = f.IsTrash,
                IsArchive = f.IsArchive,
                IsJunk = f.IsJunk,
                IsDrafts = f.IsDrafts
            })
            .ToList() ?? new List<ImapFolderInfo>();

        if (available.Count == 0) {
            return NormalizeOptionalFolder(fallbackFolder);
        }

        var byAttribute = available.FirstOrDefault(attributePredicate);
        if (byAttribute is not null) {
            return byAttribute.FullName;
        }

        var byHeuristic = available
            .Where(f => looksLikePredicate(f.Name) || looksLikePredicate(f.FullName))
            .OrderBy(heuristicScoreSelector)
            .ThenBy(f => f.FullName.Length)
            .FirstOrDefault();
        if (byHeuristic is not null) {
            return byHeuristic.FullName;
        }

        return NormalizeOptionalFolder(fallbackFolder);
    }

    private static string ResolveFolderName(string? configuredInboxFolder, string? configuredImapFolder, string fallbackFolder) {
        var inbox = NormalizeOptionalFolder(configuredInboxFolder);
        if (!string.IsNullOrWhiteSpace(inbox)) {
            return inbox;
        }
        var configured = NormalizeOptionalFolder(configuredImapFolder);
        if (!string.IsNullOrWhiteSpace(configured)) {
            return configured;
        }
        return fallbackFolder;
    }

    private static int SentFolderHeuristicScore(ImapFolderInfo folder) {
        var name = (folder.Name ?? string.Empty).Trim().ToLowerInvariant();
        var full = (folder.FullName ?? string.Empty).Trim().ToLowerInvariant();
        if (name == "sent" || full == "sent" || full.EndsWith("/sent", StringComparison.Ordinal) || full.EndsWith(".sent", StringComparison.Ordinal)) return 0;
        if (name is "sent items" or "sent mail" || full.Contains("/sent items", StringComparison.Ordinal) || full.Contains("/sent mail", StringComparison.Ordinal)) return 1;
        if (full.Contains("sent", StringComparison.Ordinal) || name.Contains("sent", StringComparison.Ordinal)) return 2;
        return 1000;
    }

    private static int DraftsFolderHeuristicScore(ImapFolderInfo folder) {
        var name = (folder.Name ?? string.Empty).Trim().ToLowerInvariant();
        var full = (folder.FullName ?? string.Empty).Trim().ToLowerInvariant();
        if (name == "drafts" || full == "drafts" || full.EndsWith("/drafts", StringComparison.Ordinal) || full.EndsWith(".drafts", StringComparison.Ordinal)) return 0;
        if (name == "draft" || full.EndsWith("/draft", StringComparison.Ordinal) || full.EndsWith(".draft", StringComparison.Ordinal)) return 1;
        if (name.Contains("draft", StringComparison.Ordinal) || full.Contains("draft", StringComparison.Ordinal)) return 2;
        return 1000;
    }

    private static int TrashFolderHeuristicScore(ImapFolderInfo folder) {
        var name = (folder.Name ?? string.Empty).Trim().ToLowerInvariant();
        var full = (folder.FullName ?? string.Empty).Trim().ToLowerInvariant();
        if (name == "trash" || name == "bin" || full == "trash" || full.EndsWith("/trash", StringComparison.Ordinal) || full.EndsWith(".trash", StringComparison.Ordinal)) return 0;
        if (name is "deleted items" or "deleted messages" || full.Contains("/deleted items", StringComparison.Ordinal) || full.Contains("/deleted messages", StringComparison.Ordinal)) return 1;
        if (name.Contains("trash", StringComparison.Ordinal) || full.Contains("trash", StringComparison.Ordinal) ||
            name.Contains("deleted", StringComparison.Ordinal) || full.Contains("deleted", StringComparison.Ordinal) ||
            name.Contains("bin", StringComparison.Ordinal) || full.Contains("/bin", StringComparison.Ordinal)) return 2;
        return 1000;
    }

    private static int ArchiveFolderHeuristicScore(ImapFolderInfo folder) {
        var name = (folder.Name ?? string.Empty).Trim().ToLowerInvariant();
        var full = (folder.FullName ?? string.Empty).Trim().ToLowerInvariant();
        if (name == "archive" || full == "archive" || full.EndsWith("/archive", StringComparison.Ordinal) || full.EndsWith(".archive", StringComparison.Ordinal)) return 0;
        if (name == "all mail" || full.EndsWith("/all mail", StringComparison.Ordinal) || full.EndsWith(".all mail", StringComparison.Ordinal)) return 1;
        if (name.Contains("archive", StringComparison.Ordinal) || full.Contains("archive", StringComparison.Ordinal) ||
            name.Contains("all mail", StringComparison.Ordinal) || full.Contains("all mail", StringComparison.Ordinal)) return 2;
        return 1000;
    }

    private static int JunkFolderHeuristicScore(ImapFolderInfo folder) {
        var name = (folder.Name ?? string.Empty).Trim().ToLowerInvariant();
        var full = (folder.FullName ?? string.Empty).Trim().ToLowerInvariant();
        if (name == "junk" || name == "spam" || full == "junk" || full.EndsWith("/junk", StringComparison.Ordinal) || full.EndsWith(".junk", StringComparison.Ordinal)) return 0;
        if (name == "junk e-mail" || full.Contains("/junk e-mail", StringComparison.Ordinal)) return 1;
        if (name.Contains("junk", StringComparison.Ordinal) || full.Contains("junk", StringComparison.Ordinal) ||
            name.Contains("spam", StringComparison.Ordinal) || full.Contains("spam", StringComparison.Ordinal) ||
            name.Contains("bulk", StringComparison.Ordinal) || full.Contains("bulk", StringComparison.Ordinal)) return 2;
        return 1000;
    }

    private static bool LooksLikeSentFolder(string? value) {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var lower = value.Trim().ToLowerInvariant();
        if (lower.Contains("draft", StringComparison.Ordinal) ||
            lower.Contains("trash", StringComparison.Ordinal) ||
            lower.Contains("junk", StringComparison.Ordinal) ||
            lower.Contains("spam", StringComparison.Ordinal) ||
            lower.Contains("archive", StringComparison.Ordinal)) {
            return false;
        }
        return lower.Contains("sent", StringComparison.Ordinal);
    }

    private static bool LooksLikeDraftsFolder(string? value) {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var lower = value.Trim().ToLowerInvariant();
        if (lower.Contains("sent", StringComparison.Ordinal) ||
            lower.Contains("trash", StringComparison.Ordinal) ||
            lower.Contains("junk", StringComparison.Ordinal) ||
            lower.Contains("spam", StringComparison.Ordinal) ||
            lower.Contains("archive", StringComparison.Ordinal)) {
            return false;
        }
        return lower.Contains("draft", StringComparison.Ordinal);
    }

    private static bool LooksLikeTrashFolder(string? value) {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var lower = value.Trim().ToLowerInvariant();
        if (lower.Contains("sent", StringComparison.Ordinal) ||
            lower.Contains("draft", StringComparison.Ordinal) ||
            lower.Contains("junk", StringComparison.Ordinal) ||
            lower.Contains("spam", StringComparison.Ordinal) ||
            lower.Contains("archive", StringComparison.Ordinal)) {
            return false;
        }
        return lower.Contains("trash", StringComparison.Ordinal) ||
               lower.Contains("deleted", StringComparison.Ordinal) ||
               lower.Contains("bin", StringComparison.Ordinal) ||
               lower.Contains("waste", StringComparison.Ordinal);
    }

    private static bool LooksLikeArchiveFolder(string? value) {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var lower = value.Trim().ToLowerInvariant();
        if (lower.Contains("sent", StringComparison.Ordinal) ||
            lower.Contains("draft", StringComparison.Ordinal) ||
            lower.Contains("trash", StringComparison.Ordinal) ||
            lower.Contains("junk", StringComparison.Ordinal) ||
            lower.Contains("spam", StringComparison.Ordinal)) {
            return false;
        }
        return lower.Contains("archive", StringComparison.Ordinal) || lower.Contains("all mail", StringComparison.Ordinal);
    }

    private static bool LooksLikeJunkFolder(string? value) {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var lower = value.Trim().ToLowerInvariant();
        if (lower.Contains("sent", StringComparison.Ordinal) ||
            lower.Contains("draft", StringComparison.Ordinal) ||
            lower.Contains("trash", StringComparison.Ordinal) ||
            lower.Contains("archive", StringComparison.Ordinal)) {
            return false;
        }
        return lower.Contains("junk", StringComparison.Ordinal) ||
               lower.Contains("spam", StringComparison.Ordinal) ||
               lower.Contains("bulk", StringComparison.Ordinal);
    }

    private static string? NormalizeOptionalFolder(string? value) {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
