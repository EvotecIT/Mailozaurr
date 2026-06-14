using Mailozaurr.DmarcReports;
using System;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Searches for DMARC aggregate reports in a mailbox.</para>
/// <para type="description">The <c>Get-DmarcReport</c> cmdlet queries IMAP, POP3, Microsoft Graph, or Gmail API to find DMARC aggregate reports and expose zipped XML attachments.</para>
/// </summary>
[Cmdlet(VerbsCommon.Get, "DmarcReport")]
[OutputType(typeof(DmarcReport))]
public sealed class CmdletGetDmarcReport : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">Mail protocol to use.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    public EmailProtocol Protocol { get; set; }

    /// <summary>
    /// <para type="description">Optional domain filter.</para>
    /// </summary>
    [Parameter]
    public string? Domain { get; set; }

    /// <summary>
    /// <para type="description">Only reports since this time are returned.</para>
    /// </summary>
    [Parameter]
    public DateTime? Since { get; set; }

    /// <summary>
    /// <para type="description">Only reports before this time are returned.</para>
    /// </summary>
    [Parameter]
    public DateTime? Before { get; set; }

    /// <summary>
    /// <para type="description">Maximum number of reports to return. Default is unlimited.</para>
    /// </summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int Count { get; set; }

    /// <summary>
    /// <para type="description">IMAP folder to search.</para>
    /// </summary>
    [Parameter]
    public string? Folder { get; set; }

    /// <summary>
    /// <para type="description">User principal name when using Microsoft Graph.</para>
    /// </summary>
    [Parameter]
    public string? UserPrincipalName { get; set; }

    /// <summary>
    /// <para type="description">Gmail account address when using Gmail API.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "GmailApi")]
    public string? GmailAccount { get; set; }

    /// <summary>
    /// <para type="description">OAuth credential used for Gmail API authentication.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "GmailApi")]
    [ValidateNotNull]
    public PSCredential? Credential { get; set; }

    /// <summary>
    /// <para type="description">Maximum concurrent MIME downloads; set to 1 to disable parallelism.</para>
    /// </summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int ParallelDownloadLimit { get; set; } = 4;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        int max = Count > 0 ? Count : int.MaxValue;
        switch (Protocol) {
            case EmailProtocol.Imap: {
                    var conn = DefaultSessions.ImapSession;
                    if (conn != null && conn.Data != null) {
                        var reports = await MailboxSearcher.SearchDmarcReportsAsync(
                            conn.Data,
                            Folder,
                            Since,
                            Before,
                            Domain,
                            max,
                            parallelDownloadLimit: ParallelDownloadLimit,
                            cancellationToken: CancelToken);
                        foreach (var report in reports) WriteObject(report);
                    } else {
                        ThrowTerminatingError(new ErrorRecord(
                            new InvalidOperationException("Get-DmarcReport - IMAP client not provided or not connected."),
                            "ClientNotConnected",
                            ErrorCategory.InvalidOperation,
                            null));
                    }
                    break;
                }
            case EmailProtocol.Pop3: {
                    var conn = DefaultSessions.Pop3Session;
                    if (conn != null && conn.Data != null) {
                        var reports = await MailboxSearcher.SearchDmarcReportsAsync(
                            conn.Data,
                            Since,
                            Before,
                            Domain,
                            max,
                            parallelDownloadLimit: ParallelDownloadLimit,
                            cancellationToken: CancelToken);
                        foreach (var report in reports) WriteObject(report);
                    } else {
                        ThrowTerminatingError(new ErrorRecord(
                            new InvalidOperationException("Get-DmarcReport - POP3 client not provided or not connected."),
                            "ClientNotConnected",
                            ErrorCategory.InvalidOperation,
                            null));
                    }
                    break;
                }
            case EmailProtocol.Graph: {
                    var conn = DefaultSessions.GraphSession;
                    if (conn != null && conn.Credential != null && !string.IsNullOrWhiteSpace(UserPrincipalName)) {
                        var reports = await MailboxSearcher.SearchDmarcReportsAsync(
                            conn.Credential,
                            UserPrincipalName!,
                            Since,
                            Before,
                            Domain,
                            max,
                            parallelDownloadLimit: ParallelDownloadLimit,
                            cancellationToken: CancelToken);
                        foreach (var report in reports) WriteObject(report);
                    } else {
                        ThrowTerminatingError(new ErrorRecord(
                            new InvalidOperationException("Get-DmarcReport - Graph connection or UserPrincipalName missing."),
                            "ClientNotConnected",
                            ErrorCategory.InvalidOperation,
                            null));
                    }
                    break;
                }
            case EmailProtocol.GmailApi: {
                    if (Credential != null && !string.IsNullOrWhiteSpace(GmailAccount)) {
                        var net = Credential.GetNetworkCredential();
                        var oauth = new OAuthCredential { UserName = net.UserName, AccessToken = net.Password, ExpiresOn = DateTimeOffset.MaxValue };
                        var client = new GmailApiClient(oauth);
                        var reports = await MailboxSearcher.SearchDmarcReportsAsync(
                            client,
                            GmailAccount!,
                            Since,
                            Before,
                            Domain,
                            max,
                            parallelDownloadLimit: ParallelDownloadLimit,
                            cancellationToken: CancelToken);
                        foreach (var report in reports) WriteObject(report);
                    } else {
                        ThrowTerminatingError(new ErrorRecord(
                            new InvalidOperationException("Get-DmarcReport - Gmail account or Credential missing."),
                            "ClientNotConnected",
                            ErrorCategory.InvalidOperation,
                            null));
                    }
                    break;
                }
        }
    }
}