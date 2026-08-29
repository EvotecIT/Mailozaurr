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
    /// <para type="description">Maximum number of reports to return. Defaults to 1000.</para>
    /// </summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int Count { get; set; } = DmarcReportInspectionOptions.DefaultMaxMessagesScanned;

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

    /// <summary>
    /// <para type="description">Maximum uncompressed attachment size to inspect, in bytes.</para>
    /// </summary>
    [Parameter]
    [ValidateRange(1, long.MaxValue)]
    public long MaxUncompressedSize { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// <para type="description">Maximum uncompressed bytes inspected across the complete search.</para>
    /// </summary>
    [Parameter]
    [ValidateRange(1, long.MaxValue)]
    public long MaxTotalUncompressedSize { get; set; } = DmarcReportInspectionOptions.DefaultMaxTotalUncompressedBytes;

    /// <summary>
    /// <para type="description">Maximum number of DMARC attachments accepted from one message.</para>
    /// </summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxAttachmentsPerMessage { get; set; } = DmarcReportInspectionOptions.DefaultMaxAttachmentsPerMessage;

    /// <summary>
    /// <para type="description">Maximum number of entries accepted in one DMARC ZIP attachment.</para>
    /// </summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxArchiveEntriesPerAttachment { get; set; } = DmarcReportInspectionOptions.DefaultMaxArchiveEntriesPerAttachment;

    /// <summary><para type="description">Maximum number of candidate mailbox messages inspected.</para></summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxMessagesScanned { get; set; } = DmarcReportInspectionOptions.DefaultMaxMessagesScanned;

    /// <summary><para type="description">Maximum downloaded MIME bytes for one candidate message.</para></summary>
    [Parameter]
    [ValidateRange(1, long.MaxValue)]
    public long MaxMimeBytesPerMessage { get; set; } = DmarcReportInspectionOptions.DefaultMaxMimeBytesPerMessage;

    /// <summary><para type="description">Maximum downloaded MIME bytes across the search.</para></summary>
    [Parameter]
    [ValidateRange(1, long.MaxValue)]
    public long MaxTotalMimeBytes { get; set; } = DmarcReportInspectionOptions.DefaultMaxTotalMimeBytes;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        int max = Count;
        long totalUncompressedLimit = ResolveTotalLimit(
            MaxTotalUncompressedSize,
            MaxUncompressedSize,
            MyInvocation.BoundParameters.ContainsKey(nameof(MaxTotalUncompressedSize)));
        long totalMimeLimit = ResolveTotalLimit(
            MaxTotalMimeBytes,
            MaxMimeBytesPerMessage,
            MyInvocation.BoundParameters.ContainsKey(nameof(MaxTotalMimeBytes)));
        var inspectionOptions = new DmarcReportInspectionOptions {
            MaxUncompressedBytesPerAttachment = MaxUncompressedSize,
            MaxTotalUncompressedBytes = totalUncompressedLimit,
            MaxAttachmentsPerMessage = MaxAttachmentsPerMessage,
            MaxArchiveEntriesPerAttachment = MaxArchiveEntriesPerAttachment,
            MaxMessagesScanned = MaxMessagesScanned,
            MaxMimeBytesPerMessage = MaxMimeBytesPerMessage,
            MaxTotalMimeBytes = totalMimeLimit
        };
        switch (Protocol) {
            case EmailProtocol.Imap: {
                    var conn = DefaultSessions.ImapSession;
                    if (conn != null && conn.Data != null) {
                        var reports = await MailboxSearcher.SearchDmarcReportsAsync(
                            conn.Data,
                            inspectionOptions,
                            folder: Folder,
                            since: Since,
                            before: Before,
                            domain: Domain,
                            maxResults: max,
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
                            inspectionOptions,
                            since: Since,
                            before: Before,
                            domain: Domain,
                            maxResults: max,
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
                        var reports = await GraphMailboxSearcher.SearchDmarcReportsAsync(
                            conn.Credential,
                            UserPrincipalName!,
                            inspectionOptions,
                            since: Since,
                            before: Before,
                            domain: Domain,
                            maxResults: max,
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
                        using var client = new GmailApiClient(oauth);
                        var reports = await GmailMailboxSearcher.SearchDmarcReportsAsync(
                            client,
                            GmailAccount!,
                            inspectionOptions,
                            since: Since,
                            before: Before,
                            domain: Domain,
                            maxResults: max,
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

    internal static long ResolveTotalLimit(long configuredTotal, long perItem, bool totalWasExplicitlyBound) =>
        totalWasExplicitlyBound ? configuredTotal : Math.Max(configuredTotal, perItem);
}
