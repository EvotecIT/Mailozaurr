using OfficeIMO.Email;

namespace Mailozaurr.PowerShell;

using EmailMessage = OfficeIMO.Email.EmailMessage;

/// <summary>
/// <para type="synopsis">Exports messages as EML, MSG, OFT, TNEF, or local HTML copies.</para>
/// <para type="description">Accepts Get-MailMessage views, native documents, or Import-MailFile messages. OutputPath exports one message with format inferred from its extension. OutputDirectory exports message views with portable source-specific names. HTML saves embedded images locally; IncludeAttachments also saves regular files and adds links. Remote resources are blocked. Legacy Import-MailFile input is disposed after an attempted export unless KeepInputOpen is used; Get-MailMessage views remain usable.</para>
/// </summary>
/// <example><summary>Convert an Outlook MSG file to EML</summary><code>Get-MailMessage -Path './message.msg' | Export-MailFile -OutputPath './message.eml' -AllowLoss -PassThru</code><para>AllowLoss accepts the omission of Outlook metadata that EML cannot represent.</para></example>
/// <example><summary>Create a readable local copy</summary><code>Get-MailMessage -Path './Mail/invoice.eml' | Export-MailFile -OutputPath './Invoice.html' -IncludeAttachments -PassThru</code></example>
/// <example><summary>Export several messages</summary><code>Get-MailMessage -Path './Mail/*.eml' | Export-MailFile -OutputDirectory './EmailCopies' -Format Eml -PassThru</code></example>
[Cmdlet(VerbsData.Export, "MailFile", SupportsShouldProcess = true, DefaultParameterSetName = "File")]
[OutputType(typeof(FileInfo))]
public sealed class CmdletExportMailFile : AsyncPSCmdlet {
    private int _inputs;
    /// <summary>Get-MailMessage view, native email document, or Import-MailFile message.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [Alias("Message")]
    [ValidateNotNull]
    public object? InputObject { get; set; }
    /// <summary>Destination file; extension selects the output format.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "File")]
    [Alias("Path")]
    [ValidateNotNullOrEmpty]
    public string? OutputPath { get; set; }
    /// <summary>Destination directory for Get-MailMessage views, using portable message names.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Directory")]
    [ValidateNotNullOrEmpty]
    public string? OutputDirectory { get; set; }
    /// <summary>Batch output format.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Directory")]
    [ValidateSet("Eml", "Msg", "Html", "Oft", "Tnef")]
    public string Format { get; set; } = "Eml";
    /// <summary>Include regular attachments and links in HTML exports, in addition to embedded images.</summary>
    [Parameter]
    public SwitchParameter IncludeAttachments { get; set; }
    /// <summary>Allows native-format conversion to omit unrepresentable source metadata, reporting losses as warnings.</summary>
    [Parameter]
    public SwitchParameter AllowLoss { get; set; }
    /// <summary>Atomically replaces an existing destination file.</summary>
    [Parameter]
    public SwitchParameter Force { get; set; }
    /// <summary>Returns a FileInfo for each committed message copy.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }
    /// <summary>Keeps legacy Import-MailFile input open after an attempted export.</summary>
    [Parameter]
    public SwitchParameter KeepInputOpen { get; set; }

    /// <summary>Exports the current pipeline message.</summary>
    protected override async Task ProcessRecordAsync() {
        object? input = InputObject is PSObject ps ? ps.BaseObject : InputObject;
        bool attempted = false;
        try {
            if (input is not EmailMessage && input is not EmailDocument && input is not MailFileMessage)
                throw new PSArgumentException("InputObject must be a Get-MailMessage view, EmailDocument, or Import-MailFile message.");
            _inputs++;
            if (ParameterSetName == "File" && _inputs > 1)
                throw new PSArgumentException("OutputPath accepts one message. Use OutputDirectory and Format for multiple messages.");
            string output;
            if (ParameterSetName == "Directory") {
                if (input is not EmailMessage message) throw new PSArgumentException("Batch export requires Get-MailMessage views.");
                output = System.IO.Path.Combine(GetUnresolvedProviderPathFromPSPath(OutputDirectory!), message.ExportName + "." + Format.ToLowerInvariant());
            } else output = GetUnresolvedProviderPathFromPSPath(OutputPath!);
            bool html = string.Equals(System.IO.Path.GetExtension(output), ".html", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(System.IO.Path.GetExtension(output), ".htm", StringComparison.OrdinalIgnoreCase);
            if (IncludeAttachments.IsPresent && !html) throw new PSArgumentException("IncludeAttachments applies to HTML copies. Native email exports already include attachments.");
            if (File.Exists(output) && !Force.IsPresent) {
                WriteError(new ErrorRecord(new IOException($"File '{output}' already exists. Use -Force to overwrite it."),
                    "MailFileAlreadyExists", ErrorCategory.ResourceExists, output));
                return;
            }
            if (!ShouldProcess(output, "Export mail file")) return;
            attempted = true;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output)!);
            IEnumerable<EmailDiagnostic> diagnostics;
            if (html) {
                EmailHtmlExportResult result = input is EmailMessage view
                    ? EmailHtmlExporter.Export(view, output, IncludeAttachments.IsPresent, Force.IsPresent, CancelToken)
                    : EmailHtmlExporter.Export(input is MailFileMessage legacy ? legacy.OfficeDocument : (EmailDocument)input,
                        output, IncludeAttachments.IsPresent, Force.IsPresent, CancelToken);
                diagnostics = result.Diagnostics;
            } else {
                var options = new EmailWriterOptions(AllowLoss.IsPresent ? EmailConversionLossPolicy.Warn : EmailConversionLossPolicy.Block);
                EmailWriteResult result = input is EmailMessage view ? view.Save(output, Force.IsPresent, CancelToken, options) :
                    await (input is MailFileMessage legacy ? legacy.OfficeDocument : (EmailDocument)input)
                        .SaveWithConflictPolicyAsync(output, Force.IsPresent ? EmailFileConflictPolicy.Replace : EmailFileConflictPolicy.FailIfExists,
                            options, CancelToken);
                diagnostics = result.Diagnostics;
            }
            bool hasErrors = false;
            foreach (EmailDiagnostic diagnostic in diagnostics) {
                string text = $"{diagnostic.Code}: {diagnostic.Message}";
                if (diagnostic.Severity == EmailDiagnosticSeverity.Error) {
                    hasErrors = true;
                    WriteError(new ErrorRecord(new InvalidDataException(text), diagnostic.Code, ErrorCategory.InvalidData, output));
                } else if (diagnostic.Severity == EmailDiagnosticSeverity.Warning) WriteWarning(text);
                else WriteVerbose(text);
            }
            if (!hasErrors && PassThru.IsPresent) WriteObject(new FileInfo(output));
        } catch (Exception exception) when (exception is not OperationCanceledException && exception is not PipelineStoppedException) {
            WriteError(new ErrorRecord(exception, "MailFileExportFailed", ErrorCategory.WriteError, InputObject));
        } finally {
            if (attempted && !KeepInputOpen.IsPresent && input is MailFileMessage legacy) legacy.Dispose();
            InputObject = null;
        }
    }
}
