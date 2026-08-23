namespace Mailozaurr;

/// <summary>Normalized mailbox change type.</summary>
public enum MailChangeKind {
    /// <summary>A message was created or changed and should be upserted.</summary>
    Upsert,
    /// <summary>A message was removed.</summary>
    Delete
}

/// <summary>One provider-neutral mailbox change.</summary>
public sealed class MailChangeItem {
    /// <summary>Provider-specific stable message identifier.</summary>
    public string MessageId { get; set; } = string.Empty;
    /// <summary>Change operation.</summary>
    public MailChangeKind Kind { get; set; }
    /// <summary>Optional provider-native thread/conversation identifier.</summary>
    public string? ThreadId { get; set; }
    /// <summary>Optional subject available without another message read.</summary>
    public string? Subject { get; set; }
}

/// <summary>Request for one durable change-feed page.</summary>
public sealed class MailChangeFeedRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;
    /// <summary>Optional mailbox selector.</summary>
    public string? MailboxId { get; set; }
    /// <summary>Optional folder or label selector.</summary>
    public string? FolderId { get; set; }
    /// <summary>Graph delta URL or opaque Gmail history cursor. Gmail requires it.</summary>
    public string? Cursor { get; set; }
    /// <summary>Requested provider page size. A provider page may contain more events, which are never discarded.</summary>
    public int MaxChanges { get; set; } = 100;
}

/// <summary>Request for one bounded IMAP IDLE observation.</summary>
public sealed class MailChangeWaitRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;
    /// <summary>Optional folder selector. Defaults to INBOX.</summary>
    public string? FolderId { get; set; }
    /// <summary>Target arrivals before ending the observation. A simultaneous server burst is never truncated.</summary>
    public int MaxChanges { get; set; } = 1;
    /// <summary>Maximum observation duration.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>Normalized change-feed batch and cursor evidence.</summary>
public sealed class MailChangeFeedResult {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;
    /// <summary>Provider kind.</summary>
    public MailProfileKind Provider { get; set; }
    /// <summary>Resolved folder or label selector.</summary>
    public string? FolderId { get; set; }
    /// <summary>Provider cursor to persist for the next durable read.</summary>
    public string? NextCursor { get; set; }
    /// <summary>Cursor semantics: durable-delta, durable-history, or ephemeral-idle.</summary>
    public string CursorKind { get; set; } = string.Empty;
    /// <summary>True when the provider rejected the cursor and a full synchronization is required.</summary>
    public bool ResetRequired { get; set; }
    /// <summary>True when delete events are represented by this feed mode.</summary>
    public bool SupportsDeletes { get; set; }
    /// <summary>Normalized changes.</summary>
    public List<MailChangeItem> Changes { get; set; } = new();
}

/// <summary>Request to create or refresh Graph/Gmail notifications.</summary>
public sealed class MailChangeSubscriptionRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;
    /// <summary>Optional mailbox selector.</summary>
    public string? MailboxId { get; set; }
    /// <summary>Folder or label selectors. Defaults to INBOX.</summary>
    public List<string> FolderIds { get; set; } = new();
    /// <summary>Graph HTTPS notification URL.</summary>
    public string? NotificationUrl { get; set; }
    /// <summary>Graph opaque client state returned with notifications.</summary>
    public string? ClientState { get; set; }
    /// <summary>Graph subscription expiration.</summary>
    public DateTimeOffset? Expiration { get; set; }
    /// <summary>Existing Graph subscription id to renew.</summary>
    public string? SubscriptionId { get; set; }
    /// <summary>Gmail Pub/Sub topic name.</summary>
    public string? TopicName { get; set; }
}

/// <summary>Request to remove Graph/Gmail notifications.</summary>
public sealed class MailChangeUnsubscribeRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;
    /// <summary>Optional mailbox selector, primarily for Gmail watch removal.</summary>
    public string? MailboxId { get; set; }
    /// <summary>Graph subscription id. Gmail does not require one.</summary>
    public string? SubscriptionId { get; set; }
    /// <summary>Whether a missing remote subscription counts as success.</summary>
    public bool TreatMissingAsSuccess { get; set; } = true;
}

/// <summary>Normalized remote notification-subscription evidence.</summary>
public sealed class MailChangeSubscriptionResult {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;
    /// <summary>Provider kind.</summary>
    public MailProfileKind Provider { get; set; }
    /// <summary>True when the requested subscription state was achieved.</summary>
    public bool Succeeded { get; set; }
    /// <summary>True when a delete succeeded because the subscription was already absent.</summary>
    public bool AlreadyMissing { get; set; }
    /// <summary>Graph subscription id when applicable.</summary>
    public string? SubscriptionId { get; set; }
    /// <summary>Graph resource path when applicable.</summary>
    public string? Resource { get; set; }
    /// <summary>Gmail history cursor returned by watch.</summary>
    public string? Cursor { get; set; }
    /// <summary>Remote expiration when available.</summary>
    public DateTimeOffset? Expiration { get; set; }
    /// <summary>Resolved Gmail label ids.</summary>
    public List<string> FolderIds { get; set; } = new();
}
