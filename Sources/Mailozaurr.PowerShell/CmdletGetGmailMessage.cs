using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Retrieves messages using the Gmail API.
/// </summary>
[Cmdlet(VerbsCommon.Get, "GmailMessage")]
[OutputType(typeof(GmailMessage))]
public sealed class CmdletGetGmailMessage : AsyncPSCmdlet {
    /// <summary>
    /// Gmail account address to operate on.
    /// </summary>
    [Parameter(Mandatory = true)]
    public string? GmailAccount { get; set; }

    /// <summary>
    /// OAuth credential used to authenticate to Gmail.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNull]
    public PSCredential? Credential { get; set; }

    /// <summary>
    /// Search query used when listing messages.
    /// </summary>
    [Parameter(ParameterSetName = "List")]
    public string? Query { get; set; }

    /// <summary>
    /// Maximum number of messages to return when listing.
    /// </summary>
    [Parameter(ParameterSetName = "List")]
    public int? MaxResults { get; set; }

    /// <summary>
    /// Identifier of a specific Gmail message.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Id")]
    public string? Id { get; set; }

    /// <summary>
    /// Retrieves Gmail messages based on the specified parameters.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task ProcessRecordAsync() {
        var net = Credential!.GetNetworkCredential();
        var oauth = new OAuthCredential {
            UserName = net.UserName,
            AccessToken = net.Password,
            ExpiresOn = System.DateTimeOffset.MaxValue
        };
        var client = new GmailApiClient(oauth);
        if (ParameterSetName == "Id") {
            var msg = await client.GetAsync(GmailAccount!, Id!, CancelToken);
            WriteObject(msg);
            return;
        }
        IList<GmailMessage> list = await client.ListAsync(GmailAccount!, Query, MaxResults, CancelToken);
        foreach (var m in list) {
            WriteObject(m);
        }
    }
}
