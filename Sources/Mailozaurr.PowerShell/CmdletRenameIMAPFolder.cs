using System.Management.Automation;
using MailKit.Net.Imap;
using Mailozaurr;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Renames an IMAP folder.
/// </summary>
[Cmdlet("Rename", "IMAPFolder", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public sealed class CmdletRenameIMAPFolder : AsyncPSCmdlet {
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string? Folder { get; set; }

    [Parameter(Mandatory = true, Position = 2)]
    [ValidateNotNullOrEmpty]
    public string? NewName { get; set; }

    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            if (!ShouldProcess(Folder!, "Renaming IMAP folder")) {
                return;
            }
            await FolderOperations.RenameFolderAsync(conn.Data, Folder!, NewName!, CancelToken);
        } else {
            WriteWarning("Rename-IMAPFolder - Is IMAP connected?");
        }
    }
}
