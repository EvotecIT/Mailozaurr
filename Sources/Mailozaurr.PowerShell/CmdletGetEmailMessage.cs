using MimeKit;
using Mailozaurr;
using System;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Fetches email messages using IMAP or POP3 connections with optional filters.</para>
/// <para type="description">The <c>Get-EmailMessage</c> cmdlet retrieves messages from an IMAP or POP3 server using existing connection objects. It supports filtering by folder (IMAP), sender, recipients, subject text, priority and date range. Messages can optionally be deleted after retrieval.</para>
/// <example>
///   <summary>Get messages from an IMAP folder</summary>
///   <code>$imap = Connect-IMAP ...; Get-EmailMessage -ImapClient $imap -Folder "Inbox/Sub" -Subject "Report" -Since (Get-Date).AddDays(-7)</code>
/// </example>
/// <example>
///   <summary>Get POP3 messages received today</summary>
///   <code>$pop = Connect-POP3 ...; Get-EmailMessage -PopClient $pop -Since (Get-Date).Date</code>
/// </example>
/// <remarks>
/// Use this cmdlet to retrieve and filter messages from IMAP or POP3 servers for automation or archiving tasks.
/// </remarks>
/// <seealso cref="CmdletConnectIMAP"/>
/// <seealso cref="CmdletConnectPOP3"/>
/// </summary>
[Cmdlet(VerbsCommon.Get, "EmailMessage")]
[OutputType(typeof(MimeMessage))]
public sealed class CmdletGetEmailMessage : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">Active IMAP connection object returned by <c>Connect-IMAP</c>.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "IMAP", ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? ImapClient { get; set; }

    /// <summary>
    /// <para type="description">Active POP3 connection object returned by <c>Connect-POP3</c>.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "POP", ValueFromPipeline = true)]
    [ValidateNotNull]
    public PopConnectionInfo? PopClient { get; set; }



    /// <summary>
    /// <para type="description">IMAP folder to search. Defaults to Inbox if not specified.</para>
    /// </summary>
    [Parameter(ParameterSetName = "IMAP")]
    public string? Folder { get; set; }

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

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        return ParameterSetName switch {
            "IMAP" => ProcessImapAsync(),
            "POP"  => ProcessPopAsync(),
            _      => Task.CompletedTask,
        };
    }

    private Task ProcessImapAsync() {
        if (ImapClient == null || ImapClient.Data == null) {
            WriteWarning("Get-EmailMessage - Is IMAP connected?");
            return Task.CompletedTask;
        }

        var messages = MessageFetcher.Fetch(
            ImapClient.Data,
            Folder,
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
        return Task.CompletedTask;
    }

    private Task ProcessPopAsync() {
        if (PopClient == null || PopClient.Data == null) {
            WriteWarning("Get-EmailMessage - Is POP3 connected?");
            return Task.CompletedTask;
        }

        var messages = MessageFetcher.Fetch(
            PopClient.Data,
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
        return Task.CompletedTask;
    }


}
