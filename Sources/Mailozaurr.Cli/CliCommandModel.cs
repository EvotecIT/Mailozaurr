using System.CommandLine;

namespace Mailozaurr.Cli;

internal static partial class CliCommandModel {
    private static readonly char[] OptionValueSeparators = { '=', ':' };
    private static readonly Dictionary<string, string> CanonicalOptionAliases =
        new(StringComparer.OrdinalIgnoreCase) {
            ["--help"] = "--help",
            ["-h"] = "-h"
        };

    internal static RootCommand Root { get; } = CreateRoot();

    internal static void WriteHelp(Command command, TextWriter output) =>
        CliHelpWriter.Write(Root, command, output);

    internal static string[] NormalizeOptionAliases(IReadOnlyList<string> arguments) {
        var normalized = new string[arguments.Count];
        for (int index = 0; index < arguments.Count; index++) {
            string argument = arguments[index];
            int separatorIndex = argument.IndexOfAny(OptionValueSeparators);
            string alias = separatorIndex > 0 ? argument[..separatorIndex] : argument;
            normalized[index] = CanonicalOptionAliases.TryGetValue(alias, out string? canonical)
                ? separatorIndex > 0
                    ? canonical + argument[separatorIndex..]
                    : canonical
                : argument;
        }
        return normalized;
    }

    private static RootCommand CreateRoot() {
        var root = new RootCommand("Mailozaurr command-line and MCP host.");
        root.Directives.Clear();
        RemoveBuiltInVersionOption(root);
        AddRecursiveOption(root, "json");
        AddRecursiveOption(root, "profiles-dir");
        AddRecursiveOption(root, "secrets-dir");
        AddRecursiveOption(root, "drafts-dir");
        AddRecursiveOption(root, "plan-batches-dir");

        root.Add(CreateProfileCommand());
        root.Add(CreateDraftCommand());
        root.Add(CreateMailCommand());
        root.Add(CreateProviderCommand());
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
        string alias = $"--{name}";
        CanonicalOptionAliases[alias] = alias;
        if (IsFlag(name)) {
            return new Option<bool>(alias) {
                Arity = ArgumentArity.Zero,
                Description = GetOptionDescription(name),
                Recursive = recursive
            };
        }

        var option = new Option<string[]>(alias) {
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
            if (result.Tokens.Count < result.IdentifierTokenCount) {
                result.AddError($"Option '--{name}' requires a value for every occurrence.");
                return;
            }
            if (result.Tokens.Count > result.IdentifierTokenCount) {
                result.AddError($"Option '--{name}' accepts one value per occurrence.");
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
        "access-token-stdin" or "allow-cross-profile-secret-ref" or "can-read" or "can-send" or "certificate-password-stdin" or
        "client-secret-stdin" or "compact" or "default-only" or "desc" or "has-attachments" or
        "include-raw" or "is-default" or "json" or "overwrite" or "queue-on-failure" or
        "ready-only" or "refresh-token-stdin" or "root-only" or "stop-on-error" or "summary" or
        "strict" or "unflag" or "unread" or "value-stdin";

    private static string GetOptionDescription(string name) => name switch {
        "access-token-env" => "Environment variable containing an access token.",
        "access-token-ref" => "Stored access-token reference in profile-id:secret-name form.",
        "access-token-stdin" => "Read the access token from standard input.",
        "action" => "Message action name.",
        "attachment" => "Attachment file path; repeat for multiple files.",
        "attachment-id" => "Attachment identifier; repeat when supported.",
        "batch" => "Stored action-plan batch identifier.",
        "add-label" => "Gmail label identifier to add; repeat for multiple labels.",
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
        "allow-cross-profile-secret-ref" => "Explicitly allow compatible same-name secret references from another profile.",
        "client-secret-stdin" => "Read the client secret from standard input.",
        "compact" => "Return the compact response projection.",
        "confirm-token" => "Confirmation token from the matching preview.",
        "client-state" => "Opaque Graph notification client state.",
        "content-type" => "Attachment content-type filter.",
        "default-mailbox" => "Default mailbox identifier or address.",
        "default-only" => "Return only the default profile.",
        "default-sender" => "Default sender address.",
        "desc" => "Sort in descending order.",
        "description" => "Human-readable description.",
        "draft" => "Draft identifier.",
        "drafts-dir" => "Override the draft-store directory.",
        "cursor" => "Graph delta URL or opaque Gmail history cursor.",
        "expiration" => "ISO 8601 Graph subscription expiration timestamp.",
        "event-id" => "Microsoft Graph event identifier.",
        "file" => "Input file path.",
        "filter" => "Microsoft Graph OData filter expression.",
        "filter-id" => "Gmail filter identifier.",
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
        "position" => "Zero-based JMAP query position; negative values address results from the end.",
        "label-id" => "Gmail label identifier.",
        "login" => "Interactive login user name.",
        "mailbox" => "Mailbox identifier or address.",
        "max-bytes" => "Maximum provider bytes accepted for one message.",
        "max-pages" => "Maximum provider pages to read.",
        "message-id" => "Message identifier; repeat when supported.",
        "name" => "Display name or stable secret name.",
        "name-contains" => "Attachment filename substring filter.",
        "notification-url" => "Graph HTTPS notification endpoint.",
        "overwrite" => "Allow replacement of an existing destination file.",
        "parent-folder" => "Parent folder identifier.",
        "path" => "Input or destination path.",
        "plan-batches-dir" => "Override the action-plan batch-store directory.",
        "plan-name" => "Action-plan name; repeat when supported.",
        "profile" => "Mail profile identifier; repeat when supported.",
        "profiles-dir" => "Override the profile-store directory.",
        "query" => "Provider search query.",
        "remove-label" => "Gmail label identifier to remove; repeat for multiple labels.",
        "queue-on-failure" => "Queue the message only when immediate delivery fails.",
        "ready-only" => "Return only profiles ready for use.",
        "redirect-uri" => "OAuth redirect URI.",
        "refresh-token-env" => "Environment variable containing the refresh token.",
        "refresh-token-ref" => "Stored refresh-token reference.",
        "refresh-token-stdin" => "Read the refresh token from standard input.",
        "reply-to" => "Reply-To address; repeat for multiple addresses.",
        "root-only" => "Return only top-level folders.",
        "scope" => "Connection-test or OAuth scope; repeat when supported.",
        "select" => "Provider field selection expression.",
        "secrets-dir" => "Override the protected secret-store directory.",
        "setting" => "Non-secret profile setting in key=value form; repeat when needed.",
        "sort" => "Sort key.",
        "source-batch" => "Source action-plan batch identifier.",
        "stop-on-error" => "Stop batch execution after the first failure.",
        "strict" => "Fail when a remote subscription is already absent.",
        "subject" => "Message subject or subject search filter.",
        "subscription-id" => "Graph subscription identifier.",
        "rule-id" => "Microsoft Graph Inbox rule identifier.",
        "summary" => "Return the summary response projection.",
        "target-batch" => "Target action-plan batch identifier.",
        "target-folder" => "Destination folder identifier or alias.",
        "target-profile" => "Replacement profile identifier.",
        "tenant-id" => "OAuth tenant or directory identifier.",
        "text" => "Plain-text message body.",
        "timeout-seconds" => "Maximum live wait duration in seconds.",
        "thread-id" => "Provider conversation or thread identifier.",
        "topic" => "Gmail Pub/Sub topic name.",
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
