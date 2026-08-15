using MimeKit;
using OfficeIMO.Email;

namespace Mailozaurr;

/// <summary>Result of projecting an OfficeIMO artifact document into a MimeKit message.</summary>
public sealed class MailFileMimeMessageConversionResult {
    internal MailFileMimeMessageConversionResult(MimeMessage? message, EmailWriteResult officeWriteResult) {
        Message = message;
        OfficeWriteResult = officeWriteResult ?? throw new ArgumentNullException(nameof(officeWriteResult));
    }

    /// <summary>Converted message, or null when OfficeIMO reported a blocking serialization error.</summary>
    public MimeMessage? Message { get; }

    /// <summary>OfficeIMO write result containing byte count, preservation state, and fidelity diagnostics.</summary>
    public EmailWriteResult OfficeWriteResult { get; }

    /// <summary>Structured OfficeIMO preservation and fidelity diagnostics.</summary>
    public IReadOnlyList<EmailDiagnostic> Diagnostics => OfficeWriteResult.Diagnostics;

    /// <summary>True when conversion produced at least one error diagnostic.</summary>
    public bool HasErrors => OfficeWriteResult.HasErrors;

    /// <summary>True when OfficeIMO emitted the preserved source rather than regenerating MIME.</summary>
    public bool UsedPreservedSource => OfficeWriteResult.UsedPreservedSource;

    /// <summary>Number of MIME bytes handed to MimeKit.</summary>
    public long BytesWritten => OfficeWriteResult.BytesWritten;
}

/// <summary>Result of parsing a MimeKit message into an OfficeIMO artifact document.</summary>
/// <remarks>Dispose the result after using file-backed attachment content.</remarks>
public sealed class MailFileEmailDocumentConversionResult : IDisposable {
    private bool _disposed;

    internal MailFileEmailDocumentConversionResult(EmailReadResult officeReadResult) {
        OfficeReadResult = officeReadResult ?? throw new ArgumentNullException(nameof(officeReadResult));
    }

    /// <summary>Converted OfficeIMO email document.</summary>
    public EmailDocument Document => OfficeReadResult.Document;

    /// <summary>OfficeIMO read result that owns diagnostics, budgets, and file-backed attachment lifetime.</summary>
    public EmailReadResult OfficeReadResult { get; }

    /// <summary>Structured OfficeIMO parsing and preservation diagnostics.</summary>
    public IReadOnlyList<EmailDiagnostic> Diagnostics => OfficeReadResult.Diagnostics;

    /// <summary>True when parsing produced at least one error diagnostic.</summary>
    public bool HasErrors => OfficeReadResult.HasErrors;

    /// <summary>Number of serialized MIME bytes consumed by OfficeIMO.</summary>
    public long BytesRead => OfficeReadResult.BytesRead;

    /// <summary>Aggregate bounded-resource evidence from the OfficeIMO reader.</summary>
    public EmailProcessingBudgetSnapshot ProcessingBudget => OfficeReadResult.ProcessingBudget;

    /// <summary>True when retained attachment content is backed by disposable temporary storage.</summary>
    public bool UsesFileBackedContent => OfficeReadResult.UsesFileBackedContent;

    /// <summary>Releases file-backed attachment content owned by the OfficeIMO read result.</summary>
    public void Dispose() {
        if (_disposed) return;
        OfficeReadResult.Dispose();
        _disposed = true;
    }
}
