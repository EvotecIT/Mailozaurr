using System;

using System.Management.Automation;

using System.Threading.Tasks;

using Mailozaurr;

using Mailozaurr.NonDeliveryReports;



namespace Mailozaurr.PowerShell;



/// <summary>

/// Searches for non-delivery reports in a mailbox.

/// </summary>

[Cmdlet(VerbsCommon.Get, "EmailDeliveryStatus")]

[OutputType(typeof(NonDeliveryReport))]

public sealed class CmdletGetEmailDeliveryStatus : AsyncPSCmdlet {

    /// <summary>

    /// Mail protocol to use.

    /// </summary>

    [Parameter(Mandatory = true)]

    public EmailProtocol Protocol { get; set; }



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

    /// Searches for non-delivery reports using the specified protocol.

    /// </summary>

    protected override async Task ProcessRecordAsync() {

        int max = Count > 0 ? Count : int.MaxValue;

        switch (Protocol) {

            case EmailProtocol.Imap: {

                var conn = DefaultSessions.ImapSession;

                if (conn != null && conn.Data != null) {

                    var reports = await MailboxSearcher.SearchNonDeliveryReportsAsync(

                        conn.Data,

                        Folder,

                        Since,

                        Before,

                        Recipient,

                        MessageId,

                        max,

                        CancelToken);

                    foreach (var report in reports) {

                        WriteObject(report);

                    }

                } else {

                    ThrowTerminatingError(new ErrorRecord(

                        new InvalidOperationException("Get-EmailDeliveryStatus - IMAP client not provided or not connected."),

                        "ClientNotConnected",

                        ErrorCategory.InvalidOperation,

                        null));

                }

                break;

            }

            case EmailProtocol.Pop3: {

                var conn = DefaultSessions.Pop3Session;

                if (conn != null && conn.Data != null) {

                    var reports = await MailboxSearcher.SearchNonDeliveryReportsAsync(

                        conn.Data,

                        Since,

                        Before,

                        Recipient,

                        MessageId,

                        max,

                        CancelToken);

                    foreach (var report in reports) {

                        WriteObject(report);

                    }

                } else {

                    ThrowTerminatingError(new ErrorRecord(

                        new InvalidOperationException("Get-EmailDeliveryStatus - POP3 client not provided or not connected."),

                        "ClientNotConnected",

                        ErrorCategory.InvalidOperation,

                        null));

                }

                break;

            }

            case EmailProtocol.Graph: {

                var conn = DefaultSessions.GraphSession;

                if (conn != null && conn.Credential != null && !string.IsNullOrWhiteSpace(UserPrincipalName)) {

                    var reports = await MailboxSearcher.SearchNonDeliveryReportsAsync(

                        conn.Credential,

                        UserPrincipalName!,

                        Since,

                        Before,

                        Recipient,

                        MessageId,

                        max,

                        CancelToken);

                    foreach (var report in reports) {

                        WriteObject(report);

                    }

                } else {

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
