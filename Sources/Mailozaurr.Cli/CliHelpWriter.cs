using System.CommandLine;

namespace Mailozaurr.Cli;

internal static class CliHelpWriter {
    internal static void Write(RootCommand root, Command selected, TextWriter output) {
        if (root == null) throw new ArgumentNullException(nameof(root));
        if (selected == null) throw new ArgumentNullException(nameof(selected));
        if (output == null) throw new ArgumentNullException(nameof(output));

        output.WriteLine("Mailozaurr CLI");
        output.WriteLine();
        output.WriteLine("Usage:");
        output.Write("  mailozaurr");
        foreach (string segment in GetCommandPath(root, selected)) {
            output.Write(' ');
            output.Write(segment);
        }
        if (selected.Subcommands.Count > 0) output.Write(" <command>");
        if (selected.Options.Count > 0 || root.Options.Count > 0) output.Write(" [options]");
        output.WriteLine();

        if (!string.IsNullOrWhiteSpace(selected.Description)) {
            output.WriteLine();
            output.WriteLine(selected.Description);
        }

        if (selected.Subcommands.Count > 0) {
            output.WriteLine();
            output.WriteLine("Commands:");
            foreach (Command subcommand in selected.Subcommands.OrderBy(item => item.Name, StringComparer.Ordinal)) {
                output.Write("  ");
                output.Write(subcommand.Name.PadRight(34));
                output.WriteLine(subcommand.Description);
            }
        }

        output.WriteLine();
        output.WriteLine("Options:");
        foreach (Option option in GetOptions(root, selected)) {
            output.Write("  ");
            string label = option is Option<bool> ? option.Name : $"{option.Name} <value>";
            if (option.Required) label += " (required)";
            output.Write(label.PadRight(38));
            output.WriteLine(option.Description);
        }
        output.WriteLine("  -h, --help".PadRight(40) + "Show command help.");
    }

    private static IEnumerable<string> GetCommandPath(RootCommand root, Command selected) {
        var path = new Stack<string>();
        Command? current = selected;
        while (current != null && !ReferenceEquals(current, root)) {
            path.Push(current.Name);
            current = GetParentCommand(current);
        }
        return path;
    }

    private static IEnumerable<Option> GetOptions(RootCommand root, Command selected) {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Option option in selected.Options) {
            if (IsBuiltInOption(option)) continue;
            if (names.Add(option.Name)) yield return option;
        }
        if (!ReferenceEquals(selected, root)) {
            foreach (Option option in root.Options) {
                if (IsBuiltInOption(option)) continue;
                if (option.Recursive && names.Add(option.Name)) yield return option;
            }
        }
    }

    private static bool IsBuiltInOption(Option option) =>
        string.Equals(option.Name, "--help", StringComparison.Ordinal) ||
        string.Equals(option.Name, "--version", StringComparison.Ordinal);

    private static Command? GetParentCommand(Command command) {
        foreach (Symbol parent in command.Parents) {
            if (parent is Command parentCommand) return parentCommand;
        }
        return null;
    }
}
