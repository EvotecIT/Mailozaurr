using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Deletes a Gmail message.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "GmailMessage", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class CmdletRemoveGmailMessage : AsyncPSCmdlet {
    /// <summary>
    /// Gmail account containing the message.
    /// </summary>
    [Parameter(Mandatory = true)]
    public string? GmailAccount { get; set; }

    /// <summary>
    /// OAuth credential used to authenticate.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNull]
    public PSCredential? Credential { get; set; }

    /// <summary>
    /// Identifier of the Gmail message to remove.
    /// </summary>
    [Parameter(Mandatory = true)]
    public string? Id { get; set; }

    /// <summary>
    /// Deletes the specified Gmail message.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task ProcessRecordAsync() {
        if (!ShouldProcess(Id!, "Deleting Gmail message")) {
            return;
        }
        var net = Credential!.GetNetworkCredential();
        var oauth = new OAuthCredential {
            UserName = net.UserName,
            AccessToken = net.Password,
            ExpiresOn = System.DateTimeOffset.MaxValue
        };
        var client = new GmailApiClient(oauth);
        await client.DeleteAsync(GmailAccount!, Id!, CancelToken);
    }
}
