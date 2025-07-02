using System.Management.Automation;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Saves attachments from an IMAP message to disk.</para>
/// <para type="description">The <c>Save-IMAPMessageAttachment</c> cmdlet saves all attachments from an IMAP message identified by its UID to the specified directory.</para>
/// </summary>
[Cmdlet(VerbsData.Save, "IMAPMessageAttachment")]
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
            MimeKitUtils.SaveAttachments(message.Attachments, Path);
        } else {
            WriteWarning("Save-IMAPMessageAttachment - Is IMAP connected?");
        }
        return Task.CompletedTask;
    }
}
