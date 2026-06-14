using MailKit.Net.Imap;
using Mailozaurr;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Renames an IMAP folder.
/// </summary>
[Cmdlet("Rename", "IMAPFolder", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public sealed class CmdletRenameIMAPFolder : AsyncPSCmdlet {
    /// <summary>Active IMAP connection info.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    /// <summary>Folder name to rename.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string? Folder { get; set; }

    /// <summary>New name for the folder.</summary>
    [Parameter(Mandatory = true, Position = 2)]
    [ValidateNotNullOrEmpty]
    public string? NewName { get; set; }

    /// <summary>
    /// Renames an IMAP folder on the connected server.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            var dryRun = !ShouldProcess(Folder!, "Renaming IMAP folder");
            await FolderOperations.RenameFolderAsync(conn.Data, Folder!, NewName!, dryRun, CancelToken);
        } else {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Rename-IMAPFolder - IMAP client not provided or not connected."),
                "ClientNotConnected",
                ErrorCategory.InvalidOperation,
                null));
        }
    }
}