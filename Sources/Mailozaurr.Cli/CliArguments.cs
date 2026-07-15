namespace Mailozaurr.Cli;

internal sealed class CliArguments {
    private static readonly HashSet<string> FlagOptions = new(StringComparer.OrdinalIgnoreCase) {
        "access-token-stdin", "can-read", "can-send", "certificate-password-stdin",
        "client-secret-stdin", "compact", "default-only", "desc", "has-attachments",
        "include-raw", "is-default", "json", "overwrite", "queue-on-failure",
        "ready-only", "refresh-token-stdin", "root-only", "stop-on-error", "summary",
        "unflag", "unread", "value-stdin"
    };

    private static readonly HashSet<string> ValueOptions = new(StringComparer.OrdinalIgnoreCase) {
        "access-token-env", "access-token-ref", "action", "attachment", "attachment-id", "batch",
        "bcc", "cc", "certificate-password-env", "certificate-password-ref", "certificate-path",
        "client-id", "client-secret-env", "client-secret-ref", "confirm-token", "content-type",
        "default-mailbox", "default-sender", "description", "draft", "drafts-dir", "file",
        "folder", "from", "header", "html", "index", "kind", "limit", "login", "mailbox",
        "message-id", "name", "name-contains", "parent-folder", "path", "plan-batches-dir", "plan-name",
        "profile", "profiles-dir", "query", "redirect-uri", "refresh-token-env",
        "refresh-token-ref", "reply-to", "scope", "secrets-dir", "setting", "sort", "source-batch",
        "subject", "target-batch", "target-folder", "target-profile", "tenant-id", "text", "to",
        "value-env", "value-ref"
    };

    public List<string> Positionals { get; } = new();

    public Dictionary<string, List<string?>> Options { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool ShowHelp { get; private set; }

    public bool HasFlag(string name) {
        var value = GetOption(name);
        return value != null && value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public string? GetOption(string name) =>
        Options.TryGetValue(name, out var values) && values.Count > 0
            ? values[^1]
            : null;

    public IReadOnlyList<string?> GetOptionValues(string name) =>
        Options.TryGetValue(name, out var values)
            ? values.AsReadOnly()
            : Array.Empty<string?>();

    public int? GetIntOption(string name) {
        var value = GetOption(name);
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }
        if (int.TryParse(value, out var parsed)) {
            return parsed;
        }

        throw new InvalidOperationException($"Option '--{name}' must be an integer.");
    }

    public IReadOnlyList<int> GetIntOptionValues(string name) {
        var values = GetOptionValues(name);
        if (values.Count == 0) {
            return Array.Empty<int>();
        }

        var parsed = new List<int>(values.Count);
        foreach (var value in values) {
            if (string.IsNullOrWhiteSpace(value)) {
                continue;
            }
            if (int.TryParse(value, out var item)) {
                parsed.Add(item);
                continue;
            }

            throw new InvalidOperationException($"Option '--{name}' must be an integer.");
        }

        return parsed;
    }

    public static CliArguments Parse(IReadOnlyList<string> args) {
        var result = new CliArguments();
        for (var i = 0; i < args.Count; i++) {
            var arg = args[i];
            if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase)) {
                result.ShowHelp = true;
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal)) {
                var key = arg.Substring(2);
                if (key.Length == 0 || (!FlagOptions.Contains(key) && !ValueOptions.Contains(key))) {
                    throw new InvalidOperationException($"Unknown option '--{key}'.");
                }

                string value;
                if (FlagOptions.Contains(key)) {
                    value = "true";
                } else {
                    if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal)) {
                        throw new InvalidOperationException($"Option '--{key}' requires a value.");
                    }
                    value = args[++i];
                }
                if (!result.Options.TryGetValue(key, out var values)) {
                    values = new List<string?>();
                    result.Options[key] = values;
                }
                values.Add(value);
                continue;
            }

            result.Positionals.Add(arg);
        }

        return result;
    }

    public void ValidatePositionalCount() {
        if (Positionals.Count == 0) {
            return;
        }

        int maximum = string.Equals(Positionals[0], "send", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
        if (Positionals.Count > maximum) {
            throw new InvalidOperationException($"Unexpected positional argument '{Positionals[maximum]}'.");
        }
    }
}
