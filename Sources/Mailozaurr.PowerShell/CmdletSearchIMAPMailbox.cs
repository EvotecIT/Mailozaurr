using System;
using System.Management.Automation;
using MailKit.Search;
using Mailozaurr;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Searches an IMAP mailbox and returns matching messages.
/// </summary>
[Cmdlet(VerbsCommon.Search, "IMAPMailbox")]
[OutputType(typeof(ImapEmailMessage))]
public sealed class CmdletSearchIMAPMailbox : AsyncPSCmdlet {
    [Parameter(ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    [Parameter]
    public string? Folder { get; set; }

    [Parameter]
    public SearchQuery[]? SearchQuery { get; set; }

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
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            var messages = await MailboxSearcher.SearchImapAsync(
                conn.Data,
                Folder,
                Subject,
                FromContains,
                ToContains,
                Priority,
                Since,
                Before,
                HasAttachment.IsPresent,
                SearchQuery,
                Count,
                CancelToken);
            foreach (var msg in messages) {
                WriteObject(msg);
            }
        } else {
            WriteWarning("Search-IMAPMailbox - Is IMAP connected?");
        }
    }
}
