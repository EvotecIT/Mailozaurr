using System.Management.Automation;
using System.Threading.Tasks;
using Mailozaurr.PowerShell;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Retrieves messages from a POP3 mailbox with optional filters.</para>
/// <para type="description">The <c>Get-POP3Message</c> cmdlet fetches messages from a POP3 mailbox using the provided <see cref="PopConnectionInfo"/> object. It supports filtering by subject, sender, recipients, priority, date range and attachment presence. Messages can be removed after retrieval using <c>-Delete</c>.</para>
/// <example>
///   <summary>Get messages from a POP3 mailbox with a subject filter</summary>
///   <code>$client = Connect-POP3 ...; Get-POP3Message -Client $client -Subject 'Report'</code>
/// </example>
/// <example>
///   <summary>Get all messages from a POP3 mailbox and delete them</summary>
///   <code>$client = Connect-POP3 ...; Get-POP3Message -Client $client -All -Delete</code>
/// </example>
/// <remarks>
/// Use this cmdlet to enumerate or download messages from a POP3 mailbox for backup, migration, or processing.
/// </remarks>
/// <seealso cref="CmdletConnectPOP3"/>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsCommon.Get, "POP3Message")]
public sealed class CmdletGetPOP3Message : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">The <see cref="PopConnectionInfo"/> object representing the active POP3 connection. This is the object returned by <c>Connect-POP3</c>.</para>
    /// </summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public PopConnectionInfo? Client { get; set; }

    /// <summary>
    /// <para type="description">Specifies the index of the first message to retrieve. Default is 0 (the first message).</para>
    /// </summary>
    [Parameter(Position = 1)]
    public int Index { get; set; }

    /// <summary>
    /// <para type="description">Specifies the number of messages to retrieve starting from <c>Index</c>. Default is 1.</para>
    /// </summary>
    [Parameter(Position = 2)]
    public int Count { get; set; } = 1;

    /// <summary>
    /// <para type="description">If set, retrieves all messages from the POP3 mailbox.</para>
    /// </summary>
    [Parameter()]
    public SwitchParameter All { get; set; }

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
    /// <para type="description">Only return messages that contain attachments.</para>
    /// </summary>
    [Parameter]
    public SwitchParameter HasAttachment { get; set; }

    /// <summary>
    /// <para type="description">If set, deletes the retrieved messages.</para>
    /// </summary>
    [Parameter]
    public SwitchParameter Delete { get; set; }

    /// <summary>
    /// Retrieves one or more messages from the POP3 mailbox.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.Pop3Session;
        if (conn != null && conn.Data != null) {
            if (!conn.IsConnected) {
                WriteWarning("Get-POP3Message - Client is not connected.");
                return;
            }

            await foreach (var message in MessageFetcher.Fetch(
                conn.Data,
                Subject,
                FromContains,
                ToContains,
                Priority,
                Since,
                Before,
                All.IsPresent,
                Delete.IsPresent,
                HasAttachment.IsPresent,
                CancelToken)) {
                WriteObject(message);
            }
        } else {
            WriteWarning("Get-POP3Message - Is POP3 connected?");
        }
    }
}