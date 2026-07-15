using System.CommandLine;

namespace Mailozaurr.Cli;

internal static partial class CliCommandModel {
    private static Command CreateDraftCommand() => Group(
        "draft",
        "Manage reusable message drafts.",
        Leaf("list", "List stored drafts.", optional: ["compact"]),
        Leaf("save", "Import a draft file or save an inline draft.",
            optional: [
                "file", "draft", "name", "profile", "to", "cc", "bcc", "reply-to", "from",
                "subject", "text", "html", "attachment", "header"
            ]),
        Leaf("get", "Get a stored draft.", required: ["draft"], optional: ["compact"]),
        Leaf("delete", "Delete a stored draft.", required: ["draft"]),
        Leaf("export", "Export a stored draft.", required: ["draft", "path"]));

    private static Command CreateQueueCommand() => Group(
        "queue",
        "Inspect and process the persistent delivery queue.",
        Leaf("list", "List queued messages.", optional: ["compact"]),
        Leaf("get", "Get a queued message.", required: ["message-id"], optional: ["compact"]),
        Leaf("remove", "Remove a queued message.", required: ["message-id"]),
        Leaf("process", "Attempt delivery of queued messages."),
        Leaf("dead-letter-list", "List dead-letter messages."),
        Leaf("dead-letter-get", "Get a dead-letter message.", required: ["message-id"]),
        Leaf("dead-letter-remove", "Remove a dead-letter message.", required: ["message-id"]));

    private static Command CreateMcpCommand() => Group(
        "mcp",
        "Host Mailozaurr tools over Model Context Protocol.",
        Leaf("serve", "Run the MCP server over standard input and output."));

    private static Command CreateSendCommand() => Leaf(
        "send",
        "Send a stored draft, draft file, or inline message immediately.",
        optional: [
            "draft", "file", "profile", "to", "cc", "bcc", "reply-to", "from", "subject",
            "text", "html", "attachment", "header", "queue-on-failure"
        ]);
}
