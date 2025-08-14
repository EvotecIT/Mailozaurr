using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Retrieves Gmail threads using the Gmail API.
/// </summary>
[Cmdlet(VerbsCommon.Get, "GmailThread")]
[OutputType(typeof(GmailThread))]
[OutputType(typeof(GmailThreadInfo), ParameterSetName = new[] { "List" })]
public sealed class CmdletGetGmailThread : AsyncPSCmdlet {
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
    /// Search query used when listing threads.
    /// </summary>
    [Parameter(ParameterSetName = "List")]
    public string? Query { get; set; }

    /// <summary>
    /// Maximum number of threads to return when listing.
    /// </summary>
    [Parameter(ParameterSetName = "List")]
    public int? MaxResults { get; set; }

    /// <summary>
    /// Identifier of a specific Gmail thread.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Id")]
    public string? Id { get; set; }

    /// <summary>
    /// Retrieves Gmail threads based on the specified parameters.
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
            var thread = await client.GetThreadAsync(GmailAccount!, Id!, CancelToken);
            WriteObject(thread);
            return;
        }
        IList<GmailThreadInfo> list = await client.ListThreadsAsync(GmailAccount!, Query, MaxResults, CancelToken);
        foreach (var t in list) {
            WriteObject(t);
        }
    }
}

