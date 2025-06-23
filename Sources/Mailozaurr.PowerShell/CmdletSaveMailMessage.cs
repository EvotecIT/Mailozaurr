using System.Management.Automation;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Saves Microsoft Graph email messages to disk in a specified format.</para>
/// <para type="description">The <c>Save-MailMessage</c> cmdlet saves one or more <see cref="GraphEmailMessage"/> objects to disk at the specified path. Use this to archive, export, or process messages retrieved from Microsoft Graph.</para>
/// <example>
///   <summary>Save mail messages to a folder</summary>
///   <code>Get-MailMessage ... | Save-MailMessage -Path "C:\Archive"</code>
/// </example>
/// <remarks>
/// Use this cmdlet to export or archive messages for backup, migration, or compliance scenarios.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsData.Save, "MailMessage")]
public class CmdletSaveMailMessage : PSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the <see cref="GraphEmailMessage"/> objects to save. Accepts pipeline input.</para>
    /// </summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [ValidateNotNullOrEmpty]
    public GraphEmailMessage[]? Message { get; set; }
    /// <summary>
    /// <para type="description">Specifies the path where the messages will be saved.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? Path { get; set; }

    /// <summary>
    /// Saves the specified mail messages to disk at the given path.
    /// </summary>
    protected override void ProcessRecord() {
        MicrosoftGraphUtils.SaveMailMessages(Message, Path);
    }
}
