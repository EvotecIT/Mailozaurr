namespace Mailozaurr;

/// <summary>
/// Handles normalized read operations for a specific profile kind.
/// </summary>
public interface IMailReadHandler {
    /// <summary>Profile kind handled by this instance.</summary>
    MailProfileKind Kind { get; }

    /// <summary>Lists folders.</summary>
    Task<IReadOnlyList<FolderRef>> GetFoldersAsync(MailProfile profile, MailFolderQuery query, CancellationToken cancellationToken = default);

    /// <summary>Searches messages.</summary>
    Task<IReadOnlyList<MessageSummary>> SearchAsync(MailProfile profile, MailSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>Gets a detailed message view.</summary>
    Task<MessageDetail?> GetMessageAsync(MailProfile profile, GetMessageRequest request, CancellationToken cancellationToken = default);

    /// <summary>Saves an attachment.</summary>
    Task<OperationResult> SaveAttachmentAsync(MailProfile profile, SaveAttachmentRequest request, CancellationToken cancellationToken = default);
}