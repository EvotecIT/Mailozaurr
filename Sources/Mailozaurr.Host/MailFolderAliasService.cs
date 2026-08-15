namespace Mailozaurr.Hosting;

/// <summary>
/// Default implementation of provider-neutral folder alias discovery.
/// </summary>
public sealed class MailFolderAliasService : IMailFolderAliasService {
    private static readonly AliasDefinition[] KnownAliases = {
        new(MailFolderAliases.Inbox, "Inbox", MatchMode.ReadOrList, new[] { "inbox" }, new[] { "inbox" }),
        new(MailFolderAliases.Archive, "Archive", MatchMode.Move, new[] { "archive", "allmail" }, new[] { "archive", "allmail" }),
        new(MailFolderAliases.Trash, "Trash", MatchMode.Move, new[] { "trash", "deleteditems", "bin" }, new[] { "trash", "deleteditems", "deleteditem", "bin" }),
        new(MailFolderAliases.Sent, "Sent", MatchMode.ListOnly, new[] { "sent", "sentitems" }, new[] { "sent", "sentitems", "sentmail" }),
        new(MailFolderAliases.Drafts, "Drafts", MatchMode.ListOnly, new[] { "drafts", "draft" }, new[] { "drafts", "draft" }),
        new(MailFolderAliases.Junk, "Junk", MatchMode.MoveOrList, new[] { "junk", "junkemail", "spam" }, new[] { "junk", "junkemail", "spam" })
    };

    private readonly IMailProfileStore _profileStore;
    private readonly IMailReadService _read;
    private readonly IReadOnlyDictionary<MailProfileKind, MailCapability>? _availableCapabilities;

    /// <summary>
    /// Creates a new folder alias service.
    /// </summary>
    public MailFolderAliasService(IMailProfileStore profileStore, IMailReadService read)
        : this(profileStore, read, null) {
    }

    /// <summary>
    /// Creates a new folder alias service with the capabilities backed by the current handler registry.
    /// </summary>
    public MailFolderAliasService(
        IMailProfileStore profileStore,
        IMailReadService read,
        IReadOnlyDictionary<MailProfileKind, MailCapability>? availableCapabilities) {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _availableCapabilities = availableCapabilities;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MailFolderAliasSummary>> GetAliasesAsync(
        string profileId,
        string? mailboxId = null,
        CancellationToken cancellationToken = default) {
        var profile = await GetProfileAsync(profileId, cancellationToken).ConfigureAwait(false);
        var capabilities = GetCapabilities(profile);
        var folders = await TryGetFoldersAsync(profile, mailboxId, capabilities, cancellationToken).ConfigureAwait(false);
        var results = new List<MailFolderAliasSummary>();
        foreach (var alias in KnownAliases) {
            if (!IsSupported(alias.Mode, capabilities)) {
                continue;
            }

            var resolved = TryResolveAlias(alias, folders);
            results.Add(new MailFolderAliasSummary {
                ProfileId = profile.Id,
                MailboxId = mailboxId,
                Alias = alias.Alias,
                DisplayName = alias.DisplayName,
                IsSupported = true,
                IsResolved = resolved != null,
                FolderId = resolved?.Id,
                FolderDisplayName = resolved?.DisplayName,
                FolderPath = resolved?.Path,
                SpecialUse = resolved?.SpecialUse,
                Summary = resolved == null
                    ? $"{alias.Alias} [alias-only]"
                    : $"{alias.Alias} -> {resolved.Path ?? resolved.DisplayName ?? resolved.Id}"
            });
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<MailFolderTargetResolution> ResolveAsync(
        string profileId,
        string targetFolderId,
        string? mailboxId = null,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(targetFolderId)) {
            throw new ArgumentException("Target folder id is required.", nameof(targetFolderId));
        }

        var normalizedTarget = targetFolderId.Trim();
        var canonicalAlias = MailFolderAliases.Canonicalize(normalizedTarget);
        if (canonicalAlias == null) {
            return new MailFolderTargetResolution {
                ProfileId = profileId,
                MailboxId = mailboxId,
                RequestedValue = normalizedTarget,
                IsAlias = false,
                IsSupported = true,
                IsResolved = true,
                EffectiveFolderId = normalizedTarget,
                Summary = $"{normalizedTarget} [explicit]"
            };
        }

        var alias = (await GetAliasesAsync(profileId, mailboxId, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(item => string.Equals(item.Alias, canonicalAlias, StringComparison.OrdinalIgnoreCase));

        if (alias == null) {
            return new MailFolderTargetResolution {
                ProfileId = profileId,
                MailboxId = mailboxId,
                RequestedValue = normalizedTarget,
                IsAlias = true,
                Alias = canonicalAlias,
                IsSupported = false,
                IsResolved = false,
                EffectiveFolderId = canonicalAlias,
                Summary = $"{canonicalAlias} [unsupported]"
            };
        }

        return new MailFolderTargetResolution {
            ProfileId = alias.ProfileId,
            MailboxId = alias.MailboxId,
            RequestedValue = normalizedTarget,
            IsAlias = true,
            Alias = alias.Alias,
            IsSupported = alias.IsSupported,
            IsResolved = alias.IsResolved,
            EffectiveFolderId = !string.IsNullOrWhiteSpace(alias.FolderId) ? alias.FolderId! : alias.Alias,
            FolderDisplayName = alias.FolderDisplayName,
            FolderPath = alias.FolderPath,
            SpecialUse = alias.SpecialUse,
            Summary = alias.IsResolved
                ? $"{alias.Alias} -> {alias.FolderPath ?? alias.FolderDisplayName ?? alias.FolderId}"
                : $"{alias.Alias} [alias-only]"
        };
    }

    private async Task<MailProfile> GetProfileAsync(string profileId, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        var profile = await _profileStore.GetByIdAsync(profileId.Trim(), cancellationToken).ConfigureAwait(false);
        return profile ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");
    }

    private ProfileCapabilities GetCapabilities(MailProfile profile) {
        if (_availableCapabilities == null) {
            return profile.GetCapabilities();
        }

        _availableCapabilities.TryGetValue(profile.Kind, out MailCapability available);
        return profile.GetCapabilities(available);
    }

    private async Task<IReadOnlyList<FolderRef>> TryGetFoldersAsync(
        MailProfile profile,
        string? mailboxId,
        ProfileCapabilities capabilities,
        CancellationToken cancellationToken) {
        if (!capabilities.Supports(MailCapability.ListFolders)) {
            return Array.Empty<FolderRef>();
        }

        try {
            return await _read.GetFoldersAsync(new MailFolderQuery {
                ProfileId = profile.Id,
                MailboxId = mailboxId
            }, cancellationToken).ConfigureAwait(false);
        } catch {
            return Array.Empty<FolderRef>();
        }
    }

    private static bool IsSupported(MatchMode mode, ProfileCapabilities capabilities) => mode switch {
        MatchMode.Move => capabilities.Supports(MailCapability.MoveMessages),
        MatchMode.ListOnly => capabilities.Supports(MailCapability.ListFolders),
        MatchMode.MoveOrList => capabilities.Supports(MailCapability.MoveMessages) || capabilities.Supports(MailCapability.ListFolders),
        MatchMode.ReadOrList => capabilities.Supports(MailCapability.ListFolders) || capabilities.Supports(MailCapability.ReadMessages) || capabilities.Supports(MailCapability.SearchMessages),
        _ => false
    };

    private static FolderRef? TryResolveAlias(AliasDefinition alias, IReadOnlyList<FolderRef> folders) {
        if (folders.Count == 0) {
            return null;
        }

        foreach (var folder in folders) {
            if (Matches(folder.SpecialUse, alias.SpecialUseMatches)) {
                return folder;
            }
        }

        foreach (var folder in folders) {
            if (Matches(folder.Path, alias.NameMatches) || Matches(folder.DisplayName, alias.NameMatches)) {
                return folder;
            }
        }

        return null;
    }

    private static bool Matches(string? value, IReadOnlyList<string> candidates) {
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        var normalizedValue = Normalize(value!);
        foreach (var candidate in candidates) {
            if (!string.IsNullOrWhiteSpace(candidate) && normalizedValue == Normalize(candidate!)) {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string value) {
        var buffer = new char[value.Length];
        var index = 0;
        foreach (var character in value) {
            if (char.IsLetterOrDigit(character)) {
                buffer[index++] = char.ToLowerInvariant(character);
            }
        }

        return new string(buffer, 0, index);
    }

    private sealed class AliasDefinition {
        public AliasDefinition(
            string alias,
            string displayName,
            MatchMode mode,
            IReadOnlyList<string> specialUseMatches,
            IReadOnlyList<string> nameMatches) {
            Alias = alias;
            DisplayName = displayName;
            Mode = mode;
            SpecialUseMatches = specialUseMatches;
            NameMatches = nameMatches;
        }

        public string Alias { get; }

        public string DisplayName { get; }

        public MatchMode Mode { get; }

        public IReadOnlyList<string> SpecialUseMatches { get; }

        public IReadOnlyList<string> NameMatches { get; }
    }

    private enum MatchMode {
        Move,
        ListOnly,
        MoveOrList,
        ReadOrList
    }
}
