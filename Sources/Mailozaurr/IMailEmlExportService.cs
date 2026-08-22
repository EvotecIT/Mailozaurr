namespace Mailozaurr;

/// <summary>Exports provider messages as validated, lossless EML artifacts.</summary>
public interface IMailEmlExportService {
    /// <summary>Exports a bounded batch without modifying the provider mailbox.</summary>
    Task<MailEmlExportResult> ExportAsync(
        MailEmlExportRequest request,
        CancellationToken cancellationToken = default);
}
