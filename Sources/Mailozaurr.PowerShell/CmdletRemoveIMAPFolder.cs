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

    /// <summary>Remove subfolders as well.</summary>
    [Parameter]
    public SwitchParameter Recursive { get; set; }

    /// <summary>
    /// Removes the specified folder from the connected IMAP server.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            var dryRun = !ShouldProcess(Folder!, "Removing IMAP folder");
            await FolderOperations.RemoveFolderAsync(conn.Data, Folder!, Recursive.IsPresent, dryRun, CancelToken);
        } else {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Remove-IMAPFolder - IMAP client not provided or not connected."),
                "ClientNotConnected",
                ErrorCategory.InvalidOperation,
                null));
        }
    }
}
