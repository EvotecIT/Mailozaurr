using System.Management.Automation;
using MailKit;
using MailKit.Net.Imap;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Sets the working IMAP folder for subsequent operations.
/// </summary>
[Cmdlet(VerbsCommon.Set, "IMAPFolder")]
public sealed class CmdletSetIMAPFolder : AsyncPSCmdlet {
    /// <summary>Active IMAP connection.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    /// <summary>Folder path to open.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string? Path { get; set; }

    /// <summary>Folder access mode.</summary>
    [Parameter]
    public FolderAccess FolderAccess { get; set; } = FolderAccess.ReadOnly;

    protected override Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            var folder = (ImapFolder)conn.Data.GetCachedFolder(Path!, FolderAccess);
            conn.Messages = folder;
            conn.Count = folder.Count;
            conn.Recent = folder.Recent;
            conn.Folder = folder;
            conn.Folders[folder.FullName] = folder;
            DefaultSessions.ImapSession = conn;
            WriteObject(conn);
        } else {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Set-IMAPFolder - IMAP client not provided or not connected."),
                "ClientNotConnected",
                ErrorCategory.InvalidOperation,
                null));
        }
        return Task.CompletedTask;
    }
}
