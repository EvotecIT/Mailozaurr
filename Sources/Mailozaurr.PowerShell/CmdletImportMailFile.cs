using OfficeIMO.Email;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Imports a .msg or .eml mail file and returns its contents as a message object.</para>
/// <para type="description">The <c>Import-MailFile</c> cmdlet loads a .msg (Outlook) or .eml (RFC822) file from disk and returns a <see cref="MailFileMessage"/> for further processing, inspection, or conversion.</para>
/// <example>
///   <summary>Import a .msg file</summary>
///   <code>Import-MailFile "C:\Mail\message.msg"</code>
/// </example>
/// <example>
///   <summary>Import a .eml file</summary>
///   <code>Import-MailFile "C:\Mail\message.eml"</code>
/// </example>
/// <remarks>
/// Use this cmdlet to inspect, convert, or process mail files in automation or 
/// migration scenarios.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsData.Import, "MailFile")]
public sealed class CmdletImportMailFile : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the path to the .msg or .eml file to 
    /// import. Accepts aliases FilePath and Path.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("FilePath", "Path")]
    [ValidateNotNullOrEmpty]
    public string? InputPath { get; set; }

    /// <summary>Includes the merged source headers in the compatibility projection.</summary>
    [Parameter]
    public SwitchParameter IncludeHeaders { get; set; }

    /// <summary>Omits attachments from the compatibility projection.</summary>
    [Parameter]
    public SwitchParameter ExcludeAttachments { get; set; }

    /// <summary>Retains attachment metadata while omitting decoded attachment bytes.</summary>
    [Parameter]
    public SwitchParameter ExcludeAttachmentContent { get; set; }

    /// <summary>Verifies S/MIME signatures and projects signer metadata when present.</summary>
    [Parameter]
    public SwitchParameter VerifySignature { get; set; }

    /// <summary>
    /// Imports the specified mail file and returns its contents as a message object.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        var inputPath = InputPath;
        if (string.IsNullOrWhiteSpace(inputPath)) {
            WriteWarning("Import-MailFile - File path is empty.");
            return;
        }

        var options = new MailFileReaderOptions {
            IncludeAttachments = !ExcludeAttachments.IsPresent,
            IncludeAttachmentContent = !ExcludeAttachmentContent.IsPresent,
            IncludeHeaders = IncludeHeaders.IsPresent,
            VerifySignature = this.VerifySignature.IsPresent
        };
        try {
            MailFileMessage message = await MailFileMessage.LoadAsync(inputPath!, options, CancelToken)
                .ConfigureAwait(false);
            foreach (EmailDiagnostic diagnostic in message.Diagnostics) {
                string diagnosticMessage = $"{diagnostic.Code}: {diagnostic.Message}";
                if (diagnostic.Severity == EmailDiagnosticSeverity.Warning) {
                    WriteWarning(diagnosticMessage);
                } else {
                    WriteVerbose(diagnosticMessage);
                }
            }
            WriteObject(message);
        } catch (OperationCanceledException) when (CancelToken.IsCancellationRequested) {
            throw;
        } catch (NotSupportedException exception) {
            WriteError(new ErrorRecord(exception, "MailFileTypeNotSupported", ErrorCategory.InvalidArgument, inputPath));
        } catch (FileNotFoundException exception) {
            WriteError(new ErrorRecord(exception, "MailFileNotFound", ErrorCategory.ObjectNotFound, inputPath));
        } catch (InvalidDataException exception) {
            WriteError(new ErrorRecord(exception, "MailFileInvalid", ErrorCategory.InvalidData, inputPath));
        } catch (FormatException exception) {
            WriteError(new ErrorRecord(exception, "MailFileFormatMismatch", ErrorCategory.InvalidData, inputPath));
        } catch (Exception ex) {
            WriteError(new ErrorRecord(ex, "MailFileImportFailed", ErrorCategory.ReadError, inputPath));
        }
    }
}
