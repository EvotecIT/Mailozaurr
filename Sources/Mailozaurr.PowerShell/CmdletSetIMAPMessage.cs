using System.Management.Automation;
using MailKit;
using MailKit.Net.Imap;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Updates flags on an IMAP message.
/// </summary>
[Cmdlet(VerbsCommon.Set, "IMAPMessage")]
public sealed class CmdletSetIMAPMessage : AsyncPSCmdlet {
    /// <summary>Active IMAP connection.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    /// <summary>UID of the message.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public uint Uid { get; set; }

    /// <summary>Optional folder containing the message.</summary>
    [Parameter(Position = 2)]
    public string? Folder { get; set; }

    /// <summary>Marks the message as read.</summary>
    [Parameter]
    public SwitchParameter Read { get; set; }

    /// <summary>Marks the message as unread.</summary>
    [Parameter]
    public SwitchParameter Unread { get; set; }

    /// <summary>
    /// Processes the cmdlet, updating message flags as requested.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        if (Read.IsPresent && Unread.IsPresent) {
            ThrowTerminatingError(new ErrorRecord(new PSArgumentException("Specify only -Read or -Unread."), "InvalidFlags", ErrorCategory.InvalidArgument, null));
            return;
        }
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            var uid = new UniqueId(Uid);
            if (Read) {
                await MessageFlagSetter.SetFlagsAsync(conn.Data, uid, MessageFlags.Seen, true, Folder ?? conn.Folder?.FullName, CancelToken);
            } else if (Unread) {
                await MessageFlagSetter.SetFlagsAsync(conn.Data, uid, MessageFlags.Seen, false, Folder ?? conn.Folder?.FullName, CancelToken);
            }
        } else {
            WriteWarning("Set-IMAPMessage - Is IMAP connected?");
        }
    }
}
