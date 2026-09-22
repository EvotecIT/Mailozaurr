namespace Mailozaurr;

/// <summary>Resolves provider identity before an archive trusts stored records.</summary>
public interface IMailEmlArchiveScopeProvider {
    /// <summary>Gets the current identity namespace for the selected folder.</summary>
    Task<string> GetArchiveScopeAsync(MailProfile profile, string? mailboxId, string? folderId,
        CancellationToken cancellationToken = default);
}

/// <summary>Exports provider messages as validated, lossless EML artifacts.</summary>
public interface IMailEmlExportService {
    /// <summary>Exports a bounded batch without modifying the provider mailbox.</summary>
    Task<MailEmlExportResult> ExportAsync(
        MailEmlExportRequest request,
        CancellationToken cancellationToken = default);
}

internal interface IMailEmlArchiveBatchSession : IDisposable {
    Task<MailEmlExportResult> ExportAsync(MailEmlExportRequest request,
        CancellationToken cancellationToken);
}
