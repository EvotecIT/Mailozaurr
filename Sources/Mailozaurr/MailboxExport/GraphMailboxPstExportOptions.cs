using OfficeIMO.Email.Store;

namespace Mailozaurr.MailboxExport;

/// <summary>Item types exported from a mailbox.</summary>
[Flags]
public enum GraphMailboxExportItemTypes {
    /// <summary>Nothing.</summary>
    None = 0,
    /// <summary>Mail folders and their messages.</summary>
    Mail = 1,
    /// <summary>Contact folders and contacts.</summary>
    Contacts = 2,
    /// <summary>Calendars and events.</summary>
    Calendar = 4,
    /// <summary>Microsoft To Do task lists and tasks.</summary>
    Tasks = 8,
    /// <summary>Every supported item type.</summary>
    All = Mail | Contacts | Calendar | Tasks
}

/// <summary>Options for <see cref="GraphMailboxPstExporter"/>.</summary>
public sealed class GraphMailboxPstExportOptions {
    /// <summary>Mailbox identity recorded in checkpoints and used as the default PST display name.</summary>
    public string Mailbox { get; set; } = string.Empty;

    /// <summary>Destination PST path. With splitting, parts are named <c>name.partNNN.pst</c> next to it.</summary>
    public string DestinationPath { get; set; } = string.Empty;

    /// <summary>
    /// Size-based split settings. Only <see cref="EmailStorePstSplitOptions.MaxEstimatedBytesPerPart"/>,
    /// <see cref="EmailStorePstSplitOptions.PerItemOverheadBytes"/> and <see cref="EmailStorePstSplitOptions.MaxParts"/>
    /// apply to a mailbox export. Null writes one PST.
    /// </summary>
    public EmailStorePstSplitOptions? Split { get; set; }

    /// <summary>Item types to export.</summary>
    public GraphMailboxExportItemTypes ItemTypes { get; set; } = GraphMailboxExportItemTypes.Mail;

    /// <summary>
    /// Folder path patterns to include, with <c>*</c> and <c>?</c> wildcards. A folder is selected when it or one of
    /// its parents matches. Paths use backslashes, for example <c>Inbox\Projects</c>. Empty selects every folder.
    /// </summary>
    public IList<string> IncludeFolders { get; } = new List<string>();

    /// <summary>Folder path patterns to exclude. A folder is excluded when it or one of its parents matches.</summary>
    public IList<string> ExcludeFolders { get; } = new List<string>();

    /// <summary>Inclusive lower bound for message received time and event dates.</summary>
    public DateTimeOffset? StartDate { get; set; }

    /// <summary>Exclusive upper bound for message received time and event dates.</summary>
    public DateTimeOffset? EndDate { get; set; }

    /// <summary>Largest message downloaded, in bytes. Larger messages are skipped and reported.</summary>
    public long MaxItemBytes { get; set; } = 150L * 1024L * 1024L;

    /// <summary>PST display name. Defaults to the mailbox identity.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Allows existing destination files to be replaced.</summary>
    public bool Overwrite { get; set; }

    /// <summary>Checkpoint path, or null to disable resumable checkpoints.</summary>
    public string? CheckpointPath { get; set; }

    /// <summary>Number of written items between durable checkpoints.</summary>
    public int CheckpointIntervalItems { get; set; } = 500;

    /// <summary>Resumes from <see cref="CheckpointPath"/> when it exists.</summary>
    public bool Resume { get; set; }

    /// <summary>Reopens each completed part and checks its item count.</summary>
    public bool Verify { get; set; } = true;

    /// <summary>Stops on the first item that cannot be read or written instead of recording it and continuing.</summary>
    public bool StopOnItemError { get; set; }

    /// <summary>Largest number of item failures kept in the report. Counts stay exact.</summary>
    public int MaxReportedFailures { get; set; } = 1000;

    /// <summary>Optional progress receiver.</summary>
    public IProgress<GraphMailboxPstExportProgress>? Progress { get; set; }
}
