namespace Mailozaurr;

/// <summary>Provider-neutral result shapes and normalization for native Sent-folder operations.</summary>
public static class NativeSentMailboxOperations {
    /// <summary>Result of a native Sent append operation.</summary>
    public sealed class NativeSentAppendResult {
        /// <summary>True when append succeeded.</summary>
        public bool Appended { get; set; }
        /// <summary>Resolved folder or label display name.</summary>
        public string? Folder { get; set; }
        /// <summary>Resolved provider message id when available.</summary>
        public string? MessageId { get; set; }
    }

    /// <summary>Result of a native Sent duplicate probe.</summary>
    public sealed class NativeSentDuplicateProbeResult {
        /// <summary>True when a duplicate message was found.</summary>
        public bool IsMatch { get; set; }
        /// <summary>Resolved folder or label display name.</summary>
        public string? Folder { get; set; }
        /// <summary>Matched message-id token.</summary>
        public string? MessageId { get; set; }
        /// <summary>Reusable non-match result.</summary>
        public static NativeSentDuplicateProbeResult None { get; } = new();
    }

    /// <summary>Resolves a requested, configured, or fallback folder name in priority order.</summary>
    public static string ResolveFolderName(string? requestedFolder, string? configuredFolder,
        string fallbackFolder) => NormalizeOptional(requestedFolder)
        ?? NormalizeOptional(configuredFolder)
        ?? NormalizeOptional(fallbackFolder)
        ?? throw new ArgumentException("A fallback folder is required.", nameof(fallbackFolder));

    /// <summary>Normalizes an RFC822 Message-Id token without angle brackets.</summary>
    public static string? NormalizeMessageIdToken(string? value) {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string token = value!.Trim();
        if (token.StartsWith("<", StringComparison.Ordinal)) token = token.Substring(1);
        if (token.EndsWith(">", StringComparison.Ordinal)) token = token.Substring(0, token.Length - 1);
        token = token.Trim();
        return token.Length == 0 ? null : token;
    }

    private static string? NormalizeOptional(string? value) {
        string trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
