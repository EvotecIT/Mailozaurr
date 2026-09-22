namespace Mailozaurr;

/// <summary>An explicit, immutable inventory of message IDs to archive as verified EML files.</summary>
public sealed class MailEmlArchiveRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Optional mailbox identifier.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional folder identifier.</summary>
    public string? FolderId { get; set; }

    /// <summary>Complete selected inventory. Resume requires the same set of IDs.</summary>
    public List<string> MessageIds { get; set; } = new();

    /// <summary>Archive root, containing EML content and verification records.</summary>
    public string DestinationDirectory { get; set; } = string.Empty;

    /// <summary>Maximum provider message size.</summary>
    public long MaxMessageBytes { get; set; } = 64L * 1024L * 1024L;
}

/// <summary>Verified archive progress for one explicit inventory.</summary>
public sealed class MailEmlArchiveResult : OperationResult {
    /// <summary>Archive root.</summary>
    public string DestinationDirectory { get; set; } = string.Empty;

    /// <summary>Number of distinct selected message IDs.</summary>
    public int RequestedCount { get; set; }

    /// <summary>Files verified and retained from an earlier run.</summary>
    public int VerifiedExistingCount { get; set; }

    /// <summary>Files exported and recorded in this run.</summary>
    public int ExportedCount { get; set; }

    /// <summary>Messages that could not be archived.</summary>
    public int FailedCount { get; set; }

    /// <summary>Failed message IDs with provider diagnostics.</summary>
    public List<MailEmlArchiveFailure> Failures { get; set; } = new();
}

/// <summary>One failed archive item.</summary>
public sealed class MailEmlArchiveFailure {
    /// <summary>Provider message ID.</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Machine-readable failure code.</summary>
    public string? Code { get; set; }

    /// <summary>Diagnostic text.</summary>
    public string? Message { get; set; }
}
