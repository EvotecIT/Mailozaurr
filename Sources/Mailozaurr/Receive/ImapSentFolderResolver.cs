using MailKit;
using MailKit.Net.Imap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Resolves IMAP Sent-folder names using explicit overrides, server attributes, and name heuristics.
/// </summary>
public static class ImapSentFolderResolver {
    /// <summary>
    /// Lightweight IMAP folder metadata used by resolver heuristics.
    /// </summary>
    public sealed class ImapFolderInfo {
        /// <summary>Folder full name as used by IMAP.</summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>Folder display name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>True when folder has the IMAP <see cref="FolderAttributes.Sent"/> attribute.</summary>
        public bool IsSent { get; set; }

        /// <summary>True when folder has the IMAP <see cref="FolderAttributes.Trash"/> attribute.</summary>
        public bool IsTrash { get; set; }

        /// <summary>True when folder has the IMAP <see cref="FolderAttributes.Archive"/> attribute.</summary>
        public bool IsArchive { get; set; }

        /// <summary>True when folder has the IMAP <see cref="FolderAttributes.Junk"/> attribute.</summary>
        public bool IsJunk { get; set; }

        /// <summary>True when folder has the IMAP <see cref="FolderAttributes.Drafts"/> attribute.</summary>
        public bool IsDrafts { get; set; }
    }

    /// <summary>
    /// Resolved special-folder mappings for IMAP.
    /// </summary>
    public sealed class ImapSpecialFolderMappings {
        /// <summary>Inbox folder name.</summary>
        public string Inbox { get; set; } = "INBOX";

        /// <summary>Sent folder name.</summary>
        public string Sent { get; set; } = "Sent";

        /// <summary>Drafts folder name, when resolved.</summary>
        public string? Drafts { get; set; }

        /// <summary>Trash folder name, when resolved.</summary>
        public string? Trash { get; set; }

        /// <summary>Archive folder name, when resolved.</summary>
        public string? Archive { get; set; }

        /// <summary>Junk folder name, when resolved.</summary>
        public string? Junk { get; set; }
    }

    /// <summary>
    /// Resolves Sent folder name by enumerating folders from an IMAP client.
    /// </summary>
    public static async Task<string> ResolveSentFolderNameAsync(
        ImapClient client,
        string? requestedFolder,
        string? configuredFolder = null,
        string fallbackFolder = "Sent",
        CancellationToken cancellationToken = default) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }

        var requested = NormalizeOptionalFolder(requestedFolder);
        if (requested != null) {
            return requested;
        }

        var configured = NormalizeOptionalFolder(configuredFolder);
        if (configured != null) {
            return configured;
        }

        try {
            var folders = await GetFoldersInfoAsync(client, cancellationToken).ConfigureAwait(false);
            return ResolveSentFolderName(
                requestedFolder: null,
                configuredFolder: null,
                folders: folders,
                fallbackFolder: fallbackFolder);
        } catch {
            // Best-effort fallback for servers/providers that fail folder enumeration.
            return NormalizeOptionalFolder(fallbackFolder) ?? "Sent";
        }
    }

    /// <summary>
    /// Resolves Sent folder name from known folder metadata.
    /// </summary>
    public static string ResolveSentFolderName(
        string? requestedFolder,
        IEnumerable<ImapFolderInfo>? folders,
        string fallbackFolder = "Sent") {
        return ResolveSentFolderName(
            requestedFolder: requestedFolder,
            configuredFolder: null,
            folders: folders,
            fallbackFolder: fallbackFolder);
    }

    /// <summary>
    /// Resolves Sent folder name from known folder metadata.
    /// </summary>
    public static string ResolveSentFolderName(
        string? requestedFolder,
        string? configuredFolder,
        IEnumerable<ImapFolderInfo>? folders,
        string fallbackFolder = "Sent") {
        var resolved = ResolveSpecialFolderName(
            requestedFolder: requestedFolder,
            configuredFolder: configuredFolder,
            folders: folders,
            attributePredicate: x => x.IsSent,
            looksLikePredicate: LooksLikeSentFolder,
            heuristicScoreSelector: SentFolderHeuristicScore,
            fallbackFolder: fallbackFolder);
        return resolved ?? ResolveFolderName(null, null, fallbackFolder);
    }

    /// <summary>
    /// Resolves Drafts folder name from known folder metadata.
    /// </summary>
    public static string? ResolveDraftsFolderName(
        string? requestedFolder,
        string? configuredFolder,
        IEnumerable<ImapFolderInfo>? folders,
        string? fallbackFolder = null) {
        return ResolveSpecialFolderName(
            requestedFolder: requestedFolder,
            configuredFolder: configuredFolder,
            folders: folders,
            attributePredicate: x => x.IsDrafts,
            looksLikePredicate: LooksLikeDraftsFolder,
            heuristicScoreSelector: DraftsFolderHeuristicScore,
            fallbackFolder: fallbackFolder);
    }

    /// <summary>
    /// Resolves Trash folder name from known folder metadata.
    /// </summary>
    public static string? ResolveTrashFolderName(
        string? requestedFolder,
        string? configuredFolder,
        IEnumerable<ImapFolderInfo>? folders,
        string? fallbackFolder = null) {
        return ResolveSpecialFolderName(
            requestedFolder: requestedFolder,
            configuredFolder: configuredFolder,
            folders: folders,
            attributePredicate: x => x.IsTrash,
            looksLikePredicate: LooksLikeTrashFolder,
            heuristicScoreSelector: TrashFolderHeuristicScore,
            fallbackFolder: fallbackFolder);
    }

    /// <summary>
    /// Resolves Archive folder name from known folder metadata.
    /// </summary>
    public static string? ResolveArchiveFolderName(
        string? requestedFolder,
        string? configuredFolder,
        IEnumerable<ImapFolderInfo>? folders,
        string? fallbackFolder = null) {
        return ResolveSpecialFolderName(
            requestedFolder: requestedFolder,
            configuredFolder: configuredFolder,
            folders: folders,
            attributePredicate: x => x.IsArchive,
            looksLikePredicate: LooksLikeArchiveFolder,
            heuristicScoreSelector: ArchiveFolderHeuristicScore,
            fallbackFolder: fallbackFolder);
    }

    /// <summary>
    /// Resolves Junk folder name from known folder metadata.
    /// </summary>
    public static string? ResolveJunkFolderName(
        string? requestedFolder,
        string? configuredFolder,
        IEnumerable<ImapFolderInfo>? folders,
        string? fallbackFolder = null) {
        return ResolveSpecialFolderName(
            requestedFolder: requestedFolder,
            configuredFolder: configuredFolder,
            folders: folders,
            attributePredicate: x => x.IsJunk,
            looksLikePredicate: LooksLikeJunkFolder,
            heuristicScoreSelector: JunkFolderHeuristicScore,
            fallbackFolder: fallbackFolder);
    }

    /// <summary>
    /// Resolves all primary special folders using configuration overrides, attributes, and heuristics.
    /// </summary>
    public static ImapSpecialFolderMappings ResolveSpecialFolderMappings(
        string? configuredInboxFolder,
        string? configuredSentFolder,
        string? configuredDraftsFolder,
        string? configuredTrashFolder,
        string? configuredArchiveFolder,
        string? configuredJunkFolder,
        IEnumerable<ImapFolderInfo>? folders,
        string? configuredImapFolder) {
        var inboxFallback = ResolveFolderName(null, configuredImapFolder, "INBOX");
        return new ImapSpecialFolderMappings {
            Inbox = ResolveFolderName(requestedFolder: null, configuredFolder: configuredInboxFolder, fallbackFolder: inboxFallback),
            Sent = ResolveSentFolderName(requestedFolder: null, configuredFolder: configuredSentFolder, folders: folders, fallbackFolder: "Sent"),
            Drafts = ResolveDraftsFolderName(requestedFolder: null, configuredFolder: configuredDraftsFolder, folders: folders),
            Trash = ResolveTrashFolderName(requestedFolder: null, configuredFolder: configuredTrashFolder, folders: folders),
            Archive = ResolveArchiveFolderName(requestedFolder: null, configuredFolder: configuredArchiveFolder, folders: folders),
            Junk = ResolveJunkFolderName(requestedFolder: null, configuredFolder: configuredJunkFolder, folders: folders)
        };
    }

    /// <summary>
    /// Lists IMAP folders with metadata suitable for special-folder resolution.
    /// </summary>
    public static async Task<IReadOnlyList<ImapFolderInfo>> ListFoldersInfoAsync(
        ImapClient client,
        CancellationToken cancellationToken = default) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }

        var folders = await GetFoldersInfoAsync(client, cancellationToken).ConfigureAwait(false);
        folders.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.OrdinalIgnoreCase));
        return folders;
    }

    /// <summary>
    /// Lists IMAP folder full names.
    /// </summary>
    public static async Task<IReadOnlyList<string>> ListFoldersAsync(
        ImapClient client,
        CancellationToken cancellationToken = default) {
        var folders = await ListFoldersInfoAsync(client, cancellationToken).ConfigureAwait(false);
        var output = new List<string>(folders.Count);
        foreach (var folder in folders) {
            if (string.IsNullOrWhiteSpace(folder.FullName)) {
                continue;
            }

            output.Add(folder.FullName);
        }
        return output;
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
        if (folder == null) {
            return;
        }

        try {
            var full = folder.FullName ?? folder.Name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(full) &&
                !output.Exists(x => string.Equals(x.FullName, full, StringComparison.OrdinalIgnoreCase))) {
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
            // Best effort.
        }

        IList<IMailFolder> subfolders;
        try {
            subfolders = await folder.GetSubfoldersAsync(false, cancellationToken).ConfigureAwait(false);
        } catch {
            return;
        }

        foreach (var sub in subfolders) {
            await CollectFoldersInfoAsync(sub, output, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool LooksLikeSentFolder(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        var lower = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (lower.Length == 0) {
            return false;
        }
        if (lower.Contains("draft", StringComparison.Ordinal) ||
            lower.Contains("trash", StringComparison.Ordinal) ||
            lower.Contains("junk", StringComparison.Ordinal) ||
            lower.Contains("spam", StringComparison.Ordinal) ||
            lower.Contains("archive", StringComparison.Ordinal)) {
            return false;
        }

        var normalized = new string(lower.Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ').ToArray());
        var tokens = normalized
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .Where(token => token.Length > 0);
        foreach (var token in tokens) {
            if (token == "sent" || token == "sentitems" || token == "sentmail") {
                return true;
            }
        }

        return lower.Contains("/sent", StringComparison.Ordinal) ||
               lower.Contains(".sent", StringComparison.Ordinal) ||
               lower.Contains("sent", StringComparison.Ordinal);
    }

    private static int SentFolderHeuristicScore(ImapFolderInfo folder) {
        var name = (folder.Name ?? string.Empty).Trim().ToLowerInvariant();
        var full = (folder.FullName ?? string.Empty).Trim().ToLowerInvariant();

        if (name == "sent" ||
            full == "sent" ||
            full.EndsWith("/sent", StringComparison.Ordinal) ||
            full.EndsWith(".sent", StringComparison.Ordinal)) {
            return 0;
        }

        if (name is "sent items" or "sent mail" ||
            full.Contains("/sent items", StringComparison.Ordinal) ||
            full.Contains("/sent mail", StringComparison.Ordinal)) {
            return 1;
        }

        if (full.Contains("sent", StringComparison.Ordinal) || name.Contains("sent", StringComparison.Ordinal)) {
            return 2;
        }

        return 1000;
    }

    private static int DraftsFolderHeuristicScore(ImapFolderInfo folder) {
        var name = (folder.Name ?? string.Empty).Trim().ToLowerInvariant();
        var full = (folder.FullName ?? string.Empty).Trim().ToLowerInvariant();

        if (name == "drafts" ||
            full == "drafts" ||
            full.EndsWith("/drafts", StringComparison.Ordinal) ||
            full.EndsWith(".drafts", StringComparison.Ordinal)) {
            return 0;
        }
        if (name == "draft" ||
            full.EndsWith("/draft", StringComparison.Ordinal) ||
            full.EndsWith(".draft", StringComparison.Ordinal)) {
            return 1;
        }
        if (name.Contains("draft", StringComparison.Ordinal) || full.Contains("draft", StringComparison.Ordinal)) {
            return 2;
        }

        return 1000;
    }

    private static int TrashFolderHeuristicScore(ImapFolderInfo folder) {
        var name = (folder.Name ?? string.Empty).Trim().ToLowerInvariant();
        var full = (folder.FullName ?? string.Empty).Trim().ToLowerInvariant();

        if (name == "trash" ||
            name == "bin" ||
            full == "trash" ||
            full.EndsWith("/trash", StringComparison.Ordinal) ||
            full.EndsWith(".trash", StringComparison.Ordinal)) {
            return 0;
        }
        if (name is "deleted items" or "deleted messages" ||
            full.Contains("/deleted items", StringComparison.Ordinal) ||
            full.Contains("/deleted messages", StringComparison.Ordinal)) {
            return 1;
        }
        if (name.Contains("trash", StringComparison.Ordinal) || full.Contains("trash", StringComparison.Ordinal) ||
            name.Contains("deleted", StringComparison.Ordinal) || full.Contains("deleted", StringComparison.Ordinal) ||
            name.Contains("bin", StringComparison.Ordinal) || full.Contains("/bin", StringComparison.Ordinal)) {
            return 2;
        }

        return 1000;
    }

    private static int ArchiveFolderHeuristicScore(ImapFolderInfo folder) {
        var name = (folder.Name ?? string.Empty).Trim().ToLowerInvariant();
        var full = (folder.FullName ?? string.Empty).Trim().ToLowerInvariant();

        if (name == "archive" ||
            full == "archive" ||
            full.EndsWith("/archive", StringComparison.Ordinal) ||
            full.EndsWith(".archive", StringComparison.Ordinal)) {
            return 0;
        }
        if (name == "all mail" ||
            full.EndsWith("/all mail", StringComparison.Ordinal) ||
            full.EndsWith(".all mail", StringComparison.Ordinal)) {
            return 1;
        }
        if (name.Contains("archive", StringComparison.Ordinal) || full.Contains("archive", StringComparison.Ordinal) ||
            name.Contains("all mail", StringComparison.Ordinal) || full.Contains("all mail", StringComparison.Ordinal)) {
            return 2;
        }

        return 1000;
    }

    private static int JunkFolderHeuristicScore(ImapFolderInfo folder) {
        var name = (folder.Name ?? string.Empty).Trim().ToLowerInvariant();
        var full = (folder.FullName ?? string.Empty).Trim().ToLowerInvariant();

        if (name == "junk" ||
            name == "spam" ||
            full == "junk" ||
            full.EndsWith("/junk", StringComparison.Ordinal) ||
            full.EndsWith(".junk", StringComparison.Ordinal)) {
            return 0;
        }
        if (name == "junk e-mail" || full.Contains("/junk e-mail", StringComparison.Ordinal)) {
            return 1;
        }
        if (name.Contains("junk", StringComparison.Ordinal) || full.Contains("junk", StringComparison.Ordinal) ||
            name.Contains("spam", StringComparison.Ordinal) || full.Contains("spam", StringComparison.Ordinal) ||
            name.Contains("bulk", StringComparison.Ordinal) || full.Contains("bulk", StringComparison.Ordinal)) {
            return 2;
        }

        return 1000;
    }

    private static bool LooksLikeDraftsFolder(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        var lower = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (lower.Length == 0) {
            return false;
        }
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
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        var lower = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (lower.Length == 0) {
            return false;
        }
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
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        var lower = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (lower.Length == 0) {
            return false;
        }
        if (lower.Contains("sent", StringComparison.Ordinal) ||
            lower.Contains("draft", StringComparison.Ordinal) ||
            lower.Contains("trash", StringComparison.Ordinal) ||
            lower.Contains("junk", StringComparison.Ordinal) ||
            lower.Contains("spam", StringComparison.Ordinal)) {
            return false;
        }

        return lower.Contains("archive", StringComparison.Ordinal) ||
               lower.Contains("all mail", StringComparison.Ordinal);
    }

    private static bool LooksLikeJunkFolder(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        var lower = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (lower.Length == 0) {
            return false;
        }
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

    private static string? ResolveSpecialFolderName(
        string? requestedFolder,
        string? configuredFolder,
        IEnumerable<ImapFolderInfo>? folders,
        Func<ImapFolderInfo, bool> attributePredicate,
        Func<string?, bool> looksLikePredicate,
        Func<ImapFolderInfo, int> heuristicScoreSelector,
        string? fallbackFolder) {
        var requested = NormalizeOptionalFolder(requestedFolder);
        if (requested != null) {
            return requested;
        }

        var configured = NormalizeOptionalFolder(configuredFolder);
        if (configured != null) {
            return configured;
        }

        var available = folders?
            .Where(x => !string.IsNullOrWhiteSpace(x.FullName))
            .Select(x => new ImapFolderInfo {
                FullName = x.FullName.Trim(),
                Name = (x.Name ?? string.Empty).Trim(),
                IsSent = x.IsSent,
                IsTrash = x.IsTrash,
                IsArchive = x.IsArchive,
                IsJunk = x.IsJunk,
                IsDrafts = x.IsDrafts
            })
            .ToList() ?? new List<ImapFolderInfo>();

        if (available.Count == 0) {
            return NormalizeOptionalFolder(fallbackFolder);
        }

        var byAttribute = available.FirstOrDefault(attributePredicate);
        if (byAttribute != null) {
            return byAttribute.FullName;
        }

        var byHeuristic = available
            .Where(x => looksLikePredicate(x.Name) || looksLikePredicate(x.FullName))
            .OrderBy(heuristicScoreSelector)
            .ThenBy(x => x.FullName.Length)
            .FirstOrDefault();
        if (byHeuristic != null) {
            return byHeuristic.FullName;
        }

        return NormalizeOptionalFolder(fallbackFolder);
    }

    private static string ResolveFolderName(string? requestedFolder, string? configuredFolder, string fallbackFolder = "INBOX") {
        var requested = NormalizeOptionalFolder(requestedFolder);
        if (requested != null) {
            return requested;
        }

        var configured = NormalizeOptionalFolder(configuredFolder);
        if (configured != null) {
            return configured;
        }

        var fallback = NormalizeOptionalFolder(fallbackFolder);
        return fallback ?? "INBOX";
    }

    private static string? NormalizeOptionalFolder(string? value) {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
