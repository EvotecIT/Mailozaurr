using System.Management.Automation;
using MailKit;
using MailKit.Net.Imap;
using Mailozaurr;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Removes messages from an IMAP folder by UID.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "IMAPMessage", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class CmdletRemoveIMAPMessage : AsyncPSCmdlet {
    /// <summary>Active IMAP connection info.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    /// <summary>UIDs of messages to delete.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public uint[]? Uid { get; set; }

    /// <summary>Optional folder containing the messages.</summary>
    [Parameter(Position = 2)]
    public string? Folder { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            var folder = Folder ?? conn.Folder?.FullName;
            if (!ShouldProcess(string.Join(",", Uid ?? System.Array.Empty<uint>()), "Deleting IMAP message")) {
                return;
            }
            foreach (var u in Uid!) {
                await MessageRemover.DeleteAsync(conn.Data, new UniqueId(u), folder, CancelToken);
            }
        } else {
            WriteWarning("Remove-IMAPMessage - Is IMAP connected?");
        }
    }
}
