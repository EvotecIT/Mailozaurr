using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Imports a .msg or .eml mail file and returns its contents as a message object.</para>
/// <para type="description">The <c>Import-MailFile</c> cmdlet loads a .msg (Outlook) or .eml (RFC822) file from disk and returns a <see cref="MailFileMessage"/> for further processing, inspection, or conversion. Supports both file types and provides warnings for unsupported files or errors.</para>
/// <example>
///   <summary>Import a .msg file</summary>
///   <code>Import-MailFile -InputPath "C:\Mail\message.msg"</code>
/// </example>
/// <example>
///   <summary>Import a .eml file</summary>
///   <code>Import-MailFile -InputPath "C:\Mail\message.eml"</code>
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

    /// <summary>
    /// Imports the specified mail file and returns its contents as a message object.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        var inputPath = InputPath;
        if (string.IsNullOrWhiteSpace(inputPath)) {
            WriteWarning("Import-MailFile - File path is empty.");
            return Task.CompletedTask;
        }

        if (MailFileReader.TryRead(inputPath!, out var message, out var error)) {
            WriteObject(message);
        } else {
            WriteWarning($"Import-MailFile - {error}");
        }

        return Task.CompletedTask;
    }
}
