using MailKit;
using MailKit.Search;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Fetches email messages using IMAP or POP3 connections with optional filters.</para>
/// <para type="description">The <c>Get-EmailMessage</c> cmdlet retrieves messages from an IMAP or POP3 server using existing connection objects. It supports filtering by folder (IMAP), subject text, and date range.</para>
/// <example>
///   <summary>Get messages from an IMAP folder</summary>
///   <code>$imap = Connect-IMAP ...; Get-EmailMessage -ImapClient $imap -Folder "Inbox/Sub" -Subject "Report" -Since (Get-Date).AddDays(-7)</code>
/// </example>
/// <example>
///   <summary>Get POP3 messages received today</summary>
///   <code>$pop = Connect-POP ...; Get-EmailMessage -PopClient $pop -Since (Get-Date).Date</code>
/// </example>
/// <remarks>
/// Use this cmdlet to retrieve and filter messages from IMAP or POP3 servers for automation or archiving tasks.
/// </remarks>
/// <seealso cref="CmdletConnectIMAP"/>
/// <seealso cref="CmdletConnectPOP"/>
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
    /// <para type="description">Active POP3 connection object returned by <c>Connect-POP</c>.</para>
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
            "POP" => ProcessPopAsync(),
            _ => Task.CompletedTask
        };
    }

    private Task ProcessImapAsync() {
        if (ImapClient == null || ImapClient.Data == null) {
            WriteWarning("Get-EmailMessage - Is IMAP connected?");
            return Task.CompletedTask;
        }

        var client = ImapClient.Data;
        IMailFolder folder = client.Inbox;
        if (!string.IsNullOrEmpty(Folder)) {
            try {
                folder = client.GetFolder(Folder);
            } catch {
                try {
                    folder = client.GetFolder(client.PersonalNamespaces[0]).GetSubfolder(Folder);
                } catch {
                    WriteWarning($"Get-EmailMessage - Folder '{Folder}' not found. Using Inbox.");
                    folder = client.Inbox;
                }
            }
        }

        folder.Open(FolderAccess.ReadOnly);

        SearchQuery query = SearchQuery.All;
        if (!string.IsNullOrEmpty(Subject)) {
            query = query.And(SearchQuery.SubjectContains(Subject));
        }
        if (Since.HasValue) {
            query = query.And(SearchQuery.DeliveredAfter(Since.Value));
        }
        if (Before.HasValue) {
            query = query.And(SearchQuery.DeliveredBefore(Before.Value));
        }

        var uids = folder.Search(query);
        List<MimeMessage> messages = new();
        foreach (var uid in uids) {
            messages.Add(folder.GetMessage(uid));
        }

        WriteObject(messages, true);
        return Task.CompletedTask;
    }

    private Task ProcessPopAsync() {
        if (PopClient == null || PopClient.Data == null) {
            WriteWarning("Get-EmailMessage - Is POP3 connected?");
            return Task.CompletedTask;
        }

        var client = PopClient.Data;
        List<MimeMessage> messages = new();
        for (int i = 0; i < client.Count; i++) {
            var message = client.GetMessage(i);
            if (!string.IsNullOrEmpty(Subject)) {
                if (message.Subject == null || message.Subject.IndexOf(Subject, StringComparison.OrdinalIgnoreCase) < 0) {
                    continue;
                }
            }
            var msgDate = message.Date.DateTime;
            if (Since.HasValue && msgDate < Since.Value) {
                continue;
            }
            if (Before.HasValue && msgDate > Before.Value) {
                continue;
            }
            messages.Add(message);
        }

        WriteObject(messages, true);
        return Task.CompletedTask;
    }
}
