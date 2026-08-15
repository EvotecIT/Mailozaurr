namespace Mailozaurr.Hosting;

/// <summary>
/// Provides reusable draft lifecycle operations.
/// </summary>
public interface IMailDraftService {
    /// <summary>Lists all saved drafts.</summary>
    Task<IReadOnlyList<MailDraft>> GetDraftsAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists all saved drafts using a lightweight projection.</summary>
    Task<IReadOnlyList<MailDraftCompact>> GetDraftsCompactAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a draft by id.</summary>
    Task<MailDraft?> GetDraftAsync(string draftId, CancellationToken cancellationToken = default);

    /// <summary>Gets a draft by id using a lightweight projection.</summary>
    Task<MailDraftCompact?> GetDraftCompactAsync(string draftId, CancellationToken cancellationToken = default);

    /// <summary>Saves or updates a draft.</summary>
    Task<OperationResult> SaveAsync(MailDraft draft, CancellationToken cancellationToken = default);

    /// <summary>Deletes a draft by id.</summary>
    Task<OperationResult> DeleteAsync(string draftId, CancellationToken cancellationToken = default);
}