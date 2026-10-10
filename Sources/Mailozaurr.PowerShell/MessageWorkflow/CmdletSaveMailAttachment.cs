using OfficeIMO.Email;

namespace Mailozaurr.PowerShell;

using EmailMessage = OfficeIMO.Email.EmailMessage;

/// <summary>
/// <para type="synopsis">Saves selected message attachments and inline images.</para>
/// <para type="description">Uses bounded, atomic attachment extraction. By default each message gets its own folder; Flatten uses message-prefixed filenames in one folder. Original filenames remain in PassThru records. Existing files are never overwritten. Source files must remain unchanged.</para>
/// </summary>
/// <example><summary>Save PDFs from messages</summary><code>Get-MailMessage -Path './Mail/*.eml' | Save-MailAttachment -Path './Extracted' -FileName '*.pdf' -PassThru</code></example>
/// <example><summary>Save inline images</summary><code>Get-MailMessage -Path './Mail/invoice.eml' | Get-MailAttachment -InlineOnly -ContentType 'image/*' | Save-MailAttachment -Path './Images' -PassThru</code></example>
[Cmdlet(VerbsData.Save, "MailAttachment", SupportsShouldProcess = true)]
[OutputType(typeof(MailSavedAttachment))]
public sealed class CmdletSaveMailAttachment : MailAttachmentCmdletBase {
    /// <summary>Destination directory.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;
    /// <summary>Saves all messages into one directory with source-specific filename prefixes.</summary>
    [Parameter]
    public SwitchParameter Flatten { get; set; }
    /// <summary>Returns saved paths, attachment metadata and source provenance.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }
    /// <summary>Saves matching attachment groups.</summary>
    protected override Task ProcessRecordAsync() {
        try {
            string root = GetUnresolvedProviderPathFromPSPath(Path);
            foreach (var group in GetAttachments().GroupBy(a => a.Message)) {
                EmailMessage message = group.Key;
                string destination = Flatten.IsPresent ? root : System.IO.Path.Combine(root, message.ExportName);
                if (!ShouldProcess(destination, "Save mail attachments")) continue;
                EmailAttachmentExtractionResult result = message.SaveAttachments(destination, group.Select(a => a.Index),
                    CancelToken, Flatten.IsPresent ? message.ExportName : null);
                foreach (EmailDiagnostic diagnostic in result.Diagnostics) WriteWarning($"{diagnostic.Code}: {diagnostic.Message}");
                foreach (EmailAttachmentExtractionEntry entry in result.Entries) {
                    foreach (EmailDiagnostic diagnostic in entry.Diagnostics) {
                        if (diagnostic.Severity == EmailDiagnosticSeverity.Error)
                            WriteError(new ErrorRecord(new IOException(diagnostic.Message), diagnostic.Code, ErrorCategory.WriteError, destination));
                        else WriteWarning($"{diagnostic.Code}: {diagnostic.Message}");
                    }
                    if (entry.OutputPath != null && PassThru.IsPresent) WriteObject(new MailSavedAttachment(
                        message.Attachments[int.Parse(entry.SourcePath, System.Globalization.CultureInfo.InvariantCulture)], entry));
                }
            }
        } catch (Exception exception) when (exception is not OperationCanceledException && exception is not PipelineStoppedException) {
            WriteError(new ErrorRecord(exception, "MailAttachmentSaveFailed", ErrorCategory.WriteError, InputObject));
        }
        return Task.CompletedTask;
    }
}
