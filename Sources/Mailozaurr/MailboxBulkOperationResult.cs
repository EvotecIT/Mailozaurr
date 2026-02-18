namespace Mailozaurr;

/// <summary>
/// Provider-agnostic result item for mailbox bulk operations.
/// </summary>
public class MailboxBulkOperationResult {
    /// <summary>Original message/thread/conversation identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>True when the operation completed successfully for this identifier.</summary>
    public bool Ok { get; set; }

    /// <summary>Error text when <see cref="Ok"/> is false.</summary>
    public string? Error { get; set; }
}
