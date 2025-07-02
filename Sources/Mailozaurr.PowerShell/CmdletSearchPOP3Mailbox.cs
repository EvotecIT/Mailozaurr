using System;
using System.Management.Automation;
using System.Threading.Tasks;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Searches a POP3 mailbox and returns matching messages.
/// </summary>
[Cmdlet(VerbsCommon.Search, "POP3Mailbox")]
[OutputType(typeof(Pop3EmailMessage))]
public sealed class CmdletSearchPOP3Mailbox : AsyncPSCmdlet {
    [Parameter(ValueFromPipeline = true)]
    [ValidateNotNull]
    public PopConnectionInfo? Client { get; set; }

    [Parameter]
    public string? Subject { get; set; }

    [Parameter]
    public string? FromContains { get; set; }

    [Parameter]
    public string? ToContains { get; set; }

    [Parameter]
    public MessagePriority? Priority { get; set; }

    [Parameter]
    public DateTime? Since { get; set; }

    [Parameter]
    public DateTime? Before { get; set; }

    [Parameter]
    public SwitchParameter HasAttachment { get; set; }

    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int Count { get; set; }

    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.Pop3Session;
        if (conn != null && conn.Data != null) {
            if (!conn.IsConnected) {
                WriteWarning("Search-POP3Mailbox - Client is not connected.");
                return;
            }
            var messages = await MailboxSearcher.SearchPop3Async(
                conn.Data,
                Subject,
                FromContains,
                ToContains,
                Priority,
                Since,
                Before,
                HasAttachment.IsPresent,
                Count,
                CancelToken);
            foreach (var msg in messages) {
                WriteObject(msg);
            }
        } else {
            WriteWarning("Search-POP3Mailbox - Is POP3 connected?");
        }
    }
}
