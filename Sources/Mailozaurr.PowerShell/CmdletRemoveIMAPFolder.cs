using System.Management.Automation;
using MailKit.Net.Imap;
using Mailozaurr;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Removes an IMAP folder.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "IMAPFolder", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class CmdletRemoveIMAPFolder : AsyncPSCmdlet {
    /// <summary>Active IMAP connection info.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    /// <summary>Name of the folder to remove.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string? Folder { get; set; }

    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            if (!ShouldProcess(Folder!, "Removing IMAP folder")) {
                return;
            }
            await FolderOperations.RemoveFolderAsync(conn.Data, Folder!, CancelToken);
        } else {
            WriteWarning("Remove-IMAPFolder - Is IMAP connected?");
        }
    }
}
