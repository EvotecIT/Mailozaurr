using Mailozaurr.MailboxExport;
using OfficeIMO.Email.Store;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Exports an Exchange Online mailbox to one or more PST files through Microsoft Graph.</para>
/// <para type="description">Reads the mailbox folder tree through Microsoft Graph and streams each message's MIME content straight into a new Unicode PST written by OfficeIMO.Email. The folder hierarchy is kept, and Inbox, Sent Items, Drafts, Junk Email and Outbox keep their Outlook roles. The mailbox is only read; nothing in it is changed.</para>
/// <para type="description">SplitSizeMB starts a new numbered part (name.part001.pst, name.part002.pst, ...) before an item would take the current part over the size. Each part is reopened and its item count checked, and the report lists every part with its size and SHA-256 hash.</para>
/// <para type="description">Mail is always exported. IncludeContacts, IncludeCalendar and IncludeTasks add contacts, calendar events and Microsoft To Do tasks as typed Outlook items. Recurring events keep their recurrence pattern; modified and cancelled occurrences of a series, and task recurrence, are not carried and are listed as partial items in the report.</para>
/// <para type="description">By default the export keeps a checkpoint next to the output. If it is interrupted, run the same command with -Resume to continue where it stopped without duplicating items. Use -NoCheckpoint to turn this off.</para>
/// <para type="description">Required Microsoft Graph permissions. Application (app-only) access: Mail.Read, plus Contacts.Read for IncludeContacts, Calendars.Read for IncludeCalendar and Tasks.Read.All for IncludeTasks. Delegated access to the signed-in user's own mailbox: Mail.Read, Contacts.Read, Calendars.Read and Tasks.Read. Delegated access to another mailbox the signed-in user can open: Mail.Read.Shared, Contacts.Read.Shared and Calendars.Read.Shared; Microsoft To Do tasks of another user are only available with application access. To limit an application to selected mailboxes, use RBAC for Applications in Exchange Online or an application access policy.</para>
/// <para type="description">The PST writer creates its own Deleted Items folder, so the mailbox's Deleted Items is written as a separate folder with the same name.</para>
/// </summary>
/// <example>
///   <summary>Export a mailbox to 5 GB PST parts</summary>
///   <code>Connect-EmailGraph -ClientId $clientId -CertificatePath './app.pfx' -CertificatePassword $pfxPassword -DirectoryId $tenantId
/// Export-GraphMailboxToPst -Identity 'user@contoso.com' -OutputPath 'C:\Export' -SplitSizeMB 5120</code>
///   <para>Writes C:\Export\user@contoso.com.part001.pst, part002.pst and so on, and returns the export report.</para>
/// </example>
/// <example>
///   <summary>Export last year's Inbox and Sent Items with contacts and calendar</summary>
///   <code>Export-GraphMailboxToPst -Identity 'user@contoso.com' -OutputPath 'C:\Export\user-2025.pst' -IncludeFolder 'Inbox', 'Sent Items' -StartDate '2025-01-01' -EndDate '2026-01-01' -IncludeContacts -IncludeCalendar</code>
///   <para>IncludeFolder patterns match folder paths such as Inbox\Projects and also select the folder's subfolders.</para>
/// </example>
/// <example>
///   <summary>Continue an interrupted export</summary>
///   <code>Export-GraphMailboxToPst -Identity 'user@contoso.com' -OutputPath 'C:\Export' -SplitSizeMB 5120 -Resume</code>
///   <para>Run with the same parameters as the interrupted export.</para>
/// </example>
[Cmdlet(VerbsData.Export, "GraphMailboxToPst", SupportsShouldProcess = true)]
[OutputType(typeof(GraphMailboxPstExportReport))]
public sealed class CmdletExportGraphMailboxToPst : MailStoreCmdletBase {
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>User principal name, SMTP address or object id of the mailbox to export.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("UserPrincipalName", "Mailbox")]
    [ValidateNotNullOrEmpty]
    public string? Identity { get; set; }

    /// <summary>Destination PST file, or a directory in which the PST is named after the mailbox.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [Alias("Path", "DestinationPath")]
    [ValidateNotNullOrEmpty]
    public string? OutputPath { get; set; }

    /// <summary>Largest size of one PST part in megabytes. Without it a single PST is written.</summary>
    [Parameter]
    [ValidateRange(1, 1_000_000)]
    public int SplitSizeMB { get; set; }

    /// <summary>Largest number of parts. The export stops with an error if it needs more.</summary>
    [Parameter]
    [ValidateRange(1, 10_000)]
    public int MaxParts { get; set; } = 1000;

    /// <summary>Folder path patterns to export, such as 'Inbox' or 'Inbox\Projects*'. A matching folder's subfolders are included.</summary>
    [Parameter]
    [SupportsWildcards]
    public string[]? IncludeFolder { get; set; }

    /// <summary>Folder path patterns to leave out, such as 'Deleted Items' or 'Junk Email'. A matching folder's subfolders are left out too.</summary>
    [Parameter]
    [SupportsWildcards]
    public string[]? ExcludeFolder { get; set; }

    /// <summary>Exports messages received on or after this time, and events that end on or after it.</summary>
    [Parameter]
    public DateTime? StartDate { get; set; }

    /// <summary>Exports messages received before this time, and events that start before it.</summary>
    [Parameter]
    public DateTime? EndDate { get; set; }

    /// <summary>Adds the default contacts folder and its subfolders.</summary>
    [Parameter]
    public SwitchParameter IncludeContacts { get; set; }

    /// <summary>Adds every calendar in the mailbox.</summary>
    [Parameter]
    public SwitchParameter IncludeCalendar { get; set; }

    /// <summary>Adds Microsoft To Do task lists.</summary>
    [Parameter]
    public SwitchParameter IncludeTasks { get; set; }

    /// <summary>Largest message downloaded, in megabytes. Larger messages are skipped and listed in the report.</summary>
    [Parameter]
    [ValidateRange(1, 2047)]
    public int MaxItemSizeMB { get; set; } = 150;

    /// <summary>PST display name shown in Outlook. Defaults to the mailbox identity.</summary>
    [Parameter]
    public string? DisplayName { get; set; }

    /// <summary>Continues an interrupted export from its checkpoint.</summary>
    [Parameter]
    public SwitchParameter Resume { get; set; }

    /// <summary>Checkpoint file. Defaults to name.checkpoint next to the output.</summary>
    [Parameter]
    public string? CheckpointPath { get; set; }

    /// <summary>Turns off checkpoints, so an interrupted export cannot be resumed.</summary>
    [Parameter]
    public SwitchParameter NoCheckpoint { get; set; }

    /// <summary>Replaces existing PST files and discards an existing checkpoint.</summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    /// <summary>Stops at the first item that cannot be read or written instead of listing it in the report.</summary>
    [Parameter]
    public SwitchParameter StopOnItemError { get; set; }

    /// <summary>Skips reopening each part to check its item count.</summary>
    [Parameter]
    public SwitchParameter SkipVerification { get; set; }

    /// <summary>Graph connection from Connect-EmailGraph. Defaults to the current session.</summary>
    [Parameter]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    /// <summary>Runs the export.</summary>
    protected override async Task ProcessRecordAsync() {
        string identity = Identity!;
        GraphConnectionInfo? connection = Connection ?? DefaultSessions.GraphSession;
        if (connection == null) {
            WriteWarning("Export-GraphMailboxToPst - Connection not provided and no default session available.");
            return;
        }
        string destination = ResolveDestination(identity);
        if (!ShouldProcess(destination, $"Export mailbox '{identity}' to PST")) return;

        var source = new GraphMailboxExportSource(connection.Credential, identity);

        GraphMailboxPstExportOptions options = BuildOptions(identity, destination);
        var progress = new ThrottledProgress(this, identity);
        options.Progress = progress;
        try {
            GraphMailboxPstExportReport report = await new GraphMailboxPstExporter(source, options)
                .ExportAsync(CancelToken).ConfigureAwait(false);
            progress.Complete();
            foreach (GraphMailboxExportIssue issue in report.Issues) {
                string message = $"{issue.Kind} {issue.ItemType} '{issue.Subject ?? issue.ItemId}' in '{issue.FolderPath}': {issue.Message}";
                if (issue.Kind == GraphMailboxExportIssueKind.Failed) WriteWarning(message);
                else WriteVerbose(message);
            }
            WriteStoreDiagnostics(report.NativeDiagnostics, destination);
            foreach (GraphMailboxPstExportPart part in report.Parts.Where(part => part.Verified == false)) {
                WriteError(new ErrorRecord(
                    new InvalidDataException($"PST part '{part.Path}' contains {part.VerifiedItemCount} items; {part.ItemCount} were written."),
                    "MailboxPstPartVerificationFailed",
                    ErrorCategory.InvalidData,
                    part.Path));
            }
            WriteObject(report);
        } catch (OperationCanceledException) when (CancelToken.IsCancellationRequested) {
            progress.Complete();
            throw;
        } catch (Exception exception) {
            progress.Complete();
            string hint = options.CheckpointPath != null && File.Exists(options.CheckpointPath)
                ? " Run the same command with -Resume to continue."
                : string.Empty;
            WriteError(new ErrorRecord(
                new InvalidOperationException(exception.Message + hint, exception),
                "MailboxPstExportFailed",
                ErrorCategory.WriteError,
                destination));
        }
    }

    private string ResolveDestination(string identity) {
        string path = GetUnresolvedProviderPathFromPSPath(OutputPath!);
        bool isFile = path.EndsWith(".pst", StringComparison.OrdinalIgnoreCase) && !Directory.Exists(path);
        if (isFile) return path;
        return Path.Combine(path, SanitizeFileName(identity) + ".pst");
    }

    internal static string SanitizeFileName(string value) {
        char[] invalid = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (char character in value) builder.Append(Array.IndexOf(invalid, character) >= 0 ? '_' : character);
        return builder.ToString().Trim();
    }

    private GraphMailboxPstExportOptions BuildOptions(string identity, string destination) {
        GraphMailboxExportItemTypes types = GraphMailboxExportItemTypes.Mail;
        if (IncludeContacts.IsPresent) types |= GraphMailboxExportItemTypes.Contacts;
        if (IncludeCalendar.IsPresent) types |= GraphMailboxExportItemTypes.Calendar;
        if (IncludeTasks.IsPresent) types |= GraphMailboxExportItemTypes.Tasks;
        string? checkpoint = NoCheckpoint.IsPresent
            ? null
            : CheckpointPath != null
                ? GetUnresolvedProviderPathFromPSPath(CheckpointPath)
                : Path.ChangeExtension(destination, ".checkpoint");
        var options = new GraphMailboxPstExportOptions {
            Mailbox = identity,
            DestinationPath = destination,
            Split = SplitSizeMB > 0
                ? new EmailStorePstSplitOptions(maxEstimatedBytesPerPart: SplitSizeMB * 1024L * 1024L, maxParts: MaxParts)
                : null,
            ItemTypes = types,
            StartDate = ToOffset(StartDate),
            EndDate = ToOffset(EndDate),
            MaxItemBytes = MaxItemSizeMB * 1024L * 1024L,
            DisplayName = DisplayName,
            Overwrite = Force.IsPresent,
            CheckpointPath = checkpoint,
            Resume = Resume.IsPresent,
            Verify = !SkipVerification.IsPresent,
            StopOnItemError = StopOnItemError.IsPresent
        };
        foreach (string pattern in IncludeFolder ?? Array.Empty<string>()) options.IncludeFolders.Add(pattern);
        foreach (string pattern in ExcludeFolder ?? Array.Empty<string>()) options.ExcludeFolders.Add(pattern);
        return options;
    }

    private static DateTimeOffset? ToOffset(DateTime? value) {
        if (!value.HasValue) return null;
        DateTime date = value.Value;
        return date.Kind == DateTimeKind.Utc ? new DateTimeOffset(date) : new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Local));
    }

    private sealed class ThrottledProgress : IProgress<GraphMailboxPstExportProgress> {
        private readonly CmdletExportGraphMailboxToPst _cmdlet;
        private readonly ProgressRecord _record;
        private DateTime _lastWrite = DateTime.MinValue;

        internal ThrottledProgress(CmdletExportGraphMailboxToPst cmdlet, string identity) {
            _cmdlet = cmdlet;
            _record = new ProgressRecord(1, $"Exporting {identity} to PST", "Reading folders");
        }

        public void Report(GraphMailboxPstExportProgress value) {
            DateTime now = DateTime.UtcNow;
            if (now - _lastWrite < ProgressInterval) return;
            _lastWrite = now;
            _record.StatusDescription =
                $"{value.FolderPath}: {value.ItemsWritten} written, {value.ItemsSkipped} skipped, {value.ItemsFailed} failed (part {value.PartNumber}, ~{value.PartEstimatedBytes / (1024 * 1024)} MB)";
            _record.PercentComplete = value.ItemsExpected is long expected && expected > 0
                ? (int)Math.Min(99, 100L * (value.ItemsWritten + value.ItemsSkipped + value.ItemsFailed) / expected)
                : -1;
            _cmdlet.WriteProgress(_record);
        }

        internal void Complete() {
            _record.RecordType = ProgressRecordType.Completed;
            _cmdlet.WriteProgress(_record);
        }
    }
}
