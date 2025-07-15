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
    /// <summary>
    /// Credential object containing client ID and secret.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Credential")]
    [ValidateNotNull]
    public PSCredential? Credential { get; set; }

    /// <summary>
    /// Client (application) identifier.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Plain")]
    [Parameter(Mandatory = true, ParameterSetName = "Certificate")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytes")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificatePem")]
    [Parameter(Mandatory = true, ParameterSetName = "DeviceCode")]
    [Parameter(Mandatory = true, ParameterSetName = "OnBehalfOf")]
    [ValidateNotNullOrEmpty]
    public string? ClientId { get; set; }

    /// <summary>
    /// Secret associated with the application (for app-only auth).
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Plain")]
    [Parameter(Mandatory = true, ParameterSetName = "OnBehalfOf")]
    [ValidateNotNullOrEmpty]
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Directory (tenant) identifier.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Plain")]
    [Parameter(Mandatory = true, ParameterSetName = "Certificate")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytes")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificatePem")]
    [Parameter(Mandatory = true, ParameterSetName = "DeviceCode")]
    [Parameter(Mandatory = true, ParameterSetName = "OnBehalfOf")]
    [ValidateNotNullOrEmpty]
    public string? DirectoryId { get; set; }

    /// <summary>
    /// Path to a PFX certificate used for authentication.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Certificate")]
    [ValidateNotNullOrEmpty]
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Raw bytes of a PFX certificate used for authentication.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytes")]
    [ValidateNotNull]
    public byte[]? CertificateBytes { get; set; }

    /// <summary>
    /// Path to a PEM encoded certificate used for authentication.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "CertificatePem")]
    [ValidateNotNullOrEmpty]
    public string? CertificatePemPath { get; set; }

    /// <summary>
    /// Password used to decrypt the certificate file.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Certificate")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytes")]
    [ValidateNotNullOrEmpty]
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Use the device code flow for authentication.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "DeviceCode")]
    public SwitchParameter DeviceCode { get; set; }

    /// <summary>
    /// Access token to use for on-behalf-of authentication.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "OnBehalfOf")]
    [ValidateNotNullOrEmpty]
    public string? OnBehalfOfToken { get; set; }

    /// <summary>
    /// Microsoft Graph permission scopes to request.
    /// </summary>
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

    /// <summary>
    /// Request timeout for Microsoft Graph operations in seconds.
    /// </summary>
    [Parameter]
    public int TimeoutSeconds { get; set; } = 100;

    /// <summary>
    /// Maximum number of concurrent Microsoft Graph requests.
    /// </summary>
    [Parameter]
    public int MaxConcurrentRequests { get; set; } = 5;

    /// <summary>
    /// Connects to Microsoft Graph using the provided authentication details.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
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
        MicrosoftGraphUtils.MaxConcurrentRequests = MaxConcurrentRequests;
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
