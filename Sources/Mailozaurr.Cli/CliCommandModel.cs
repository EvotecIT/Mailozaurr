using System.CommandLine;

namespace Mailozaurr.Cli;

internal static partial class CliCommandModel {
    internal static RootCommand Root { get; } = CreateRoot();

    internal static void WriteHelp(Command command, TextWriter output) =>
        CliHelpWriter.Write(Root, command, output);

    private static RootCommand CreateRoot() {
        var root = new RootCommand("Mailozaurr command-line and MCP host.");
        RemoveBuiltInVersionOption(root);
        AddRecursiveOption(root, "json");
        AddRecursiveOption(root, "profiles-dir");
        AddRecursiveOption(root, "secrets-dir");
        AddRecursiveOption(root, "drafts-dir");
        AddRecursiveOption(root, "plan-batches-dir");

        root.Add(CreateProfileCommand());
        root.Add(CreateDraftCommand());
        root.Add(CreateMailCommand());
        root.Add(CreateMcpCommand());
        root.Add(CreateSendCommand());
        root.Add(CreateQueueCommand());
        return root;
    }

    private static void RemoveBuiltInVersionOption(RootCommand root) {
        for (int index = root.Options.Count - 1; index >= 0; index--) {
            if (string.Equals(root.Options[index].Name, "--version", StringComparison.Ordinal)) {
                root.Options.RemoveAt(index);
            }
        }
    }

    private static Command Group(string name, string description, params Command[] subcommands) {
        var command = new Command(name, description);
        foreach (Command subcommand in subcommands) command.Add(subcommand);
        return command;
    }

    private static Command Leaf(
        string name,
        string description,
        string[]? required = null,
        string[]? optional = null) {
        var command = new Command(name, description);
        AddOptions(command, required, required: true);
        AddOptions(command, optional, required: false);
        return command;
    }

    private static void AddOptions(Command command, string[]? names, bool required) {
        if (names == null) return;
        foreach (string name in names) command.Add(CreateOption(name, required, recursive: false));
    }

    private static void AddRecursiveOption(RootCommand command, string name) =>
        command.Add(CreateOption(name, required: false, recursive: true));

    private static Option CreateOption(string name, bool required, bool recursive) {
        if (IsFlag(name)) {
            return new Option<bool>($"--{name}") {
                Description = GetOptionDescription(name),
                Recursive = recursive
            };
        }

        var option = new Option<string[]>($"--{name}") {
            Description = GetOptionDescription(name),
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = false,
            CustomParser = result => {
                var values = new string[result.Tokens.Count];
                for (int index = 0; index < result.Tokens.Count; index++) {
                    values[index] = result.Tokens[index].Value;
                }
                return values;
            },
            Required = required,
            Recursive = recursive
        };
        option.Validators.Add(result => {
            if (result.Tokens.Count == 0) {
                result.AddError($"Option '--{name}' requires a value.");
                return;
            }
            foreach (var token in result.Tokens) {
                if (token.Value.StartsWith("--", StringComparison.Ordinal)) {
                    result.AddError($"Option '--{name}' requires a value.");
                    return;
                }
            }
        });
        return option;
    }

    private static bool IsFlag(string name) => name is
        "access-token-stdin" or "can-read" or "can-send" or "certificate-password-stdin" or
        "client-secret-stdin" or "compact" or "default-only" or "desc" or "has-attachments" or
        "include-raw" or "is-default" or "json" or "overwrite" or "queue-on-failure" or
        "ready-only" or "refresh-token-stdin" or "root-only" or "stop-on-error" or "summary" or
        "unflag" or "unread" or "value-stdin";

    private static string GetOptionDescription(string name) => name switch {
        "access-token-env" => "Environment variable containing an access token.",
        "access-token-ref" => "Stored access-token reference in profile-id:secret-name form.",
        "access-token-stdin" => "Read the access token from standard input.",
        "action" => "Message action name.",
        "attachment" => "Attachment file path; repeat for multiple files.",
        "attachment-id" => "Attachment identifier; repeat when supported.",
        "batch" => "Stored action-plan batch identifier.",
        "bcc" => "BCC recipient address; repeat for multiple recipients.",
        "can-read" => "Return only profiles that support read operations.",
        "can-send" => "Return only profiles that support send operations.",
        "cc" => "CC recipient address; repeat for multiple recipients.",
        "certificate-password-env" => "Environment variable containing the certificate password.",
        "certificate-password-ref" => "Stored certificate-password reference.",
        "certificate-password-stdin" => "Read the certificate password from standard input.",
        "certificate-path" => "Certificate file path.",
        "client-id" => "OAuth client or application identifier.",
        "client-secret-env" => "Environment variable containing the client secret.",
        "client-secret-ref" => "Stored client-secret reference.",
        "client-secret-stdin" => "Read the client secret from standard input.",
        "compact" => "Return the compact response projection.",
        "confirm-token" => "Confirmation token from the matching preview.",
        "content-type" => "Attachment content-type filter.",
        "default-mailbox" => "Default mailbox identifier or address.",
        "default-only" => "Return only the default profile.",
        "default-sender" => "Default sender address.",
        "desc" => "Sort in descending order.",
        "description" => "Human-readable description.",
        "draft" => "Draft identifier.",
        "drafts-dir" => "Override the draft-store directory.",
        "file" => "Input file path.",
        "folder" => "Mailbox folder identifier or name.",
        "from" => "Sender address.",
        "has-attachments" => "Return only messages with attachments.",
        "header" => "Message header in key=value form; repeat for multiple headers.",
        "html" => "HTML message body.",
        "include-raw" => "Include raw provider content when available.",
        "index" => "Zero-based action-plan index; repeat when supported.",
        "is-default" => "Make the profile the default.",
        "kind" => "Mail provider kind.",
        "limit" => "Maximum result count.",
        "login" => "Interactive login user name.",
        "mailbox" => "Mailbox identifier or address.",
        "message-id" => "Message identifier; repeat when supported.",
        "name" => "Display name or stable secret name.",
        "name-contains" => "Attachment filename substring filter.",
        "overwrite" => "Allow replacement of an existing destination file.",
        "parent-folder" => "Parent folder identifier.",
        "path" => "Input or destination path.",
        "plan-batches-dir" => "Override the action-plan batch-store directory.",
        "plan-name" => "Action-plan name; repeat when supported.",
        "profile" => "Mail profile identifier; repeat when supported.",
        "profiles-dir" => "Override the profile-store directory.",
        "query" => "Provider search query.",
        "queue-on-failure" => "Queue the message only when immediate delivery fails.",
        "ready-only" => "Return only profiles ready for use.",
        "redirect-uri" => "OAuth redirect URI.",
        "refresh-token-env" => "Environment variable containing the refresh token.",
        "refresh-token-ref" => "Stored refresh-token reference.",
        "refresh-token-stdin" => "Read the refresh token from standard input.",
        "reply-to" => "Reply-To address; repeat for multiple addresses.",
        "root-only" => "Return only top-level folders.",
        "scope" => "Connection-test or OAuth scope; repeat when supported.",
        "secrets-dir" => "Override the protected secret-store directory.",
        "setting" => "Non-secret profile setting in key=value form; repeat when needed.",
        "sort" => "Sort key.",
        "source-batch" => "Source action-plan batch identifier.",
        "stop-on-error" => "Stop batch execution after the first failure.",
        "subject" => "Message subject or subject search filter.",
        "summary" => "Return the summary response projection.",
        "target-batch" => "Target action-plan batch identifier.",
        "target-folder" => "Destination folder identifier or alias.",
        "target-profile" => "Replacement profile identifier.",
        "tenant-id" => "OAuth tenant or directory identifier.",
        "text" => "Plain-text message body.",
        "to" => "Recipient address; repeat for multiple recipients.",
        "unflag" => "Clear the flagged state.",
        "unread" => "Set the unread state.",
        "value-env" => "Environment variable containing the secret value.",
        "value-ref" => "Stored secret reference to copy without exposing its value.",
        "value-stdin" => "Read the secret value from standard input.",
        "json" => "Write machine-readable JSON output.",
        _ => throw new InvalidOperationException($"No typed CLI description exists for option '--{name}'.")
    };
}
