using System.Management.Automation;
using MailKit.Net.Imap;
using Mailozaurr;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Moves an IMAP folder to a new location.
/// </summary>
[Cmdlet(VerbsCommon.Move, "IMAPFolder", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public sealed class CmdletMoveIMAPFolder : AsyncPSCmdlet {
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string? Folder { get; set; }

    [Parameter(Mandatory = true, Position = 2)]
    [ValidateNotNullOrEmpty]
    public string? DestinationFolder { get; set; }

    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            if (!ShouldProcess(Folder!, "Moving IMAP folder")) {
                return;
            }
            await FolderOperations.MoveFolderAsync(conn.Data, Folder!, DestinationFolder!, CancelToken);
        } else {
            WriteWarning("Move-IMAPFolder - Is IMAP connected?");
        }
    }
}
