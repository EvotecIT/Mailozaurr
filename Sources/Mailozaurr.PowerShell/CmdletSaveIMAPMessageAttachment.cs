using MailKit;
using MailKit.Net.Imap;
using Mailozaurr;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Saves attachments from an IMAP message to disk.</para>
/// <para type="description">The <c>Save-IMAPMessageAttachment</c> cmdlet saves all attachments from an IMAP message identified by its UID to the specified directory.</para>
/// </summary>
[Cmdlet(VerbsData.Save, "IMAPMessageAttachment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public sealed class CmdletSaveIMAPMessageAttachment : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">The <see cref="ImapConnectionInfo"/> object representing the active IMAP connection.</para>
    /// </summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    /// <summary>
    /// <para type="description">Specifies the UID of the message to process.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 1)]
    public uint Uid { get; set; }

    /// <summary>
    /// <para type="description">Optional folder from which to retrieve the message. Defaults to Inbox.</para>
    /// </summary>
    [Parameter(Position = 2)]
    public string? Folder { get; set; }

    /// <summary>
    /// <para type="description">Specifies the directory path where attachments will be saved.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 3)]
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
    /// Saves attachments from the specified IMAP message to disk.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            var uid = new UniqueId(Uid);
            var mailFolder = conn.Data.GetCachedFolder(Folder ?? conn.Folder?.FullName, FolderAccess.ReadOnly);
            conn.Folders[mailFolder.FullName] = (ImapFolder)mailFolder;
            var message = mailFolder.GetMessage(uid);
            if (Path is not null) {
                AttachmentFileConflictPolicy policy = AttachmentCmdletConflictPolicy.Resolve(
                    this,
                    Force,
                    ConflictPolicy);
                if (ShouldProcess(Path, "Save IMAP message attachments")) {
                    IReadOnlyList<AttachmentFileSaveResult> results = MimeKitUtils.SaveAttachments(
                        message.Attachments,
                        Path,
                        policy);
                    if (PassThru.IsPresent) WriteObject(results, enumerateCollection: true);
                }
            }
        } else {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Save-IMAPMessageAttachment - IMAP client not provided or not connected."),
                "ClientNotConnected",
                ErrorCategory.InvalidOperation,
                null));
        }
        return Task.CompletedTask;
    }
}
