using System.Management.Automation;
using MailKit.Net.Pop3;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Updates local flags on a POP3 message.
/// </summary>
[Cmdlet(VerbsCommon.Set, "POP3Message")]
public sealed class CmdletSetPOP3Message : AsyncPSCmdlet {
    /// <summary>Active POP3 connection.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public PopConnectionInfo? Client { get; set; }

    /// <summary>Message index.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public int Index { get; set; }

    /// <summary>Marks the message as read.</summary>
    [Parameter]
    public SwitchParameter Read { get; set; }

    /// <summary>Marks the message as unread.</summary>
    [Parameter]
    public SwitchParameter Unread { get; set; }

    protected override Task ProcessRecordAsync() {
        if (Read.IsPresent && Unread.IsPresent) {
            ThrowTerminatingError(new ErrorRecord(new PSArgumentException("Specify only -Read or -Unread."), "InvalidFlags", ErrorCategory.InvalidArgument, null));
            return Task.CompletedTask;
        }
        var conn = Client ?? DefaultSessions.Pop3Session;
        if (conn != null && conn.Data != null) {
            if (Read) {
                return MessageFlagSetter.SetReadAsync(conn.Data, Index, true, CancelToken);
            }
            if (Unread) {
                return MessageFlagSetter.SetReadAsync(conn.Data, Index, false, CancelToken);
            }
        } else {
            WriteWarning("Set-POP3Message - Is POP3 connected?");
        }
        return Task.CompletedTask;
    }
}
