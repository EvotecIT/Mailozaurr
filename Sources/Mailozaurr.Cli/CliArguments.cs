namespace Mailozaurr.Cli;

internal sealed class CliArguments {
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
                string? value = null;
                if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) {
                    value = args[++i];
                }
                if (!result.Options.TryGetValue(key, out var values)) {
                    values = new List<string?>();
                    result.Options[key] = values;
                }

                values.Add(value ?? "true");
                continue;
            }

            result.Positionals.Add(arg);
        }

        return result;
    }
}