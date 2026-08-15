namespace Mailozaurr.Hosting;

/// <summary>
/// Persists reusable outbound drafts.
/// </summary>
public interface IMailDraftStore {
    /// <summary>Lists all saved drafts.</summary>
    Task<IReadOnlyList<MailDraft>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a saved draft by id.</summary>
    Task<MailDraft?> GetByIdAsync(string draftId, CancellationToken cancellationToken = default);

    /// <summary>Saves or updates a draft.</summary>
    Task SaveAsync(MailDraft draft, CancellationToken cancellationToken = default);

    /// <summary>Removes a saved draft by id.</summary>
    Task<bool> RemoveAsync(string draftId, CancellationToken cancellationToken = default);
}