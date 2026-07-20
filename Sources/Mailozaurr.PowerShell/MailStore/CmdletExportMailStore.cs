using OfficeIMO.Email;
using OfficeIMO.Email.Store;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Exports selected items from a PST, OST, OLM, Mbox, EMLX, or mailbox directory.</para>
/// <para type="description">Delegates to OfficeIMO.Email for EML, MSG, OFT, TNEF, Mbox, Maildir, or EMLX output. The source store remains read-only and the native preservation report is returned.</para>
/// <example>
///   <summary>Export an OST to EML files</summary>
///   <code>Export-MailStore -InputObject $data -OutputPath './export' -Format Eml</code>
/// </example>
/// <example>
///   <summary>Export one folder hierarchy to Mbox</summary>
///   <code>Export-MailStore -InputObject $data -OutputPath './archive.mbox' -Format Mbox -FolderId $folder.Id -IncludeDescendants</code>
/// </example>
/// </summary>
[Cmdlet(VerbsData.Export, "MailStore", SupportsShouldProcess = true)]
[OutputType(typeof(EmailStoreExportReport), typeof(EmailStoreMboxExportReport))]
public sealed class CmdletExportMailStore : MailStoreCmdletBase {
    /// <summary>An Import-MailData result containing a store, or a native EmailStoreSession.</summary>
    [Parameter(Mandatory = true)]
    [Alias("Store")]
    [ValidateNotNull]
    public object? InputObject { get; set; }

    /// <summary>Destination file or directory.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("Path", "DestinationPath")]
    [ValidateNotNullOrEmpty]
    public string? OutputPath { get; set; }

    /// <summary>Output format: Eml, Msg, Oft, Tnef, Mbox, Maildir, or Emlx.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateSet("Eml", "Msg", "Oft", "Tnef", "Mbox", "Maildir", "Emlx")]
    public string Format { get; set; } = "Eml";

    /// <summary>Optional stable source folder identifier.</summary>
    [Parameter]
    public string? FolderId { get; set; }

    /// <summary>Includes descendants of FolderId.</summary>
    [Parameter]
    public SwitchParameter IncludeDescendants { get; set; }

    /// <summary>Includes folder-associated information items.</summary>
    [Parameter]
    public SwitchParameter IncludeAssociatedItems { get; set; }

    /// <summary>Includes recoverable items absent from normal folder tables.</summary>
    [Parameter]
    public SwitchParameter IncludeOrphanedItems { get; set; }

    /// <summary>Flattens folder hierarchy for directory-based output.</summary>
    [Parameter]
    public SwitchParameter Flatten { get; set; }

    /// <summary>Suppresses preservation-manifest output for directory-based exports.</summary>
    [Parameter]
    public SwitchParameter NoManifest { get; set; }

    /// <summary>Stops after the first item read or write failure.</summary>
    [Parameter]
    public SwitchParameter StopOnError { get; set; }

    /// <summary>Allows existing destination artifacts to be replaced.</summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    /// <summary>Maximum source items attempted.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxItems { get; set; } = 100_000;

    /// <summary>Executes the selected OfficeIMO.Email export workflow.</summary>
    protected override Task ProcessRecordAsync() {
        string? outputPath = OutputPath;
        if (string.IsNullOrWhiteSpace(outputPath)) return Task.CompletedTask;
        try {
            EmailStoreSession session = GetStoreSession(InputObject);
            string destination = GetUnresolvedProviderPathFromPSPath(outputPath);
            if (!ShouldProcess(destination, $"Export mail store as {Format}")) return Task.CompletedTask;

            object report;
            IReadOnlyList<EmailStoreDiagnostic> diagnostics;
            if (Format.Equals("Mbox", StringComparison.OrdinalIgnoreCase)) {
                var options = new EmailStoreMboxExportOptions(
                    FolderId,
                    IncludeDescendants.IsPresent,
                    IncludeAssociatedItems.IsPresent,
                    IncludeOrphanedItems.IsPresent,
                    Force.IsPresent,
                    !StopOnError.IsPresent,
                    MaxItems);
                EmailStoreMboxExportReport result = session.ExportToMbox(destination, options, CancelToken);
                report = result;
                diagnostics = result.Diagnostics;
            } else if (Format.Equals("Maildir", StringComparison.OrdinalIgnoreCase) ||
                       Format.Equals("Emlx", StringComparison.OrdinalIgnoreCase)) {
                EmailStoreNativeDirectoryFormat nativeFormat = Format.Equals("Maildir", StringComparison.OrdinalIgnoreCase)
                    ? EmailStoreNativeDirectoryFormat.Maildir
                    : EmailStoreNativeDirectoryFormat.Emlx;
                var options = new EmailStoreNativeDirectoryExportOptions(
                    nativeFormat,
                    FolderId,
                    IncludeDescendants.IsPresent,
                    IncludeAssociatedItems.IsPresent,
                    IncludeOrphanedItems.IsPresent,
                    !Flatten.IsPresent,
                    Force.IsPresent,
                    !StopOnError.IsPresent,
                    !NoManifest.IsPresent,
                    MaxItems);
                EmailStoreExportReport result = session.ExportToNativeDirectory(destination, options, CancelToken);
                report = result;
                diagnostics = result.Diagnostics;
            } else {
                EmailFileFormat emailFormat = Format.ToUpperInvariant() switch {
                    "EML" => EmailFileFormat.Eml,
                    "MSG" => EmailFileFormat.OutlookMsg,
                    "OFT" => EmailFileFormat.OutlookTemplate,
                    "TNEF" => EmailFileFormat.Tnef,
                    _ => throw new PSArgumentException($"Unsupported mail-store export format '{Format}'.")
                };
                var options = new EmailStoreExportOptions(
                    emailFormat,
                    FolderId,
                    IncludeDescendants.IsPresent,
                    IncludeAssociatedItems.IsPresent,
                    IncludeOrphanedItems.IsPresent,
                    !Flatten.IsPresent,
                    Force.IsPresent,
                    !StopOnError.IsPresent,
                    !NoManifest.IsPresent,
                    MaxItems);
                EmailStoreExportReport result = session.ExportToDirectory(destination, options, CancelToken);
                report = result;
                diagnostics = result.Diagnostics;
            }
            WriteObject(report);
            WriteStoreDiagnostics(diagnostics, destination);
        } catch (OperationCanceledException) when (CancelToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            WriteError(new ErrorRecord(exception, "MailStoreExportFailed", ErrorCategory.WriteError, outputPath));
        }
        return Task.CompletedTask;
    }
}
