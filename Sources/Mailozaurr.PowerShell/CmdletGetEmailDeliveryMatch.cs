using System;
using System.Management.Automation;
using System.Threading.Tasks;
using Mailozaurr;
using Mailozaurr.NonDeliveryReports;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Searches for non-delivery reports and matches them to sent messages.</para>
/// <para type="description">The <c>Get-EmailDeliveryMatch</c> cmdlet searches for non-delivery reports and uses a <see cref="SendLogResolver"/> to correlate them with sent messages.</para>
/// </summary>
[Cmdlet(VerbsCommon.Get, "EmailDeliveryMatch")]
[OutputType(typeof(NonDeliveryReportResult))]
public sealed class CmdletGetEmailDeliveryMatch : AsyncPSCmdlet
{
    /// <summary>
    /// <para type="description">Mail protocol to use.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    public EmailProtocol Protocol { get; set; }

    /// <summary>
    /// <para type="description">Resolver used to correlate NDRs with sent messages.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNull]
    public SendLogResolver? Resolver { get; set; }

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

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync()
    {
        if (Resolver == null)
        {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Get-EmailDeliveryMatch - Resolver is required."),
                "ResolverMissing",
                ErrorCategory.InvalidArgument,
                null));
            return;
        }

        int max = Count > 0 ? Count : int.MaxValue;
        switch (Protocol)
        {
            case EmailProtocol.Imap:
            {
                var conn = DefaultSessions.ImapSession;
                if (conn != null && conn.Data != null)
                {
                    var service = new ImapNonDeliveryReportService(conn.Data, Resolver, Folder);
                    var results = await service.SearchAsync(Since, Before, Recipient, MessageId, max, CancelToken);
                    foreach (var result in results)
                    {
                        WriteObject(result);
                    }
                }
                else
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new InvalidOperationException("Get-EmailDeliveryMatch - IMAP client not provided or not connected."),
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
                    var service = new Pop3NonDeliveryReportService(conn.Data, Resolver);
                    var results = await service.SearchAsync(Since, Before, Recipient, MessageId, max, CancelToken);
                    foreach (var result in results)
                    {
                        WriteObject(result);
                    }
                }
                else
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new InvalidOperationException("Get-EmailDeliveryMatch - POP3 client not provided or not connected."),
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
                    var service = new GraphNonDeliveryReportService(conn.Credential, UserPrincipalName!, Resolver);
                    var results = await service.SearchAsync(Since, Before, Recipient, MessageId, max, CancelToken);
                    foreach (var result in results)
                    {
                        WriteObject(result);
                    }
                }
                else
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new InvalidOperationException("Get-EmailDeliveryMatch - Graph connection or UserPrincipalName missing."),
                        "ClientNotConnected",
                        ErrorCategory.InvalidOperation,
                        null));
                }

                break;
            }
        }
    }
}
