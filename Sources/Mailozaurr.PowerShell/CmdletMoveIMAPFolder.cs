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
    private const string ParentParameterSet = "Parent";
    private const string RootParameterSet = "Root";
    /// <summary>Active IMAP connection info.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    /// <summary>Name of the folder to move.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string? Folder { get; set; }

    /// <summary>Destination folder name.</summary>
    [Parameter(Mandatory = true, Position = 2, ParameterSetName = ParentParameterSet)]
    [ValidateNotNullOrEmpty]
    public string? DestinationFolder { get; set; }

    /// <summary>Move folder to the root.</summary>
    [Parameter(ParameterSetName = RootParameterSet)]
    public SwitchParameter Root { get; set; }

    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            if (!ShouldProcess(Folder!, "Moving IMAP folder")) {
                return;
            }
            var dest = ParameterSetName == RootParameterSet ? null : DestinationFolder;
            await FolderOperations.MoveFolderAsync(conn.Data, Folder!, dest, CancelToken);
        } else {
            WriteWarning("Move-IMAPFolder - Is IMAP connected?");
        }
    }
}
