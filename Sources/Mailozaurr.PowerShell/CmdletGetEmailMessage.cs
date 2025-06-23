using MimeKit;
using Mailozaurr;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
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

    [Parameter(Mandatory = true, ParameterSetName = "GraphCredential")]
    [Parameter(Mandatory = true, ParameterSetName = "GraphConnection")]
    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "GraphCredential")]
    [ValidateNotNull]
    public PSCredential? Credential { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "GraphConnection")]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    [Parameter(ParameterSetName = "GraphCredential")]
    [Parameter(ParameterSetName = "GraphConnection")]
    [Parameter(ParameterSetName = "MgGraphRequest")]
    public string[]? Property { get; set; }

    [Parameter(ParameterSetName = "GraphCredential")]
    [Parameter(ParameterSetName = "GraphConnection")]
    [Parameter(ParameterSetName = "MgGraphRequest")]
    public string? Filter { get; set; }

    [Parameter(ParameterSetName = "GraphCredential")]
    [Parameter(ParameterSetName = "GraphConnection")]
    [Parameter(ParameterSetName = "MgGraphRequest")]
    public int? Limit { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "GraphCredential")]
    [Parameter(Mandatory = true, ParameterSetName = "GraphConnection")]
    public SwitchParameter Graph { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }


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
            "POP" => ProcessPopAsync(),
            "GraphCredential" => ProcessGraphAsync(GetGraphCredential()),
            "GraphConnection" => ProcessGraphAsync(Connection!.Credential),
            "MgGraphRequest" => ProcessMgGraphAsync(),
            _ => Task.CompletedTask
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

    private GraphCredential GetGraphCredential() => MicrosoftGraphUtils.ConvertFromGraphCredential(
        Credential!.UserName,
        Credential.GetNetworkCredential().Password);

    private async Task ProcessGraphAsync(GraphCredential cred) {

        var filters = new List<string>();
        if (!All.IsPresent) {
            if (!string.IsNullOrEmpty(Subject)) {
                filters.Add($"contains(subject,'{Subject.Replace("'", "''")}')");
            }
            if (!string.IsNullOrEmpty(FromContains)) {
                filters.Add($"contains(from/emailAddress/address,'{FromContains.Replace("'", "''")}')");
            }
            if (!string.IsNullOrEmpty(ToContains)) {
                filters.Add($"toRecipients/any(r:contains(r/emailAddress/address,'{ToContains.Replace("'", "''")}'))");
            }
            if (Since.HasValue) {
                filters.Add($"receivedDateTime ge {Since.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}");
            }
            if (Before.HasValue) {
                filters.Add($"receivedDateTime le {Before.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}");
            }
        }
        if (HasAttachment.IsPresent) {
            filters.Add("hasAttachments eq true");
        }
        var filter = string.Join(" and ", filters);
        if (!string.IsNullOrEmpty(Filter)) {
            filter = string.IsNullOrEmpty(filter) ? Filter : $"{filter} and {Filter}";
        }

        var messages = await MicrosoftGraphUtils.GetMailMessagesAsync(
            cred,
            UserPrincipalName!,
            Property,
            filter,
            Limit);

        foreach (var msg in messages) {
            WriteObject(PSObject.AsPSObject(msg));
            if (Delete.IsPresent && msg.TryGetValue("id", out var idObj) && idObj is string id) {
                await MicrosoftGraphUtils.DeleteMailMessageAsync(cred, UserPrincipalName!, id);
            }
        }
    }


    private Task ProcessMgGraphAsync() {
        var filters = new List<string>();
        if (!All.IsPresent) {
            if (!string.IsNullOrEmpty(Subject)) {
                filters.Add($"contains(subject,'{Subject.Replace("'", "''")}')");
            }
            if (!string.IsNullOrEmpty(FromContains)) {
                filters.Add($"contains(from/emailAddress/address,'{FromContains.Replace("'", "''")}')");
            }
            if (!string.IsNullOrEmpty(ToContains)) {
                filters.Add($"toRecipients/any(r:contains(r/emailAddress/address,'{ToContains.Replace("'", "''")}'))");
            }
            if (Since.HasValue) {
                filters.Add($"receivedDateTime ge {Since.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}");
            }
            if (Before.HasValue) {
                filters.Add($"receivedDateTime le {Before.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}");
            }
        }
        if (HasAttachment.IsPresent) {
            filters.Add("hasAttachments eq true");
        }
        var filter = string.Join(" and ", filters);
        if (!string.IsNullOrEmpty(Filter)) {
            filter = string.IsNullOrEmpty(filter) ? Filter : $"{filter} and {Filter}";
        }

        var query = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(filter)) query["$filter"] = filter;
        if (Property != null && Property.Length > 0) query["$select"] = string.Join(",", Property);
        if (Limit.HasValue) query["$top"] = Limit.Value.ToString();
        var uri = MicrosoftGraphUtils.JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{UserPrincipalName}/messages", query);

        var parameters = new Hashtable { { "Method", "GET" }, { "Uri", uri } };
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest");
        ps.AddParameters(parameters);
        var results = ps.Invoke();
        foreach (var res in results) {
            WriteObject(res);
        }
        return Task.CompletedTask;
    }
}
