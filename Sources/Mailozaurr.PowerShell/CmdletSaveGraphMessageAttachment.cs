using Mailozaurr;
using System.Management.Automation;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Saves attachments from Microsoft Graph message objects.
/// </summary>
[Cmdlet(VerbsData.Save, "GraphMessageAttachment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
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

    /// <summary>Controls how existing destination files are handled.</summary>
    [Parameter]
    public AttachmentFileConflictPolicy ConflictPolicy { get; set; } = AttachmentFileConflictPolicy.Fail;

    /// <summary>Replaces existing regular files. Equivalent to ConflictPolicy Replace.</summary>
    [Parameter]
    [Alias("Overwrite")]
    public SwitchParameter Force { get; set; }

    /// <summary>Writes one save result for each attachment.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <summary>
    /// Processes the cmdlet invocation.
    /// </summary>
    protected override void ProcessRecord() {
        if (Attachment?.Length > 0 && Path is not null) {
            AttachmentFileConflictPolicy policy = AttachmentCmdletConflictPolicy.Resolve(
                this,
                Force,
                ConflictPolicy);
            if (!ShouldProcess(Path, "Save Microsoft Graph message attachments")) return;
            IReadOnlyList<AttachmentFileSaveResult> results = MicrosoftGraphUtils.SaveAttachments(
                Attachment,
                Path,
                policy);
            if (PassThru.IsPresent) WriteObject(results, enumerateCollection: true);
        }
    }
}
