namespace Mailozaurr;

/// <summary>
/// Shared well-known folder aliases used across providers.
/// </summary>
public static class MailFolderAliases {
    private static readonly string[] KnownAliases = {
        Inbox,
        Archive,
        Trash,
        Sent,
        Drafts,
        Junk
    };

    /// <summary>Inbox folder alias.</summary>
    public const string Inbox = "Inbox";

    /// <summary>Archive folder alias.</summary>
    public const string Archive = "Archive";

    /// <summary>Trash/deleted-items folder alias.</summary>
    public const string Trash = "Trash";

    /// <summary>Sent items folder alias.</summary>
    public const string Sent = "Sent";

    /// <summary>Drafts folder alias.</summary>
    public const string Drafts = "Drafts";

    /// <summary>Junk or spam folder alias.</summary>
    public const string Junk = "Junk";

    /// <summary>Returns all known provider-neutral folder aliases.</summary>
    public static IReadOnlyList<string> All => KnownAliases;

    /// <summary>Returns the canonical alias value when the input matches a known alias.</summary>
    public static string? Canonicalize(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        var normalizedValue = value!.Trim();
        foreach (var alias in KnownAliases) {
            if (string.Equals(alias, normalizedValue, StringComparison.OrdinalIgnoreCase)) {
                return alias;
            }
        }

        return null;
    }
}