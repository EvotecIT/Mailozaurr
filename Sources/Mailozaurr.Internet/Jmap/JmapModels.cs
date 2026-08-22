using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>Standard JMAP capability identifiers.</summary>
public static class JmapCapabilities {
    /// <summary>RFC 8620 core capability.</summary>
    public const string Core = "urn:ietf:params:jmap:core";

    /// <summary>RFC 8621 mail capability.</summary>
    public const string Mail = "urn:ietf:params:jmap:mail";

    /// <summary>RFC 8621 submission and identity capability.</summary>
    public const string Submission = "urn:ietf:params:jmap:submission";
}

/// <summary>JMAP Session resource discovered through the configured HTTPS endpoint.</summary>
public sealed class JmapSessionResource {
    /// <summary>Server-wide capabilities.</summary>
    [JsonPropertyName("capabilities")]
    public Dictionary<string, JsonElement> Capabilities { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Accounts available to the authenticated principal.</summary>
    [JsonPropertyName("accounts")]
    public Dictionary<string, JmapAccount> Accounts { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Primary account identifiers keyed by capability.</summary>
    [JsonPropertyName("primaryAccounts")]
    public Dictionary<string, string> PrimaryAccounts { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Authenticated username reported by the server.</summary>
    [JsonPropertyName("username")]
    public string? UserName { get; set; }

    /// <summary>JMAP method-call endpoint.</summary>
    [JsonPropertyName("apiUrl")]
    public string? ApiUrl { get; set; }

    /// <summary>Blob download URL template.</summary>
    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    /// <summary>EventSource URL template when push is supported.</summary>
    [JsonPropertyName("eventSourceUrl")]
    public string? EventSourceUrl { get; set; }

    /// <summary>Session state token.</summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }
}

/// <summary>One JMAP account and its authoritative account-scoped capabilities.</summary>
public sealed class JmapAccount {
    /// <summary>Human-readable account name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Whether the account belongs to the authenticated principal.</summary>
    [JsonPropertyName("isPersonal")]
    public bool IsPersonal { get; set; }

    /// <summary>Whether the account is read-only.</summary>
    [JsonPropertyName("isReadOnly")]
    public bool IsReadOnly { get; set; }

    /// <summary>Capabilities authorized for this account.</summary>
    [JsonPropertyName("accountCapabilities")]
    public Dictionary<string, JsonElement> AccountCapabilities { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>JMAP mailbox object.</summary>
public sealed class JmapMailbox {
    /// <summary>Stable mailbox identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Human-readable mailbox name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Parent mailbox identifier, or null for a top-level mailbox.</summary>
    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    /// <summary>Standard mailbox role such as inbox, sent, or trash.</summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>Server-defined mailbox sort order.</summary>
    [JsonPropertyName("sortOrder")]
    public long SortOrder { get; set; }

    /// <summary>Total email count.</summary>
    [JsonPropertyName("totalEmails")]
    public long TotalEmails { get; set; }

    /// <summary>Unread email count.</summary>
    [JsonPropertyName("unreadEmails")]
    public long UnreadEmails { get; set; }

    /// <summary>Total thread count.</summary>
    [JsonPropertyName("totalThreads")]
    public long TotalThreads { get; set; }

    /// <summary>Unread thread count.</summary>
    [JsonPropertyName("unreadThreads")]
    public long UnreadThreads { get; set; }

    /// <summary>Effective rights for the authenticated principal.</summary>
    [JsonPropertyName("myRights")]
    public JmapMailboxRights? MyRights { get; set; }
}

/// <summary>Effective rights reported for a JMAP mailbox.</summary>
public sealed class JmapMailboxRights {
    /// <summary>Whether mailbox items may be read.</summary>
    [JsonPropertyName("mayReadItems")]
    public bool MayReadItems { get; set; }

    /// <summary>Whether mailbox items may be added.</summary>
    [JsonPropertyName("mayAddItems")]
    public bool MayAddItems { get; set; }

    /// <summary>Whether mailbox items may be removed.</summary>
    [JsonPropertyName("mayRemoveItems")]
    public bool MayRemoveItems { get; set; }

    /// <summary>Whether the seen keyword may be changed.</summary>
    [JsonPropertyName("maySetSeen")]
    public bool MaySetSeen { get; set; }

    /// <summary>Whether message keywords may be changed.</summary>
    [JsonPropertyName("maySetKeywords")]
    public bool MaySetKeywords { get; set; }

    /// <summary>Whether child mailboxes may be created.</summary>
    [JsonPropertyName("mayCreateChild")]
    public bool MayCreateChild { get; set; }

    /// <summary>Whether the mailbox may be renamed.</summary>
    [JsonPropertyName("mayRename")]
    public bool MayRename { get; set; }

    /// <summary>Whether the mailbox may be deleted.</summary>
    [JsonPropertyName("mayDelete")]
    public bool MayDelete { get; set; }

    /// <summary>Whether messages may be submitted using this mailbox.</summary>
    [JsonPropertyName("maySubmit")]
    public bool MaySubmit { get; set; }
}

/// <summary>JMAP email address.</summary>
public sealed class JmapEmailAddress {
    /// <summary>Display name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Email address.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

/// <summary>Common JMAP Email projection.</summary>
public sealed class JmapEmail {
    /// <summary>Stable email identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Blob identifier for raw content retrieval.</summary>
    [JsonPropertyName("blobId")]
    public string? BlobId { get; set; }

    /// <summary>Owning thread identifier.</summary>
    [JsonPropertyName("threadId")]
    public string? ThreadId { get; set; }

    /// <summary>Mailbox membership keyed by mailbox identifier.</summary>
    [JsonPropertyName("mailboxIds")]
    public Dictionary<string, bool> MailboxIds { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Message keywords keyed by keyword name.</summary>
    [JsonPropertyName("keywords")]
    public Dictionary<string, bool> Keywords { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Message subject.</summary>
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    /// <summary>Sender addresses.</summary>
    [JsonPropertyName("from")]
    public List<JmapEmailAddress> From { get; set; } = new();

    /// <summary>Primary recipient addresses.</summary>
    [JsonPropertyName("to")]
    public List<JmapEmailAddress> To { get; set; } = new();

    /// <summary>Server-reported receipt timestamp.</summary>
    [JsonPropertyName("receivedAt")]
    public DateTimeOffset? ReceivedAt { get; set; }

    /// <summary>Message size in bytes.</summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>Server-generated plain-text preview.</summary>
    [JsonPropertyName("preview")]
    public string? Preview { get; set; }

    /// <summary>Whether the message has an attachment.</summary>
    [JsonPropertyName("hasAttachment")]
    public bool HasAttachment { get; set; }
}

/// <summary>JMAP thread with its email identifiers.</summary>
public sealed class JmapThread {
    /// <summary>Stable thread identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Email identifiers in the thread.</summary>
    [JsonPropertyName("emailIds")]
    public List<string> EmailIds { get; set; } = new();
}

/// <summary>JMAP sending identity.</summary>
public sealed class JmapIdentity {
    /// <summary>Stable identity identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Identity display name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Identity email address.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>Whether the identity may be deleted.</summary>
    [JsonPropertyName("mayDelete")]
    public bool MayDelete { get; set; }
}

/// <summary>Typed JMAP Email/query filter.</summary>
public sealed class JmapEmailFilter {
    /// <summary>Restricts results to one mailbox.</summary>
    [JsonPropertyName("inMailbox")]
    public string? InMailbox { get; set; }

    /// <summary>Matches text across server-supported message fields.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>Matches sender text.</summary>
    [JsonPropertyName("from")]
    public string? From { get; set; }

    /// <summary>Matches recipient text.</summary>
    [JsonPropertyName("to")]
    public string? To { get; set; }

    /// <summary>Matches subject text.</summary>
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    /// <summary>Restricts results to messages received before this timestamp.</summary>
    [JsonPropertyName("before")]
    public DateTimeOffset? Before { get; set; }

    /// <summary>Restricts results to messages received after this timestamp.</summary>
    [JsonPropertyName("after")]
    public DateTimeOffset? After { get; set; }

    /// <summary>Restricts results by attachment presence.</summary>
    [JsonPropertyName("hasAttachment")]
    public bool? HasAttachment { get; set; }
}

/// <summary>Typed JMAP query comparator.</summary>
public sealed class JmapComparator {
    /// <summary>JMAP property name used for ordering.</summary>
    [JsonPropertyName("property")]
    public string Property { get; set; } = "receivedAt";

    /// <summary>Whether values are sorted in ascending order.</summary>
    [JsonPropertyName("isAscending")]
    public bool IsAscending { get; set; }
}

/// <summary>Result of JMAP Email/query.</summary>
public sealed class JmapEmailQueryResult {
    /// <summary>Account that produced the result.</summary>
    [JsonPropertyName("accountId")]
    public string? AccountId { get; set; }

    /// <summary>Opaque query state token.</summary>
    [JsonPropertyName("queryState")]
    public string? QueryState { get; set; }

    /// <summary>Whether the server can calculate query changes.</summary>
    [JsonPropertyName("canCalculateChanges")]
    public bool CanCalculateChanges { get; set; }

    /// <summary>Position of the first returned identifier.</summary>
    [JsonPropertyName("position")]
    public long Position { get; set; }

    /// <summary>Matching email identifiers.</summary>
    [JsonPropertyName("ids")]
    public List<string> Ids { get; set; } = new();

    /// <summary>Total matches when requested and available.</summary>
    [JsonPropertyName("total")]
    public long? Total { get; set; }

    /// <summary>Whether additional matches exist after this page.</summary>
    [JsonIgnore]
    public bool HasMore { get; set; }
}

/// <summary>Result of JMAP Email/get.</summary>
public sealed class JmapEmailGetResult {
    /// <summary>Account that produced the result.</summary>
    [JsonPropertyName("accountId")]
    public string? AccountId { get; set; }

    /// <summary>Current Email collection state.</summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>Returned email objects.</summary>
    [JsonPropertyName("list")]
    public List<JmapEmail> List { get; set; } = new();

    /// <summary>Requested identifiers not found by the server.</summary>
    [JsonPropertyName("notFound")]
    public List<string> NotFound { get; set; } = new();
}

/// <summary>Result of JMAP Email/changes.</summary>
public sealed class JmapEmailChangesResult {
    /// <summary>Account that produced the result.</summary>
    [JsonPropertyName("accountId")]
    public string? AccountId { get; set; }

    /// <summary>State token supplied to the method.</summary>
    [JsonPropertyName("oldState")]
    public string? OldState { get; set; }

    /// <summary>New state token represented by this change page.</summary>
    [JsonPropertyName("newState")]
    public string? NewState { get; set; }

    /// <summary>Whether another bounded change request is required.</summary>
    [JsonPropertyName("hasMoreChanges")]
    public bool HasMoreChanges { get; set; }

    /// <summary>Created email identifiers.</summary>
    [JsonPropertyName("created")]
    public List<string> Created { get; set; } = new();

    /// <summary>Updated email identifiers.</summary>
    [JsonPropertyName("updated")]
    public List<string> Updated { get; set; } = new();

    /// <summary>Destroyed email identifiers.</summary>
    [JsonPropertyName("destroyed")]
    public List<string> Destroyed { get; set; } = new();
}

internal sealed class JmapMailboxGetArguments {
    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("ids")]
    public string[]? Ids { get; set; }
}

internal sealed class JmapMailboxGetResponse {
    [JsonPropertyName("accountId")]
    public string? AccountId { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("list")]
    public List<JmapMailbox> List { get; set; } = new();
}

internal sealed class JmapEmailQueryArguments {
    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("filter")]
    public JmapEmailFilter? Filter { get; set; }

    [JsonPropertyName("sort")]
    public List<JmapComparator>? Sort { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("collapseThreads")]
    public bool CollapseThreads { get; set; }

    [JsonPropertyName("calculateTotal")]
    public bool CalculateTotal { get; set; } = true;
}

internal sealed class JmapEmailGetArguments {
    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("ids")]
    public string[] Ids { get; set; } = Array.Empty<string>();

    [JsonPropertyName("properties")]
    public string[]? Properties { get; set; }
}

internal sealed class JmapEmailChangesArguments {
    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("sinceState")]
    public string SinceState { get; set; } = string.Empty;

    [JsonPropertyName("maxChanges")]
    public int MaxChanges { get; set; }
}

internal sealed class JmapThreadGetArguments {
    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("ids")]
    public string[] Ids { get; set; } = Array.Empty<string>();
}

internal sealed class JmapThreadGetResponse {
    [JsonPropertyName("accountId")]
    public string? AccountId { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("list")]
    public List<JmapThread> List { get; set; } = new();
}

internal sealed class JmapIdentityGetArguments {
    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("ids")]
    public string[]? Ids { get; set; }
}

internal sealed class JmapIdentityGetResponse {
    [JsonPropertyName("accountId")]
    public string? AccountId { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("list")]
    public List<JmapIdentity> List { get; set; } = new();
}
