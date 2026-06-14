using MailKit.Search;
using Mailozaurr;
using System;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Searches an IMAP mailbox and returns matching messages.
/// </summary>
[Cmdlet(VerbsCommon.Search, "IMAPMailbox")]
[OutputType(typeof(ImapEmailMessage))]
public sealed class CmdletSearchIMAPMailbox : AsyncPSCmdlet {
    /// <summary>
    /// Active IMAP connection.
    /// </summary>
    [Parameter(ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    /// <summary>
    /// Folder to search. Defaults to inbox.
    /// </summary>
    [Parameter]
    public string? Folder { get; set; }

    /// <summary>
    /// Additional MailKit search queries.
    /// </summary>
    [Parameter]
    public SearchQuery[]? SearchQuery { get; set; }

    /// <summary>
    /// Query language string to filter messages.
    /// </summary>
    [Parameter]
    public string? Query { get; set; }

    /// <summary>
    /// Filters messages by subject.
    /// </summary>
    [Parameter]
    public string? Subject { get; set; }

    /// <summary>
    /// Filters messages by sender content.
    /// </summary>
    [Parameter]
    public string? FromContains { get; set; }

    /// <summary>
    /// Filters messages by recipient content.
    /// </summary>
    [Parameter]
    public string? ToContains { get; set; }

    /// <summary>
    /// Filters messages where the body contains this text.
    /// </summary>
    [Parameter]
    public string? BodyContains { get; set; }

    /// <summary>
    /// Filters messages by priority.
    /// </summary>
    [Parameter]
    public MessagePriority? Priority { get; set; }

    /// <summary>
    /// Only messages received since this date are returned.
    /// </summary>
    [Parameter]
    public DateTime? Since { get; set; }

    /// <summary>
    /// Only messages received before this date are returned.
    /// </summary>
    [Parameter]
    public DateTime? Before { get; set; }

    /// <summary>
    /// Filters messages that contain attachments.
    /// </summary>
    [Parameter]
    public SwitchParameter HasAttachment { get; set; }

    /// <summary>
    /// Maximum number of messages to return.
    /// </summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int Count { get; set; }

    /// <summary>
    /// Searches the IMAP mailbox using the specified criteria.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            var messages = await MailboxSearcher.SearchImapAsync(
                conn.Data,
                Folder,
                Subject,
                FromContains,
                ToContains,
                BodyContains,
                Priority,
                Since,
                Before,
                HasAttachment.IsPresent,
                SearchQuery,
                Count,
                CancelToken,
                Query);
            foreach (var msg in messages) {
                WriteObject(msg);
            }
        } else {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Search-IMAPMailbox - IMAP client not provided or not connected."),
                "ClientNotConnected",
                ErrorCategory.InvalidOperation,
                null));
        }
    }
}