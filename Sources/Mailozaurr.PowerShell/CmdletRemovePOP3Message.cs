using Mailozaurr;
using System.Collections.Generic;
using System.Management.Automation;
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
            var dryRun = !ShouldProcess(string.Join(",", Index ?? System.Array.Empty<int>()), "Deleting POP3 message");
            var valid = new List<int>();
            foreach (var i in Index!) {
                if (i < conn.Data.Count) {
                    valid.Add(i);
                } else {
                    WriteWarning($"Remove-POP3Message - Index is out of range. Use index less than {conn.Data.Count}.");
                }
            }
            await MessageRemover.DeleteAsync(conn.Data, valid, dryRun, CancelToken);
        } else {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Remove-POP3Message - POP3 client not provided or not connected."),
                "ClientNotConnected",
                ErrorCategory.InvalidOperation,
                null));
        }
    }
}