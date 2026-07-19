using OfficeIMO.Email;
using System.Management.Automation;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Exports an imported mail file as EML, MSG, or TNEF.</para>
/// <para type="description">Writes a <see cref="MailFileMessage"/> to a file and disposes the input after an attempted export unless <c>-KeepInputOpen</c> is specified. Previewed or rejected operations leave the input open. The destination extension selects EML, MSG, or TNEF, so the same command handles conversion in either direction.</para>
/// <example>
///   <summary>Convert an Outlook MSG file to EML</summary>
///   <code>Import-MailFile './message.msg' | Export-MailFile './message.eml'</code>
/// </example>
/// <example>
///   <summary>Export a message and return the new file</summary>
///   <code>$mail | Export-MailFile './message.msg' -Force -PassThru</code>
/// </example>
/// </summary>
[Cmdlet(VerbsData.Export, "MailFile", SupportsShouldProcess = true)]
[OutputType(typeof(FileInfo))]
public sealed class CmdletExportMailFile : PSCmdlet {
    /// <summary>Message returned by <c>Import-MailFile</c> or loaded through the .NET API.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [Alias("Message")]
    [ValidateNotNull]
    public MailFileMessage? InputObject { get; set; }

    /// <summary>Destination file. Use .eml, .msg, .tnef, or winmail.dat.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("Path")]
    [ValidateNotNullOrEmpty]
    public string? OutputPath { get; set; }

    /// <summary>Overwrites an existing destination file.</summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    /// <summary>Returns a <see cref="FileInfo"/> for the exported file.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <summary>Keeps the input message open after export so the caller can continue using it.</summary>
    [Parameter]
    public SwitchParameter KeepInputOpen { get; set; }

    /// <summary>Exports the current pipeline message.</summary>
    protected override void ProcessRecord() {
        MailFileMessage? input = InputObject;
        if (input == null) return;
        bool exportAttempted = false;

        try {
            if (string.IsNullOrWhiteSpace(OutputPath)) return;

            string outputPath;
            try {
                outputPath = GetUnresolvedProviderPathFromPSPath(OutputPath!);
            } catch (Exception exception) {
                WriteError(new ErrorRecord(exception, "MailFileOutputPathInvalid", ErrorCategory.InvalidArgument, OutputPath));
                return;
            }

            if (File.Exists(outputPath) && !Force.IsPresent) {
                WriteError(new ErrorRecord(
                    new IOException($"File '{outputPath}' already exists. Use -Force to overwrite it."),
                    "MailFileAlreadyExists", ErrorCategory.ResourceExists, outputPath));
                return;
            }
            if (!ShouldProcess(outputPath, "Export mail file")) return;
            exportAttempted = true;

            EmailWriteResult result;
            try {
                result = input.Save(outputPath);
            } catch (Exception exception) {
                WriteError(new ErrorRecord(exception, "MailFileExportFailed", ErrorCategory.WriteError, outputPath));
                return;
            }

            bool hasErrors = false;
            foreach (EmailDiagnostic diagnostic in result.Diagnostics) {
                string message = $"{diagnostic.Code}: {diagnostic.Message}";
                if (diagnostic.Severity == EmailDiagnosticSeverity.Error) {
                    hasErrors = true;
                    WriteError(new ErrorRecord(new InvalidDataException(message), diagnostic.Code,
                        ErrorCategory.InvalidData, outputPath));
                } else if (diagnostic.Severity == EmailDiagnosticSeverity.Warning) {
                    WriteWarning(message);
                } else {
                    WriteVerbose(message);
                }
            }
            if (!hasErrors && PassThru.IsPresent) WriteObject(new FileInfo(outputPath));
        } finally {
            if (exportAttempted && !KeepInputOpen.IsPresent) input.Dispose();
            InputObject = null;
        }
    }
}
