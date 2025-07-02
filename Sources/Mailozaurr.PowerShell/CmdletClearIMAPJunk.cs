using System.Management.Automation;
using MailKit.Net.Imap;
using System.Threading.Tasks;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Clears messages from an IMAP junk folder.
/// </summary>
[Cmdlet(VerbsCommon.Clear, "IMAPJunk", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class CmdletClearIMAPJunk : AsyncPSCmdlet {
    /// <summary>Active IMAP connection info.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    /// <summary>Junk folder name.</summary>
    [Parameter(Position = 1)]
    public string? Folder { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            var folder = Folder ?? "Junk";
            if (!ShouldProcess(folder, "Clearing IMAP junk")) return;
            await JunkCleaner.ClearImapJunkAsync(conn.Data, folder, CancelToken);
        } else {
            WriteWarning("Clear-IMAPJunk - Is IMAP connected?");
        }
    }
}
