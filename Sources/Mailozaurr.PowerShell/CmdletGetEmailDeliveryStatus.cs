using System;
using System.Management.Automation;
using System.Threading.Tasks;
using Mailozaurr;
using Mailozaurr.NonDeliveryReports;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Searches for non-delivery reports in a mailbox.</para>
/// <para type="description">The <c>Get-EmailDeliveryStatus</c> cmdlet queries IMAP, POP3, or Microsoft Graph to find non-delivery reports using optional filters.</para>
/// </summary>
[Cmdlet(VerbsCommon.Get, "EmailDeliveryStatus")]
[OutputType(typeof(NonDeliveryReport))]
public sealed class CmdletGetEmailDeliveryStatus : AsyncPSCmdlet
{
    /// <summary>
    /// <para type="description">Mail protocol to use.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    public EmailProtocol Protocol { get; set; }

    /// <summary>
    /// <para type="description">Only reports for recipients containing this value are returned.</para>
    /// </summary>
    [Parameter]
    public string? Recipient { get; set; }

    /// <summary>
    /// <para type="description">Only reports matching this message ID are returned.</para>
    /// </summary>
    [Parameter]
    public string? MessageId { get; set; }

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
    /// <para type="description">Maximum concurrent MIME downloads; set to 1 to disable parallelism.</para>
    /// </summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int ParallelDownloadLimit { get; set; } = 4;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync()
    {
        int max = Count > 0 ? Count : int.MaxValue;
        switch (Protocol)
        {
            case EmailProtocol.Imap:
            {
                var conn = DefaultSessions.ImapSession;
                if (conn != null && conn.Data != null)
                {
                    var reports = await MailboxSearcher.SearchNonDeliveryReportsAsync(
                        conn.Data,
                        Folder,
                        Since,
                        Before,
                        Recipient,
                        MessageId,
                        max,
                        parallelDownloadLimit: ParallelDownloadLimit,
                        cancellationToken: CancelToken);
                    foreach (var report in reports)
                    {
                        WriteObject(report);
                    }
                }
                else
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new InvalidOperationException("Get-EmailDeliveryStatus - IMAP client not provided or not connected."),
                        "ClientNotConnected",
                        ErrorCategory.InvalidOperation,
                        null));
                }

                break;
            }
            case EmailProtocol.Pop3:
            {
                var conn = DefaultSessions.Pop3Session;
                if (conn != null && conn.Data != null)
                {
                    var reports = await MailboxSearcher.SearchNonDeliveryReportsAsync(
                        conn.Data,
                        Since,
                        Before,
                        Recipient,
                        MessageId,
                        max,
                        parallelDownloadLimit: ParallelDownloadLimit,
                        cancellationToken: CancelToken);
                    foreach (var report in reports)
                    {
                        WriteObject(report);
                    }
                }
                else
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new InvalidOperationException("Get-EmailDeliveryStatus - POP3 client not provided or not connected."),
                        "ClientNotConnected",
                        ErrorCategory.InvalidOperation,
                        null));
                }

                break;
            }
            case EmailProtocol.Graph:
            {
                var conn = DefaultSessions.GraphSession;
                if (conn != null && conn.Credential != null && !string.IsNullOrWhiteSpace(UserPrincipalName))
                {
                    var reports = await MailboxSearcher.SearchNonDeliveryReportsAsync(
                        conn.Credential,
                        UserPrincipalName!,
                        Since,
                        Before,
                        Recipient,
                        MessageId,
                        max,
                        parallelDownloadLimit: ParallelDownloadLimit,
                        cancellationToken: CancelToken);
                    foreach (var report in reports)
                    {
                        WriteObject(report);
                    }
                }
                else
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new InvalidOperationException("Get-EmailDeliveryStatus - Graph connection or UserPrincipalName missing."),
                        "ClientNotConnected",
                        ErrorCategory.InvalidOperation,
                        null));
                }

                break;
            }
        }
    }
}
