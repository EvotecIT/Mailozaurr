#pragma warning disable CS1591
#pragma warning disable CS8600,CS8601,CS8602,CS8603,CS8604,CS8618,CS8625
#nullable enable
using System;

namespace Mailozaurr;

public static class ImapSentMessageOperations {
    public static string? NormalizeMessageIdToken(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0) {
            return null;
        }

        if (trimmed.StartsWith("<", StringComparison.Ordinal)) {
            trimmed = trimmed.Substring(1);
        }
        if (trimmed.EndsWith(">", StringComparison.Ordinal)) {
            trimmed = trimmed.Substring(0, trimmed.Length - 1);
        }

        trimmed = trimmed.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
