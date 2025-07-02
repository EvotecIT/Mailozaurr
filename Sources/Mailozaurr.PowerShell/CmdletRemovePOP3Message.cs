using System.Management.Automation;
using Mailozaurr;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Removes messages from a POP3 mailbox by index.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "POP3Message", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class CmdletRemovePOP3Message : AsyncPSCmdlet {
    /// <summary>Active POP3 connection info.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public PopConnectionInfo? Client { get; set; }

    /// <summary>Indexes of messages to delete.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public int[]? Index { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.Pop3Session;
        if (conn != null && conn.Data != null) {
            if (!ShouldProcess(string.Join(",", Index ?? System.Array.Empty<int>()), "Deleting POP3 message")) {
                return;
            }
            foreach (var i in Index!) {
                if (i < conn.Data.Count) {
                    await MessageRemover.DeleteAsync(conn.Data, i, CancelToken);
                } else {
                    WriteWarning($"Remove-POP3Message - Index is out of range. Use index less than {conn.Data.Count}.");
                }
            }
        } else {
            WriteWarning("Remove-POP3Message - Is POP3 connected?");
        }
    }
}
