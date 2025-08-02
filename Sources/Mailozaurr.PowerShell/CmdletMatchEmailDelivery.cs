using System;

using System.Management.Automation;

using System.Threading.Tasks;

using Mailozaurr;

using Mailozaurr.NonDeliveryReports;



namespace Mailozaurr.PowerShell;



/// <summary>

/// Searches for non-delivery reports and matches them to sent messages.

/// </summary>

[Cmdlet("Match", "EmailDelivery")]

[OutputType(typeof(NonDeliveryReportResult))]

public sealed class CmdletMatchEmailDelivery : AsyncPSCmdlet {

    /// <summary>

    /// Mail protocol to use.

    /// </summary>

    [Parameter(Mandatory = true)]

    public EmailProtocol Protocol { get; set; }



    /// <summary>

    /// Resolver used to correlate NDRs with sent messages.

    /// </summary>

    [Parameter(Mandatory = true)]

    [ValidateNotNull]

    public SendLogResolver? Resolver { get; set; }



    /// <summary>

    /// Only reports for recipients containing this value are returned.

    /// </summary>

    [Parameter]

    public string? Recipient { get; set; }



    /// <summary>

    /// Only reports matching this message ID are returned.

    /// </summary>

    [Parameter]

    public string? MessageId { get; set; }



    /// <summary>

    /// Only reports since this time are returned.

    /// </summary>

    [Parameter]

    public DateTime? Since { get; set; }



    /// <summary>

    /// Only reports before this time are returned.

    /// </summary>

    [Parameter]

    public DateTime? Before { get; set; }



    /// <summary>

    /// Maximum number of reports to return.

    /// </summary>

    [Parameter]

    [ValidateRange(1, int.MaxValue)]

    public int Count { get; set; }



    /// <summary>

    /// IMAP folder to search.

    /// </summary>

    [Parameter]

    public string? Folder { get; set; }



    /// <summary>

    /// User principal name when using Microsoft Graph.

    /// </summary>

    [Parameter]

    public string? UserPrincipalName { get; set; }



    /// <summary>

    /// Searches for non-delivery reports and matches them using the resolver.

    /// </summary>

    protected override async Task ProcessRecordAsync() {

        if (Resolver == null) {

            ThrowTerminatingError(new ErrorRecord(

                new InvalidOperationException("Match-EmailDelivery - Resolver is required."),

                "ResolverMissing",

                ErrorCategory.InvalidArgument,

                null));

        }



        int max = Count > 0 ? Count : int.MaxValue;

        switch (Protocol) {

            case EmailProtocol.Imap: {

                var conn = DefaultSessions.ImapSession;

                if (conn != null && conn.Data != null) {

                    var service = new ImapNonDeliveryReportService(conn.Data, Resolver!, Folder);

                    var results = await service.SearchAsync(Since, Before, Recipient, MessageId, max, CancelToken);

                    foreach (var result in results) {

                        WriteObject(result);

                    }

                } else {

                    ThrowTerminatingError(new ErrorRecord(

                        new InvalidOperationException("Match-EmailDelivery - IMAP client not provided or not connected."),

                        "ClientNotConnected",

                        ErrorCategory.InvalidOperation,

                        null));

                }

                break;

            }

            case EmailProtocol.Pop3: {

                var conn = DefaultSessions.Pop3Session;

                if (conn != null && conn.Data != null) {

                    var service = new Pop3NonDeliveryReportService(conn.Data, Resolver!);

                    var results = await service.SearchAsync(Since, Before, Recipient, MessageId, max, CancelToken);

                    foreach (var result in results) {

                        WriteObject(result);

                    }

                } else {

                    ThrowTerminatingError(new ErrorRecord(

                        new InvalidOperationException("Match-EmailDelivery - POP3 client not provided or not connected."),

                        "ClientNotConnected",

                        ErrorCategory.InvalidOperation,

                        null));

                }

                break;

            }

            case EmailProtocol.Graph: {

                var conn = DefaultSessions.GraphSession;

                if (conn != null && conn.Credential != null && !string.IsNullOrWhiteSpace(UserPrincipalName)) {

                    var service = new GraphNonDeliveryReportService(conn.Credential, UserPrincipalName!, Resolver!);

                    var results = await service.SearchAsync(Since, Before, Recipient, MessageId, max, CancelToken);

                    foreach (var result in results) {

                        WriteObject(result);

                    }

                } else {

                    ThrowTerminatingError(new ErrorRecord(

                        new InvalidOperationException("Match-EmailDelivery - Graph connection or UserPrincipalName missing."),

                        "ClientNotConnected",

                        ErrorCategory.InvalidOperation,

                        null));

                }

                break;

            }

        }

    }

}
