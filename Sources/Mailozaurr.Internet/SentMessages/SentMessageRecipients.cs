using MimeKit;

namespace Mailozaurr;

internal static class SentMessageRecipients {
    public static string Serialize(InternetAddressList? recipients) {
        if (recipients == null || recipients.Count == 0) {
            return string.Empty;
        }

        return string.Join(",",
            recipients.Mailboxes
                .Select(mailbox => NormalizeAddress(mailbox.Address))
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .Distinct(StringComparer.OrdinalIgnoreCase)!);
    }

    public static IReadOnlyList<string> Parse(string? recipients) {
        if (string.IsNullOrWhiteSpace(recipients)) {
            return Array.Empty<string>();
        }

        var output = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in recipients!.Split(',')) {
            var normalized = NormalizeAddress(candidate);
            if (string.IsNullOrWhiteSpace(normalized)) {
                continue;
            }

            var address = normalized!;
            if (!seen.Add(address)) {
                continue;
            }

            output.Add(address);
        }

        return output;
    }

    public static string? NormalizeAddress(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        var normalized = value!.Trim();
        var semicolonIndex = normalized.IndexOf(';');
        if (semicolonIndex >= 0 && semicolonIndex + 1 < normalized.Length) {
            normalized = normalized.Substring(semicolonIndex + 1).Trim();
        }

        var start = normalized.LastIndexOf('<');
        var end = normalized.LastIndexOf('>');
        if (start >= 0 && end > start) {
            normalized = normalized.Substring(start + 1, end - start - 1).Trim();
        }

        if (normalized.StartsWith("\"", StringComparison.Ordinal) && normalized.EndsWith("\"", StringComparison.Ordinal) && normalized.Length > 1) {
            normalized = normalized.Substring(1, normalized.Length - 2).Trim();
        }

        return normalized.Length == 0 ? null : normalized;
    }
}