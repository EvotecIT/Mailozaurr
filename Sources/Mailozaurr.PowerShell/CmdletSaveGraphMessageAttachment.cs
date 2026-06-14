using Mailozaurr;
using System.Management.Automation;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Saves attachments from Microsoft Graph message objects.
/// </summary>
[Cmdlet(VerbsData.Save, "GraphMessageAttachment")]
public class CmdletSaveGraphMessageAttachment : PSCmdlet {
    /// <summary>
    /// Attachments to save from the message.
    /// </summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [ValidateNotNullOrEmpty]
    public Attachment[]? Attachment { get; set; }

    /// <summary>
    /// Destination path for saved attachments.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? Path { get; set; }

    /// <summary>
    /// Processes the cmdlet invocation.
    /// </summary>
    protected override void ProcessRecord() {
        if (Attachment?.Length > 0 && Path is not null) {
            MicrosoftGraphUtils.SaveAttachments(Attachment, Path);
        }
    }
}