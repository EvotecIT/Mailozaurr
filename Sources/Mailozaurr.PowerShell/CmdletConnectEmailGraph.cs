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
    [ValidateNotNullOrEmpty]
    public string? ClientId { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Plain")]
    [ValidateNotNullOrEmpty]
    public string? ClientSecret { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Plain")]
    [Parameter(Mandatory = true, ParameterSetName = "Certificate")]
    [ValidateNotNullOrEmpty]
    public string? DirectoryId { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Certificate")]
    [ValidateNotNullOrEmpty]
    public string? CertificatePath { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Certificate")]
    [ValidateNotNullOrEmpty]
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// <para type="description">Specifies how many times the cmdlet should retry when obtaining the access token. Default is 0 (no retries).</para>
    /// </summary>
    [Parameter]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// <para type="description">Delay in milliseconds between retry attempts.</para>
    /// </summary>
    [Parameter]
    public int RetryDelayMilliseconds { get; set; } = 0;

    /// <summary>
    /// <para type="description">Multiplicative backoff applied to the retry delay. Value of 1 disables backoff.</para>
    /// </summary>
    [Parameter]
    public double RetryDelayBackoff { get; set; } = 1.0;

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
        } else {
            cred = new GraphCredential {
                ClientId = ClientId!,
                ClientSecret = ClientSecret!,
                DirectoryId = DirectoryId!
            };
        }

        bool connected = false;
        try {
            var token = await MicrosoftGraphUtils.ConnectO365GraphAsync(
                cred,
                cred.DirectoryId,
                "https://graph.microsoft.com",
                RetryCount,
                RetryDelayMilliseconds,
                RetryDelayBackoff);
            connected = !string.IsNullOrEmpty(token);
        } catch (Exception ex) {
            WriteWarning($"Connect-EmailGraph - {ex.Message}");
        }

        var info = new GraphConnectionInfo { Credential = cred, IsConnected = connected };
        DefaultSessions.GraphSession = info;
        WriteObject(info);
    }
}
