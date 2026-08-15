namespace Mailozaurr.Hosting;

/// <summary>
/// Provides provider-neutral folder alias discovery for reusable adapters.
/// </summary>
public interface IMailFolderAliasService {
    /// <summary>Lists supported folder aliases for a profile.</summary>
    Task<IReadOnlyList<MailFolderAliasSummary>> GetAliasesAsync(
        string profileId,
        string? mailboxId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves a requested folder target to an alias or provider-specific destination.</summary>
    Task<MailFolderTargetResolution> ResolveAsync(
        string profileId,
        string targetFolderId,
        string? mailboxId = null,
        CancellationToken cancellationToken = default);
}