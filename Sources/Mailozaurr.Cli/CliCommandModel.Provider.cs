using System.CommandLine;

namespace Mailozaurr.Cli;

internal static partial class CliCommandModel {
    private static Command CreateProviderCommand() => Group(
        "provider",
        "Inspect provider evidence and use Graph/Gmail native mailbox features.",
        Leaf("permission-evidence", "Inspect bounded Graph/Gmail OAuth permission evidence.", required: ["profile"], optional: ["mailbox"]),
        Leaf("graph-rule-list", "List Microsoft Graph Inbox rules.", required: ["profile"], optional: ["mailbox", "filter", "limit", "max-pages"]),
        Leaf("graph-rule-get", "Get one Microsoft Graph Inbox rule.", required: ["profile", "rule-id"], optional: ["mailbox"]),
        Leaf("graph-rule-create", "Create a Microsoft Graph Inbox rule from JSON.", required: ["profile", "file"], optional: ["mailbox"]),
        Leaf("graph-rule-update", "Update a Microsoft Graph Inbox rule from JSON.", required: ["profile", "rule-id", "file"], optional: ["mailbox"]),
        Leaf("graph-rule-delete", "Delete a Microsoft Graph Inbox rule.", required: ["profile", "rule-id"], optional: ["mailbox"]),
        Leaf("graph-event-list", "List Microsoft Graph calendar events.", required: ["profile"], optional: ["mailbox", "filter", "select", "limit", "max-pages"]),
        Leaf("graph-event-get", "Get one Microsoft Graph calendar event.", required: ["profile", "event-id"], optional: ["mailbox"]),
        Leaf("graph-event-create", "Create a Microsoft Graph calendar event from JSON.", required: ["profile", "file"], optional: ["mailbox"]),
        Leaf("graph-event-update", "Update a Microsoft Graph calendar event from JSON.", required: ["profile", "event-id", "file"], optional: ["mailbox"]),
        Leaf("graph-event-delete", "Delete a Microsoft Graph calendar event.", required: ["profile", "event-id"], optional: ["mailbox"]),
        Leaf("graph-thread-get", "List messages in one Microsoft Graph conversation.", required: ["profile", "thread-id"], optional: ["mailbox", "limit", "max-pages"]),
        Leaf("gmail-filter-list", "List Gmail filters.", required: ["profile"], optional: ["mailbox"]),
        Leaf("gmail-filter-get", "Get one Gmail filter.", required: ["profile", "filter-id"], optional: ["mailbox"]),
        Leaf("gmail-filter-create", "Create a Gmail filter from JSON.", required: ["profile", "file"], optional: ["mailbox"]),
        Leaf("gmail-filter-delete", "Delete a Gmail filter.", required: ["profile", "filter-id"], optional: ["mailbox"]),
        Leaf("gmail-label-list", "List Gmail labels.", required: ["profile"], optional: ["mailbox"]),
        Leaf("gmail-label-get", "Get one Gmail label.", required: ["profile", "label-id"], optional: ["mailbox"]),
        Leaf("gmail-label-create", "Create a Gmail user label from JSON.", required: ["profile", "file"], optional: ["mailbox"]),
        Leaf("gmail-label-update", "Update a Gmail user label from JSON.", required: ["profile", "label-id", "file"], optional: ["mailbox"]),
        Leaf("gmail-label-delete", "Delete a Gmail user label.", required: ["profile", "label-id"], optional: ["mailbox"]),
        Leaf("gmail-thread-list", "List one provider page of Gmail threads.", required: ["profile"], optional: ["mailbox", "query", "limit", "cursor"]),
        Leaf("gmail-thread-get", "Get one Gmail thread.", required: ["profile", "thread-id"], optional: ["mailbox"]),
        Leaf("gmail-thread-labels", "Add or remove labels on one Gmail thread.", required: ["profile", "thread-id"], optional: ["mailbox", "add-label", "remove-label"]),
        Leaf("gmail-thread-trash", "Move one Gmail thread to trash.", required: ["profile", "thread-id"], optional: ["mailbox"]),
        Leaf("gmail-thread-delete", "Permanently delete one Gmail thread.", required: ["profile", "thread-id"], optional: ["mailbox"]));
}
