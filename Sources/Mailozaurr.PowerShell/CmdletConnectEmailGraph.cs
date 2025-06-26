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
    [Parameter(Mandatory = true, ParameterSetName = "Certificate")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytes")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificatePem")]
    [ValidateNotNullOrEmpty]
    public string? ClientId { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Plain")]
    [ValidateNotNullOrEmpty]
    public string? ClientSecret { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Plain")]
    [Parameter(Mandatory = true, ParameterSetName = "Certificate")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytes")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificatePem")]
    [ValidateNotNullOrEmpty]
    public string? DirectoryId { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Certificate")]
    [ValidateNotNullOrEmpty]
    public string? CertificatePath { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytes")]
    [ValidateNotNull]
    public byte[]? CertificateBytes { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "CertificatePem")]
    [ValidateNotNullOrEmpty]
    public string? CertificatePemPath { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Certificate")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytes")]
    [ValidateNotNullOrEmpty]
    public string? CertificatePassword { get; set; }

    protected override async Task ProcessRecordAsync() {
        GraphCredential cred;
        if (ParameterSetName == "Credential") {
            cred = MicrosoftGraphUtils.ConvertFromGraphCredential(
                Credential!.UserName,
                Credential.GetNetworkCredential().Password);
        } else if (ParameterSetName == "Certificate") {
            cred = new GraphCredential {
                ClientId = ClientId!,
                DirectoryId = DirectoryId!,
                CertificatePath = CertificatePath!,
                CertificatePassword = CertificatePassword!
            };
        } else if (ParameterSetName == "CertificateBytes") {
            cred = new GraphCredential {
                ClientId = ClientId!,
                DirectoryId = DirectoryId!,
                CertificateBytes = CertificateBytes!,
                CertificatePassword = CertificatePassword!
            };
        } else if (ParameterSetName == "CertificatePem") {
            cred = new GraphCredential {
                ClientId = ClientId!,
                DirectoryId = DirectoryId!,
                CertificatePemPath = CertificatePemPath!
            };
        } else {
            cred = new GraphCredential {
                ClientId = ClientId!,
                ClientSecret = ClientSecret!,
                DirectoryId = DirectoryId!
            };
        }

        bool connected = false;
        try {
            var token = await MicrosoftGraphUtils.ConnectO365GraphAsync(cred, cred.DirectoryId, "https://graph.microsoft.com");
            connected = !string.IsNullOrEmpty(token);
        } catch {
            // ignore errors, return not connected
        }

        var info = new GraphConnectionInfo { Credential = cred, IsConnected = connected };
        DefaultSessions.GraphSession = info;
        WriteObject(info);
    }
}
