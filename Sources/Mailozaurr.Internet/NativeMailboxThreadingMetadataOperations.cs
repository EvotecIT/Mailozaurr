namespace Mailozaurr;

/// <summary>Normalizes provider-native threading metadata into one RFC-oriented shape.</summary>
public static class NativeMailboxThreadingMetadataOperations {
    /// <summary>Provider-neutral message threading metadata.</summary>
    public sealed class NativeMailboxThreadingMetadataResult {
        /// <summary>Normalized RFC822 Message-Id.</summary>
        public string? MessageId { get; set; }
        /// <summary>Reply-To header value.</summary>
        public string? ReplyTo { get; set; }
        /// <summary>Cc header value.</summary>
        public string? Cc { get; set; }
        /// <summary>Normalized RFC822 In-Reply-To value.</summary>
        public string? InReplyTo { get; set; }
        /// <summary>Normalized RFC822 References tokens.</summary>
        public List<string>? References { get; set; }
    }

    /// <summary>Normalizes provider-supplied threading metadata values.</summary>
    public static NativeMailboxThreadingMetadataResult Normalize(
        string? messageId, string? replyTo, string? cc, string? inReplyTo,
        IEnumerable<string>? references) => new() {
            MessageId = ImapSentMessageOperations.NormalizeMessageIdToken(messageId),
            ReplyTo = NormalizeOptional(replyTo),
            Cc = NormalizeOptional(cc),
            InReplyTo = ImapSentMessageOperations.NormalizeMessageIdToken(inReplyTo),
            References = NormalizeMessageIdList(references)
        };

    private static List<string>? NormalizeMessageIdList(IEnumerable<string>? references) {
        if (references == null) return null;
        var output = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string reference in references) {
            string? normalized = ImapSentMessageOperations.NormalizeMessageIdToken(reference);
            if (normalized != null && seen.Add(normalized)) output.Add(normalized);
        }
        return output.Count == 0 ? null : output;
    }

    private static string? NormalizeOptional(string? value) {
        string trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
