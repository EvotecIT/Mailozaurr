using System.Management.Automation;
using System.Threading.Tasks;
using Mailozaurr;

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
    [Parameter(Mandatory = true, ParameterSetName = "DeviceCode")]
    [Parameter(Mandatory = true, ParameterSetName = "OnBehalfOf")]
    [ValidateNotNullOrEmpty]
    public string? ClientId { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Plain")]
    [Parameter(Mandatory = true, ParameterSetName = "OnBehalfOf")]
    [ValidateNotNullOrEmpty]
    public string? ClientSecret { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Plain")]
    [Parameter(Mandatory = true, ParameterSetName = "Certificate")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytes")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificatePem")]
    [Parameter(Mandatory = true, ParameterSetName = "DeviceCode")]
    [Parameter(Mandatory = true, ParameterSetName = "OnBehalfOf")]
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

    [Parameter(Mandatory = true, ParameterSetName = "DeviceCode")]
    public SwitchParameter DeviceCode { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "OnBehalfOf")]
    [ValidateNotNullOrEmpty]
    public string? OnBehalfOfToken { get; set; }

    [Parameter(ParameterSetName = "DeviceCode")]
    [Parameter(ParameterSetName = "OnBehalfOf")]
    public string[] Scopes { get; set; } = new[] { "https://graph.microsoft.com/.default" };

    /// <summary>
    /// <para type="description">Number of connection retry attempts.</para>
    /// </summary>
    [Parameter]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// <para type="description">Delay in milliseconds between retries.</para>
    /// </summary>
    [Parameter]
    public int RetryDelayMilliseconds { get; set; } = 0;

    /// <summary>
    /// <para type="description">Multiplier for increasing retry delay.</para>
    /// </summary>
    [Parameter]
    public double RetryDelayBackoff { get; set; } = 1.0;

    [Parameter]
    public int TimeoutSeconds { get; set; } = 100;

    protected override async Task ProcessRecordAsync() {
        GraphCredential cred;
        OAuthCredential? oauth = null;
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
        } else if (ParameterSetName == "DeviceCode") {
            cred = new GraphCredential {
                ClientId = ClientId!,
                DirectoryId = DirectoryId!
            };
            oauth = await OAuthHelpers.AcquireO365TokenDeviceCodeAsync(
                ClientId!,
                DirectoryId!,
                Scopes);
        } else if (ParameterSetName == "OnBehalfOf") {
            cred = new GraphCredential {
                ClientId = ClientId!,
                ClientSecret = ClientSecret!,
                DirectoryId = DirectoryId!
            };
            oauth = await OAuthHelpers.AcquireO365TokenOnBehalfOfAsync(
                ClientId!,
                DirectoryId!,
                ClientSecret!,
                OnBehalfOfToken!,
                Scopes);
        } else {
            cred = new GraphCredential {
                ClientId = ClientId!,
                ClientSecret = ClientSecret!,
                DirectoryId = DirectoryId!
            };
        }

        bool connected = false;
        if (ParameterSetName == "DeviceCode" || ParameterSetName == "OnBehalfOf") {
            connected = oauth != null;
        } else {
            try {
                var token = await MicrosoftGraphUtils.ConnectO365GraphWithRetryAsync(
                    cred,
                    cred.DirectoryId!,
                    RetryCount,
                    RetryDelayMilliseconds,
                    RetryDelayBackoff,
                    "https://graph.microsoft.com");
                connected = !string.IsNullOrWhiteSpace(token);
            } catch (GraphApiException ex) {
                WriteError(new ErrorRecord(ex, "GraphApiError", ErrorCategory.InvalidOperation, null));
            }
        }
        
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        try {
            var token = await MicrosoftGraphUtils.ConnectO365GraphWithRetryAsync(
                cred,
                cred.DirectoryId!,
                RetryCount,
                RetryDelayMilliseconds,
                RetryDelayBackoff,
                "https://graph.microsoft.com");
            connected = !string.IsNullOrWhiteSpace(token);
        } catch (GraphApiException ex) {
            WriteError(new ErrorRecord(ex, "GraphApiError", ErrorCategory.InvalidOperation, null));
        }

        var info = new GraphConnectionInfo { Credential = cred, IsConnected = connected, OAuthCredential = oauth };
        DefaultSessions.GraphSession = info;
        WriteObject(info);
    }
}
