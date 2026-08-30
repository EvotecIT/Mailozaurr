using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Saves attachments from a Gmail message to disk.
/// </summary>
[Cmdlet(VerbsData.Save, "GmailMessageAttachment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public sealed class CmdletSaveGmailMessageAttachment : AsyncPSCmdlet {
    /// <summary>
    /// Gmail account containing the message.
    /// </summary>
    [Parameter(Mandatory = true)]
    public string? GmailAccount { get; set; }

    /// <summary>
    /// OAuth credential used for authentication.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNull]
    public PSCredential? Credential { get; set; }

    /// <summary>
    /// Identifier of the Gmail message.
    /// </summary>
    [Parameter(Mandatory = true)]
    public string? Id { get; set; }

    /// <summary>
    /// Destination path for saving attachments.
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
    /// Downloads attachments from the specified Gmail message.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task ProcessRecordAsync() {
        var net = Credential!.GetNetworkCredential();
        var oauth = new OAuthCredential {
            UserName = net.UserName,
            AccessToken = net.Password,
            ExpiresOn = System.DateTimeOffset.MaxValue
        };
        using var client = new GmailApiClient(oauth);
        var list = await client.ListAttachmentsAsync(GmailAccount!, Id!, CancelToken);
        AttachmentFileConflictPolicy policy = AttachmentCmdletConflictPolicy.Resolve(
            this,
            Force,
            ConflictPolicy);
        foreach (var att in list) {
            string fileName = att.FileName ?? att.Id!;
            string destinationPath = AttachmentFileStore.ResolvePathInDirectory(
                Path!,
                fileName,
                att.Id);
            if (!ShouldProcess(destinationPath, "Save Gmail message attachment")) continue;
            AttachmentFileSaveResult? skipped = AttachmentFileStore.TryPreflightSkip(
                destinationPath,
                policy);
            if (skipped != null) {
                if (PassThru.IsPresent) WriteObject(skipped);
                continue;
            }
            var bytes = await client.DownloadAttachmentAsync(GmailAccount!, Id!, att.Id!, CancelToken);
            AttachmentFileSaveResult result = AttachmentFileStore.SaveToFile(
                destinationPath,
                stream => stream.Write(bytes, 0, bytes.Length),
                policy);
            if (PassThru.IsPresent) WriteObject(result);
        }
    }
}
