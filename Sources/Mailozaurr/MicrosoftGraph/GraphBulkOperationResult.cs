namespace Mailozaurr;

/// <summary>
/// Result item for Graph bulk mailbox operations.
/// </summary>
public sealed class GraphBulkOperationResult {
    /// <summary>Original message/conversation identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>True when the operation completed successfully for this id.</summary>
    public bool Ok { get; set; }

    /// <summary>Error text when <see cref="Ok"/> is false.</summary>
    public string? Error { get; set; }
}
