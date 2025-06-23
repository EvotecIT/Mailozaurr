using System.Management.Automation;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Saves Microsoft Graph email messages to disk in a specified format.</para>
/// <para type="description">The <c>Save-MailMessage</c> cmdlet saves one or more <see cref="GraphEmailMessage"/> objects to disk at the specified path. Use this to archive, export, or process messages retrieved from Microsoft Graph.</para>
/// <example>
///   <summary>Save mail messages to a folder</summary>
///   <code>Get-EmailMessage ... | Save-MailMessage -Path "C:\Archive"</code>
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
    public PSObject[]? Message { get; set; }
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
        foreach (var m in Message) {
            if (m.BaseObject is GraphEmailMessage gm) {
                MicrosoftGraphUtils.SaveMailMessages(new[] { gm }, Path);
            } else if (m.BaseObject is MimeKit.MimeMessage mm) {
                var resolved = System.IO.Path.GetFullPath(Path);
                if (!System.IO.Directory.Exists(resolved)) System.IO.Directory.CreateDirectory(resolved);
                var file = System.IO.Path.Combine(resolved, System.IO.Path.ChangeExtension(System.IO.Path.GetRandomFileName(), "eml"));
                mm.WriteTo(file);
            }
        }
    }
}
