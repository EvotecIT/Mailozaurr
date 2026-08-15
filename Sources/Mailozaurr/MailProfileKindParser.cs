namespace Mailozaurr;

/// <summary>
/// Parses human-friendly profile kind values.
/// </summary>
public static class MailProfileKindParser {
    /// <summary>
    /// Parses a profile kind string.
    /// </summary>
    public static MailProfileKind Parse(string? value) {
        if (TryParse(value, out var kind)) {
            return kind;
        }

        throw new InvalidOperationException($"Unknown profile kind '{value}'.");
    }

    /// <summary>
    /// Attempts to parse a profile kind string.
    /// </summary>
    public static bool TryParse(string? value, out MailProfileKind kind) {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0) {
            kind = MailProfileKind.Unknown;
            return false;
        }

        switch (normalized.ToLowerInvariant()) {
            case "imap":
                kind = MailProfileKind.Imap;
                return true;
            case "pop3":
                kind = MailProfileKind.Pop3;
                return true;
            case "graph":
            case "microsoft-graph":
            case "msgraph":
                kind = MailProfileKind.Graph;
                return true;
            case "gmail":
                kind = MailProfileKind.Gmail;
                return true;
            case "smtp":
                kind = MailProfileKind.Smtp;
                return true;
            case "sendgrid":
                kind = MailProfileKind.SendGrid;
                return true;
            case "mailgun":
                kind = MailProfileKind.Mailgun;
                return true;
            case "ses":
            case "amazon-ses":
                kind = MailProfileKind.Ses;
                return true;
            default:
                kind = MailProfileKind.Unknown;
                return false;
        }
    }
}