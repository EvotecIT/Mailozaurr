namespace Mailozaurr.Hosting;

/// <summary>
/// Aggregate dry-run result for a planned message delete.
/// </summary>
public sealed class DeleteMessagesPreview : OperationResult {
    /// <summary>Owning profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Owning mailbox identifier when relevant.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional source folder identifier.</summary>
    public string? FolderId { get; set; }

    /// <summary>Total raw message identifiers provided in the request.</summary>
    public int RequestedCount { get; set; }

    /// <summary>Total unique, non-empty message identifiers after normalization.</summary>
    public int UniqueMessageCount { get; set; }

    /// <summary>Total duplicate or empty message identifiers removed during normalization.</summary>
    public int DuplicateOrEmptyCount { get; set; }

    /// <summary>The normalized unique message identifiers that would be acted on.</summary>
    public List<string> MessageIds { get; set; } = new();

    /// <summary>Optional confirmation token that can be supplied when executing the delete.</summary>
    public string? ConfirmationToken { get; set; }

    /// <summary>Warnings detected during preview.</summary>
    public List<string> Warnings { get; set; } = new();
}