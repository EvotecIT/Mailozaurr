namespace Mailozaurr.Application;

/// <summary>
/// Imports and exports reusable drafts to stable external files.
/// </summary>
public interface IMailDraftExchangeService {
    /// <summary>Loads a draft from an external file.</summary>
    Task<MailDraft> LoadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Saves a draft to an external file.</summary>
    Task SaveAsync(string path, MailDraft draft, CancellationToken cancellationToken = default);
}