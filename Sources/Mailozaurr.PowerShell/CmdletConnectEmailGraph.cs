using System.Management.Automation;
using System.Security;
using System.Threading.Tasks;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Connects to Microsoft Graph using application credentials, certificates, device code, or on-behalf-of authentication.</para>
/// <para type="description">The <c>Connect-EmailGraph</c> cmdlet creates a Microsoft Graph connection for Mailozaurr cmdlets. It supports client secret, certificate, device code, and on-behalf-of flows, and can authenticate directly or from a prebuilt <see cref="PSCredential"/>.</para>
/// <para type="description">For new scripts, prefer the <c>SecureString</c> or <c>SecretManagement</c> parameter sets instead of passing secrets and tokens in plain text.</para>
/// <example>
///   <summary>Connect to Microsoft Graph using a vault-backed client secret</summary>
///   <code>Connect-EmailGraph -ClientId "id" -DirectoryId "tenant" -ClientSecretSecretName "graph-client-secret" -ClientSecretVaultName "MailSecrets"</code>
/// </example>
/// <example>
///   <summary>Connect to Microsoft Graph using a certificate password stored as a SecureString</summary>
///   <code>$certificatePassword = Read-Host "Certificate password" -AsSecureString
/// Connect-EmailGraph -ClientId "id" -DirectoryId "tenant" -CertificatePath "cert.pfx" -CertificatePasswordSecureString $certificatePassword</code>
/// </example>
/// <remarks>
/// The vault example requires a <c>Get-Secret</c> implementation such as Microsoft.PowerShell.SecretManagement.
/// </remarks>
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
    [Parameter(Mandatory = true, ParameterSetName = "PlainSecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateSecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytesSecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "OnBehalfOfSecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "PlainSecretManagement")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateSecretManagement")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytesSecretManagement")]
    [Parameter(Mandatory = true, ParameterSetName = "OnBehalfOfSecretManagement")]
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
    /// Secret associated with the application (for app-only auth) as a SecureString.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "PlainSecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "OnBehalfOfSecureString")]
    [ValidateNotNull]
    public SecureString? ClientSecretSecureString { get; set; }

    /// <summary>
    /// Secret name used with Get-Secret for the application client secret.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "PlainSecretManagement")]
    [Parameter(Mandatory = true, ParameterSetName = "OnBehalfOfSecretManagement")]
    [ValidateNotNullOrEmpty]
    public string? ClientSecretSecretName { get; set; }

    /// <summary>
    /// Optional vault name used with Get-Secret for the application client secret.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "PlainSecretManagement")]
    [Parameter(Mandatory = false, ParameterSetName = "OnBehalfOfSecretManagement")]
    public string? ClientSecretVaultName { get; set; }

    /// <summary>
    /// Directory (tenant) identifier.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Plain")]
    [Parameter(Mandatory = true, ParameterSetName = "Certificate")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytes")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificatePem")]
    [Parameter(Mandatory = true, ParameterSetName = "DeviceCode")]
    [Parameter(Mandatory = true, ParameterSetName = "OnBehalfOf")]
    [Parameter(Mandatory = true, ParameterSetName = "PlainSecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateSecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytesSecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "OnBehalfOfSecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "PlainSecretManagement")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateSecretManagement")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytesSecretManagement")]
    [Parameter(Mandatory = true, ParameterSetName = "OnBehalfOfSecretManagement")]
    [ValidateNotNullOrEmpty]
    public string? DirectoryId { get; set; }

    /// <summary>
    /// Path to a PFX certificate used for authentication.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Certificate")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateSecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateSecretManagement")]
    [ValidateNotNullOrEmpty]
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Raw bytes of a PFX certificate used for authentication.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytes")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytesSecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytesSecretManagement")]
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
    /// Password used to decrypt the certificate file as a SecureString.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "CertificateSecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytesSecureString")]
    [ValidateNotNull]
    public SecureString? CertificatePasswordSecureString { get; set; }

    /// <summary>
    /// Secret name used with Get-Secret for the certificate password.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "CertificateSecretManagement")]
    [Parameter(Mandatory = true, ParameterSetName = "CertificateBytesSecretManagement")]
    [ValidateNotNullOrEmpty]
    public string? CertificatePasswordSecretName { get; set; }

    /// <summary>
    /// Optional vault name used with Get-Secret for the certificate password.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "CertificateSecretManagement")]
    [Parameter(Mandatory = false, ParameterSetName = "CertificateBytesSecretManagement")]
    public string? CertificatePasswordVaultName { get; set; }

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
    /// Access token to use for on-behalf-of authentication as a SecureString.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "OnBehalfOfSecureString")]
    [ValidateNotNull]
    public SecureString? OnBehalfOfTokenSecureString { get; set; }

    /// <summary>
    /// Secret name used with Get-Secret for the on-behalf-of token.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "OnBehalfOfSecretManagement")]
    [ValidateNotNullOrEmpty]
    public string? OnBehalfOfTokenSecretName { get; set; }

    /// <summary>
    /// Optional vault name used with Get-Secret for the on-behalf-of token.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "OnBehalfOfSecretManagement")]
    public string? OnBehalfOfTokenVaultName { get; set; }

    /// <summary>
    /// Microsoft Graph permission scopes to request.
    /// </summary>
    [Parameter(ParameterSetName = "DeviceCode")]
    [Parameter(ParameterSetName = "OnBehalfOf")]
    [Parameter(ParameterSetName = "OnBehalfOfSecureString")]
    [Parameter(ParameterSetName = "OnBehalfOfSecretManagement")]
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
        var clientSecret = ParameterSetName == "PlainSecretManagement" || ParameterSetName == "OnBehalfOfSecretManagement"
            ? CredentialHelpers.ToPlainText(CredentialHelpers.ResolveSecretFromVault(ClientSecretSecretName!, ClientSecretVaultName))
            : CredentialHelpers.ToPlainText(ClientSecretSecureString);
        var certificatePassword = ParameterSetName == "CertificateSecretManagement" || ParameterSetName == "CertificateBytesSecretManagement"
            ? CredentialHelpers.ToPlainText(CredentialHelpers.ResolveSecretFromVault(CertificatePasswordSecretName!, CertificatePasswordVaultName))
            : CredentialHelpers.ToPlainText(CertificatePasswordSecureString);
        var onBehalfOfToken = ParameterSetName == "OnBehalfOfSecretManagement"
            ? CredentialHelpers.ToPlainText(CredentialHelpers.ResolveSecretFromVault(OnBehalfOfTokenSecretName!, OnBehalfOfTokenVaultName))
            : CredentialHelpers.ToPlainText(OnBehalfOfTokenSecureString);
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
        } else if (ParameterSetName == "CertificateSecureString") {
            cred = new GraphCredential {
                ClientId = ClientId!,
                DirectoryId = DirectoryId!,
                CertificatePath = CertificatePath!,
                CertificatePassword = certificatePassword
            };
        } else if (ParameterSetName == "CertificateSecretManagement") {
            cred = new GraphCredential {
                ClientId = ClientId!,
                DirectoryId = DirectoryId!,
                CertificatePath = CertificatePath!,
                CertificatePassword = certificatePassword
            };
        } else if (ParameterSetName == "CertificateBytes") {
            cred = new GraphCredential {
                ClientId = ClientId!,
                DirectoryId = DirectoryId!,
                CertificateBytes = CertificateBytes!,
                CertificatePassword = CertificatePassword!
            };
        } else if (ParameterSetName == "CertificateBytesSecureString") {
            cred = new GraphCredential {
                ClientId = ClientId!,
                DirectoryId = DirectoryId!,
                CertificateBytes = CertificateBytes!,
                CertificatePassword = certificatePassword
            };
        } else if (ParameterSetName == "CertificateBytesSecretManagement") {
            cred = new GraphCredential {
                ClientId = ClientId!,
                DirectoryId = DirectoryId!,
                CertificateBytes = CertificateBytes!,
                CertificatePassword = certificatePassword
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
            cred.AccessToken = oauth.AccessToken;
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
            cred.AccessToken = oauth.AccessToken;
        } else if (ParameterSetName == "OnBehalfOfSecureString") {
            cred = new GraphCredential {
                ClientId = ClientId!,
                ClientSecret = clientSecret,
                DirectoryId = DirectoryId!
            };
            oauth = await OAuthHelpers.AcquireO365TokenOnBehalfOfAsync(
                ClientId!,
                DirectoryId!,
                clientSecret,
                onBehalfOfToken,
                Scopes);
            cred.AccessToken = oauth.AccessToken;
        } else if (ParameterSetName == "OnBehalfOfSecretManagement") {
            cred = new GraphCredential {
                ClientId = ClientId!,
                ClientSecret = clientSecret,
                DirectoryId = DirectoryId!
            };
            oauth = await OAuthHelpers.AcquireO365TokenOnBehalfOfAsync(
                ClientId!,
                DirectoryId!,
                clientSecret,
                onBehalfOfToken,
                Scopes);
            cred.AccessToken = oauth.AccessToken;
        } else if (ParameterSetName == "PlainSecureString") {
            cred = new GraphCredential {
                ClientId = ClientId!,
                ClientSecret = clientSecret,
                DirectoryId = DirectoryId!
            };
        } else if (ParameterSetName == "PlainSecretManagement") {
            cred = new GraphCredential {
                ClientId = ClientId!,
                ClientSecret = clientSecret,
                DirectoryId = DirectoryId!
            };
        } else {
            cred = new GraphCredential {
                ClientId = ClientId!,
                ClientSecret = ClientSecret!,
                DirectoryId = DirectoryId!
            };
        }

        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        MicrosoftGraphUtils.MaxConcurrentRequests = MaxConcurrentRequests;

        bool connected = false;
        if (ParameterSetName == "DeviceCode" || ParameterSetName == "OnBehalfOf" || ParameterSetName == "OnBehalfOfSecureString" || ParameterSetName == "OnBehalfOfSecretManagement") {
            connected = !string.IsNullOrWhiteSpace(cred.AccessToken);
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

        var info = new GraphConnectionInfo { Credential = cred, IsConnected = connected, OAuthCredential = oauth };
        DefaultSessions.GraphSession = info;
        WriteObject(info);
    }
}
