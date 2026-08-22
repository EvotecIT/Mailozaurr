namespace Mailozaurr;

/// <summary>Provider-neutral request for exporting messages as lossless EML artifacts.</summary>
public sealed class MailEmlExportRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Optional mailbox identifier for multi-mailbox providers.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional folder identifier or folder path.</summary>
    public string? FolderId { get; set; }

    /// <summary>Provider-specific message identifiers.</summary>
    public List<string> MessageIds { get; set; } = new();

    /// <summary>Destination directory. It is created when necessary.</summary>
    public string DestinationDirectory { get; set; } = string.Empty;

    /// <summary>Whether an existing deterministic destination may be replaced.</summary>
    public bool Overwrite { get; set; }

    /// <summary>Maximum provider payload size per message.</summary>
    public long MaxMessageBytes { get; set; } = 64L * 1024L * 1024L;
}

/// <summary>Result for one requested EML artifact.</summary>
public sealed class MailEmlExportItemResult : OperationResult {
    /// <summary>Provider-specific message identifier.</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Full destination path when one was selected.</summary>
    public string? DestinationPath { get; set; }

    /// <summary>Number of provider bytes written.</summary>
    public long BytesWritten { get; set; }

    /// <summary>SHA-256 digest of the provider content.</summary>
    public string? Sha256 { get; set; }

    /// <summary>Whether OfficeIMO.Email emitted the retained provider source verbatim.</summary>
    public bool UsedPreservedSource { get; set; }

    /// <summary>Structured OfficeIMO.Email diagnostic codes.</summary>
    public List<string> DiagnosticCodes { get; set; } = new();
}

/// <summary>Aggregate result for a provider-neutral EML batch export.</summary>
public sealed class MailEmlExportResult : OperationResult {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Full destination directory.</summary>
    public string DestinationDirectory { get; set; } = string.Empty;

    /// <summary>Number of distinct requested messages.</summary>
    public int RequestedCount { get; set; }

    /// <summary>Number of successfully exported messages.</summary>
    public int ExportedCount { get; set; }

    /// <summary>Number of failed messages.</summary>
    public int FailedCount { get; set; }

    /// <summary>Per-message evidence.</summary>
    public List<MailEmlExportItemResult> Results { get; set; } = new();
}
