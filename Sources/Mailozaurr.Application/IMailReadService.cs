namespace Mailozaurr.Application;

/// <summary>
/// Provides normalized read-oriented mailbox operations.
/// </summary>
public interface IMailReadService {
    /// <summary>Lists folders or folder-like containers.</summary>
    Task<IReadOnlyList<FolderRef>> GetFoldersAsync(MailFolderQuery query, CancellationToken cancellationToken = default);

    /// <summary>Lists folders or folder-like containers using a lightweight projection.</summary>
    Task<IReadOnlyList<FolderRefCompact>> GetFoldersCompactAsync(MailFolderQuery query, CancellationToken cancellationToken = default);

    /// <summary>Searches for messages.</summary>
    Task<IReadOnlyList<MessageSummary>> SearchAsync(MailSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>Searches for messages using a lightweight projection.</summary>
    Task<IReadOnlyList<MessageSummaryCompact>> SearchCompactAsync(MailSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>Lists attachments associated with a specific message.</summary>
    Task<IReadOnlyList<AttachmentSummary>> GetAttachmentsAsync(ListAttachmentsRequest request, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a detailed message view.</summary>
    Task<MessageDetail?> GetMessageAsync(GetMessageRequest request, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a lightweight detailed message view.</summary>
    Task<MessageDetailCompact?> GetMessageCompactAsync(GetMessageRequest request, CancellationToken cancellationToken = default);

    /// <summary>Saves one or more attachments associated with a specific message.</summary>
    Task<SaveAttachmentsResult> SaveAttachmentsAsync(SaveAttachmentsRequest request, CancellationToken cancellationToken = default);

    /// <summary>Saves an attachment to the requested destination.</summary>
    Task<OperationResult> SaveAttachmentAsync(SaveAttachmentRequest request, CancellationToken cancellationToken = default);
}
