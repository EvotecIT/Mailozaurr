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
    [Parameter(Mandatory = true)]
    public string? GmailAccount { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNull]
    public PSCredential? Credential { get; set; }

    [Parameter(ParameterSetName = "List")]
    public string? Query { get; set; }

    [Parameter(ParameterSetName = "List")]
    public int? MaxResults { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Id")]
    public string? Id { get; set; }

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
