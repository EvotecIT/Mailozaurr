using OfficeIMO.Email.Store;

namespace Mailozaurr.MailboxExport;

/// <summary>Result of a mailbox-to-PST export.</summary>
public sealed class GraphMailboxPstExportReport {
    internal GraphMailboxPstExportReport(string mailbox, IReadOnlyList<GraphMailboxPstExportPart> parts,
        GraphMailboxExportCounts written, int skipped, int failed,
        IReadOnlyList<GraphMailboxExportIssue> issues, IReadOnlyList<EmailStoreDiagnostic> diagnostics,
        bool resumed, TimeSpan duration) {
        Mailbox = mailbox;
        Parts = parts;
        Written = written;
        ItemsSkipped = skipped;
        ItemsFailed = failed;
        Issues = issues;
        NativeDiagnostics = diagnostics;
        Resumed = resumed;
        Duration = duration;
    }

    /// <summary>Exported mailbox.</summary>
    public string Mailbox { get; }

    /// <summary>Completed PST files in part order.</summary>
    public IReadOnlyList<GraphMailboxPstExportPart> Parts { get; }

    /// <summary>Items written, by type.</summary>
    public GraphMailboxExportCounts Written { get; }

    /// <summary>Total items written.</summary>
    public int ItemsWritten => Written.Total;

    /// <summary>Items deliberately not written, such as messages above the size limit.</summary>
    public int ItemsSkipped { get; }

    /// <summary>Items that could not be read or written.</summary>
    public int ItemsFailed { get; }

    /// <summary>Skipped, failed and partially carried items, up to the configured limit.</summary>
    public IReadOnlyList<GraphMailboxExportIssue> Issues { get; }

    /// <summary>Diagnostics reported by the OfficeIMO PST writer.</summary>
    public IReadOnlyList<EmailStoreDiagnostic> NativeDiagnostics { get; }

    /// <summary>Whether this run continued from a checkpoint.</summary>
    public bool Resumed { get; }

    /// <summary>Elapsed time of this run.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Total size of all parts in bytes.</summary>
    public long TotalBytes => Parts.Sum(part => part.Bytes);

    /// <summary>True when no item failed and every verified part matched its written item count.</summary>
    public bool IsSuccessful => ItemsFailed == 0 && Parts.All(part => part.Verified != false);
}

/// <summary>One completed PST file.</summary>
public sealed class GraphMailboxPstExportPart {
    internal GraphMailboxPstExportPart(int number, string path, int itemCount, int folderCount,
        long bytes, long estimatedBytes, string sha256, bool? verified, int? verifiedItemCount) {
        Number = number;
        Path = path;
        ItemCount = itemCount;
        FolderCount = folderCount;
        Bytes = bytes;
        EstimatedBytes = estimatedBytes;
        Sha256 = sha256;
        Verified = verified;
        VerifiedItemCount = verifiedItemCount;
    }

    /// <summary>One-based part number.</summary>
    public int Number { get; }

    /// <summary>Absolute PST path.</summary>
    public string Path { get; }

    /// <summary>Items written to this part.</summary>
    public int ItemCount { get; }

    /// <summary>Folders created in this part.</summary>
    public int FolderCount { get; }

    /// <summary>File size in bytes.</summary>
    public long Bytes { get; }

    /// <summary>Size estimate used for splitting.</summary>
    public long EstimatedBytes { get; }

    /// <summary>SHA-256 of the file, as uppercase hexadecimal.</summary>
    public string Sha256 { get; }

    /// <summary>True when the reopened part contains the written item count, false when it does not, null when not checked.</summary>
    public bool? Verified { get; }

    /// <summary>Items found when the part was reopened, when verification ran.</summary>
    public int? VerifiedItemCount { get; }
}

/// <summary>Item counts by type.</summary>
public sealed class GraphMailboxExportCounts {
    internal GraphMailboxExportCounts(int messages, int contacts, int events, int tasks) {
        Messages = messages;
        Contacts = contacts;
        Events = events;
        Tasks = tasks;
    }

    /// <summary>Mail messages.</summary>
    public int Messages { get; }

    /// <summary>Contacts.</summary>
    public int Contacts { get; }

    /// <summary>Calendar events.</summary>
    public int Events { get; }

    /// <summary>To Do tasks.</summary>
    public int Tasks { get; }

    /// <summary>All items.</summary>
    public int Total => Messages + Contacts + Events + Tasks;

    /// <inheritdoc />
    public override string ToString() =>
        $"Messages={Messages}; Contacts={Contacts}; Events={Events}; Tasks={Tasks}";
}

/// <summary>Severity of an item issue.</summary>
public enum GraphMailboxExportIssueKind {
    /// <summary>The item was written but some of its data was not carried.</summary>
    Partial,
    /// <summary>The item was deliberately not written.</summary>
    Skipped,
    /// <summary>The item could not be read or written.</summary>
    Failed
}

/// <summary>One item that was skipped, failed or written partially.</summary>
public sealed class GraphMailboxExportIssue {
    internal GraphMailboxExportIssue(GraphMailboxExportIssueKind kind, GraphMailboxExportItemTypes itemType,
        string folderPath, string itemId, string? subject, string message) {
        Kind = kind;
        ItemType = itemType;
        FolderPath = folderPath;
        ItemId = itemId;
        Subject = subject;
        Message = message;
    }

    /// <summary>Issue kind.</summary>
    public GraphMailboxExportIssueKind Kind { get; }

    /// <summary>Item type.</summary>
    public GraphMailboxExportItemTypes ItemType { get; }

    /// <summary>Source folder path.</summary>
    public string FolderPath { get; }

    /// <summary>Source item identifier.</summary>
    public string ItemId { get; }

    /// <summary>Item subject, when known.</summary>
    public string? Subject { get; }

    /// <summary>What happened.</summary>
    public string Message { get; }
}

/// <summary>Export progress.</summary>
public sealed class GraphMailboxPstExportProgress {
    internal GraphMailboxPstExportProgress(string folderPath, int itemsWritten, int itemsSkipped, int itemsFailed,
        long? itemsExpected, int partNumber, long partEstimatedBytes) {
        FolderPath = folderPath;
        ItemsWritten = itemsWritten;
        ItemsSkipped = itemsSkipped;
        ItemsFailed = itemsFailed;
        ItemsExpected = itemsExpected;
        PartNumber = partNumber;
        PartEstimatedBytes = partEstimatedBytes;
    }

    /// <summary>Folder being exported.</summary>
    public string FolderPath { get; }

    /// <summary>Items written so far, including earlier runs of a resumed export.</summary>
    public int ItemsWritten { get; }

    /// <summary>Items skipped so far.</summary>
    public int ItemsSkipped { get; }

    /// <summary>Items failed so far.</summary>
    public int ItemsFailed { get; }

    /// <summary>Message count reported by the selected mail folders, when known. Date filters make it an upper bound.</summary>
    public long? ItemsExpected { get; }

    /// <summary>Part being written.</summary>
    public int PartNumber { get; }

    /// <summary>Size estimate of the current part.</summary>
    public long PartEstimatedBytes { get; }
}
