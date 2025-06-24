using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Connects to Microsoft Graph using application credentials.
/// </summary>
[Cmdlet(VerbsCommunications.Connect, "EmailGraph")]
[OutputType(typeof(GraphConnectionInfo))]
public sealed class CmdletConnectEmailGraph : AsyncPSCmdlet {
    [Parameter(Mandatory = true, ParameterSetName = "Credential")]
    [ValidateNotNull]
    public PSCredential? Credential { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Plain")]
    [ValidateNotNullOrEmpty]
    public string? ClientId { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Plain")]
    [ValidateNotNullOrEmpty]
    public string? ClientSecret { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Plain")]
    [ValidateNotNullOrEmpty]
    public string? DirectoryId { get; set; }

    protected override async Task ProcessRecordAsync() {
        GraphCredential cred;
        if (ParameterSetName == "Credential") {
            cred = MicrosoftGraphUtils.ConvertFromGraphCredential(
                Credential!.UserName,
                Credential.GetNetworkCredential().Password);
        } else {
            cred = new GraphCredential { ClientId = ClientId!, ClientSecret = ClientSecret!, DirectoryId = DirectoryId! };
        }

        bool connected = false;
        try {
            var token = await MicrosoftGraphUtils.ConnectO365GraphAsync(cred, cred.DirectoryId, "https://graph.microsoft.com");
            connected = !string.IsNullOrEmpty(token);
        } catch {
            // ignore errors, return not connected
        }

        var info = new GraphConnectionInfo { Credential = cred, IsConnected = connected };
        WriteObject(info);
    }
}
