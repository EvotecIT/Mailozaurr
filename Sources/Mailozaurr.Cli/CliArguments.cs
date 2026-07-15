using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;

namespace Mailozaurr.Cli;

internal sealed class CliArguments {
    private readonly ParseResult _parseResult;

    private CliArguments(ParseResult parseResult, IReadOnlyList<string> positionals, bool showHelp) {
        _parseResult = parseResult;
        Positionals = positionals;
        ShowHelp = showHelp;
    }

    public IReadOnlyList<string> Positionals { get; }

    public bool ShowHelp { get; }

    internal Command SelectedCommand => _parseResult.CommandResult.Command;

    public bool HasFlag(string name) => _parseResult.GetValue<bool>($"--{name}");

    public string? GetOption(string name) {
        string[] values = GetValues(name);
        return values.Length == 0 ? null : values[^1];
    }

    public IReadOnlyList<string?> GetOptionValues(string name) {
        string[] values = GetValues(name);
        if (values.Length == 0) return Array.Empty<string?>();

        var result = new string?[values.Length];
        for (int index = 0; index < values.Length; index++) {
            result[index] = values[index];
        }
        return result;
    }

    public int? GetIntOption(string name) {
        string? value = GetOption(name);
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (int.TryParse(value, out int parsed)) return parsed;

        throw new InvalidOperationException($"Option '--{name}' must be an integer.");
    }

    public IReadOnlyList<int> GetIntOptionValues(string name) {
        string[] values = GetValues(name);
        if (values.Length == 0) return Array.Empty<int>();

        var parsed = new List<int>(values.Length);
        foreach (string value in values) {
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (int.TryParse(value, out int item)) {
                parsed.Add(item);
                continue;
            }

            throw new InvalidOperationException($"Option '--{name}' must be an integer.");
        }
        return parsed;
    }

    public static CliArguments Parse(IReadOnlyList<string> args) {
        if (args == null) throw new ArgumentNullException(nameof(args));

        string[] arguments = args as string[] ?? args.ToArray();
        ParseResult parseResult = CliCommandModel.Root.Parse(arguments);
        bool showHelp = arguments.Length == 0 || parseResult.Action is HelpAction;
        if (!showHelp && parseResult.Errors.Count > 0) {
            throw new InvalidOperationException(FormatErrors(parseResult.Errors, arguments));
        }

        return new CliArguments(parseResult, BuildCommandPath(parseResult.CommandResult), showHelp);
    }

    internal void WriteHelp(TextWriter output) {
        if (output == null) throw new ArgumentNullException(nameof(output));
        CliCommandModel.WriteHelp(SelectedCommand, output);
    }

    private string[] GetValues(string name) {
        SymbolResult? result = _parseResult.GetResult($"--{name}");
        if (result == null || result.Tokens.Count == 0) return Array.Empty<string>();

        var values = new string[result.Tokens.Count];
        for (int index = 0; index < result.Tokens.Count; index++) {
            values[index] = result.Tokens[index].Value;
        }
        return values;
    }

    private static IReadOnlyList<string> BuildCommandPath(CommandResult selected) {
        var path = new Stack<string>();
        SymbolResult? current = selected;
        while (current is CommandResult commandResult) {
            if (commandResult.Command is RootCommand) break;
            path.Push(commandResult.Command.Name);
            current = commandResult.Parent;
        }
        return path.ToArray();
    }

    private static string FormatErrors(IReadOnlyList<ParseError> errors, IReadOnlyList<string> arguments) {
        var optionValues = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index + 1 < arguments.Count; index++) {
            if (arguments[index].StartsWith("-", StringComparison.Ordinal) &&
                !arguments[index + 1].StartsWith("-", StringComparison.Ordinal)) {
                optionValues.Add(arguments[index + 1]);
                index++;
            }
        }

        var messages = new string[errors.Count];
        for (int index = 0; index < errors.Count; index++) {
            string message = errors[index].Message;
            foreach (string optionValue in optionValues) {
                message = message.Replace($"'{optionValue}'", "'<value>'", StringComparison.Ordinal);
            }
            messages[index] = message;
        }
        return string.Join(Environment.NewLine, messages);
    }
}
