using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;
using MailKit;
using MailKit.Search;
using MimeKit;
using Mailozaurr;
using Mailozaurr.PowerShell;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Retrieves messages from an IMAP folder using optional filters.</para>
/// <para type="description">The <c>Get-IMAPMessage</c> cmdlet fetches messages from the current IMAP folder associated with the provided <see cref="ImapConnectionInfo"/> object. You can filter by subject, sender, recipients, priority, date range and attachment presence. Messages can also be deleted after retrieval.</para>
/// <example>
///   <summary>Get all messages from the current folder</summary>
///   <code>$client = Connect-IMAP ...; Get-IMAPMessage -Client $client -All</code>
/// </example>
/// <example>
///   <summary>Get messages with a subject filter</summary>
///   <code>$client = Connect-IMAP ...; Get-IMAPMessage -Client $client -Subject 'Report'</code>
/// </example>
/// <remarks>
/// Use this cmdlet to retrieve and optionally delete messages from an IMAP server.
/// </remarks>
/// <seealso cref="CmdletConnectIMAP"/>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsCommon.Get, "IMAPMessage")]
public sealed class CmdletGetIMAPMessage : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">The <see cref="ImapConnectionInfo"/> object representing the active IMAP connection. This is the object returned by <c>Connect-IMAP</c>.</para>
    /// </summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    /// <summary>
    /// <para type="description">Specifies the folder access mode (ReadOnly or ReadWrite). Default is ReadOnly.</para>
    /// </summary>
    [Parameter(Position = 1)]
    public FolderAccess FolderAccess { get; set; } = FolderAccess.ReadOnly;

    /// <summary>
    /// <para type="description">Specifies the starting sequence number of messages to retrieve.</para>
    /// </summary>
    [Parameter(Position = 2)]
    public int? SequenceStart { get; set; }

    /// <summary>
    /// <para type="description">Specifies the ending sequence number of messages to retrieve. If not provided, only <c>SequenceStart</c> is fetched.</para>
    /// </summary>
    [Parameter(Position = 3)]
    public int? SequenceEnd { get; set; }

    /// <summary>
    /// <para type="description">Specifies the starting UID of messages to retrieve.</para>
    /// </summary>
    [Parameter(Position = 2)]
    public uint? UidStart { get; set; }

    /// <summary>
    /// <para type="description">Specifies the ending UID of messages to retrieve. If not provided, only <c>UidStart</c> is fetched.</para>
    /// </summary>
    [Parameter(Position = 3)]
    public uint? UidEnd { get; set; }

    /// <summary>
    /// <para type="description">Search query used to match messages to retrieve.</para>
    /// </summary>
    [Parameter]
    public SearchQuery? SearchQuery { get; set; }

    /// <summary>
    /// <para type="description">Only return messages containing this text in the subject.</para>
    /// </summary>
    [Parameter]
    public string? Subject { get; set; }

    /// <summary>
    /// <para type="description">Only return messages sent from addresses matching this value.</para>
    /// </summary>
    [Parameter]
    public string? FromContains { get; set; }

    /// <summary>
    /// <para type="description">Only return messages sent to addresses matching this value.</para>
    /// </summary>
    [Parameter]
    public string? ToContains { get; set; }

    /// <summary>
    /// <para type="description">Only return messages with the specified priority.</para>
    /// </summary>
    [Parameter]
    public MessagePriority? Priority { get; set; }

    /// <summary>
    /// <para type="description">Only return messages that contain attachments.</para>
    /// </summary>
    [Parameter]
    public SwitchParameter HasAttachment { get; set; }

    /// <summary>
    /// <para type="description">If set, retrieves all messages ignoring other filters.</para>
    /// </summary>
    [Parameter]
    public SwitchParameter All { get; set; }

    /// <summary>
    /// <para type="description">If set, deletes the retrieved messages.</para>
    /// </summary>
    [Parameter]
    public SwitchParameter Delete { get; set; }

    /// <summary>
    /// <para type="description">Return messages delivered on or after this date.</para>
    /// </summary>
    [Parameter]
    public DateTime? Since { get; set; }

    /// <summary>
    /// <para type="description">Return messages delivered on or before this date.</para>
    /// </summary>
    [Parameter]
    public DateTime? Before { get; set; }

    /// <summary>
    /// Opens the inbox folder and retrieves messages if message parameters are specified.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            var folderName = conn.Folder?.FullName;
            var folder = conn.Data.GetOrOpenFolder(folderName, Delete.IsPresent ? FolderAccess.ReadWrite : FolderAccess.ReadOnly);
            conn.Folder = folder;

            var messages = MessageFetcher.Fetch(
                conn.Data,
                folder.FullName,
                Subject,
                FromContains,
                ToContains,
                Priority,
                Since,
                Before,
                All.IsPresent,
                Delete.IsPresent,
                HasAttachment.IsPresent);

            WriteObject(messages, true);
        } else {
            WriteWarning("Get-IMAPMessage - Is IMAP connected?");
        }

        return Task.CompletedTask;
    }
}