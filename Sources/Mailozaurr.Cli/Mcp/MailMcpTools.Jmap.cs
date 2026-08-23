using Mailozaurr;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Mailozaurr.Cli.Mcp;

public sealed partial class MailMcpTools {
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Gets the authoritative JMAP Session resource and advertised capabilities.")]
    public Task<JmapSessionResource> mail_jmap_session_get(string profileId, CancellationToken cancellationToken = default) =>
        _application.JmapMailbox.GetSessionAsync(profileId, cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Lists JMAP mailboxes and their effective rights.")]
    public Task<IReadOnlyList<JmapMailbox>> mail_jmap_mailbox_list(string profileId, CancellationToken cancellationToken = default) =>
        _application.JmapMailbox.ListMailboxesAsync(profileId, cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Queries a bounded page of JMAP email identifiers.")]
    public Task<JmapEmailQueryResult> mail_jmap_email_query(string profileId, JmapEmailFilter? filter = null, int position = 0, int limit = 100, bool collapseThreads = false, CancellationToken cancellationToken = default) =>
        _application.JmapMailbox.QueryEmailsAsync(profileId, filter, position: position, limit: limit, collapseThreads: collapseThreads, cancellationToken: cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Gets a bounded set of JMAP email objects.")]
    public Task<JmapEmailGetResult> mail_jmap_email_get(string profileId, string[] ids, string[]? properties = null, CancellationToken cancellationToken = default) =>
        _application.JmapMailbox.GetEmailsAsync(profileId, ids, properties, cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Gets bounded JMAP Email changes since an opaque state token.")]
    public Task<JmapEmailChangesResult> mail_jmap_email_changes(string profileId, string sinceState, int maxChanges = 1000, CancellationToken cancellationToken = default) =>
        _application.JmapMailbox.GetEmailChangesAsync(profileId, sinceState, maxChanges, cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Gets a bounded set of JMAP threads.")]
    public Task<IReadOnlyList<JmapThread>> mail_jmap_thread_get(string profileId, string[] ids, CancellationToken cancellationToken = default) =>
        _application.JmapMailbox.GetThreadsAsync(profileId, ids, cancellationToken);

    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Lists JMAP submission identities when that capability is available.")]
    public Task<IReadOnlyList<JmapIdentity>> mail_jmap_identity_list(string profileId, CancellationToken cancellationToken = default) =>
        _application.JmapMailbox.ListIdentitiesAsync(profileId, cancellationToken);
}
