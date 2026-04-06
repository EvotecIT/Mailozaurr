namespace Mailozaurr.PowerShell;

using System.Management.Automation;
using System.Security;
using System.Threading.Tasks;

/// <summary>
/// <para type="synopsis">Creates a PSCredential containing a Microsoft Graph access token obtained using certificate authentication.</para>
/// <para type="description">The <c>ConvertTo-GraphCertificateCredential</c> cmdlet authenticates with Microsoft Graph using a client certificate and returns a <see cref="PSCredential"/> with the access token. Use the resulting credential with cmdlets that accept Graph tokens.</para>
/// <para type="description">For new scripts, prefer <c>-CertificatePasswordSecureString</c> or <c>-SecretName</c> rather than passing the certificate password as plain text.</para>
/// <example>
///   <summary>Acquire a token using a certificate and SecureString password</summary>
///   <code>$certificatePassword = Read-Host "Certificate password" -AsSecureString
/// ConvertTo-GraphCertificateCredential -ClientId "id" -TenantId "tenant" -CertificatePath "cert.pfx" -CertificatePasswordSecureString $certificatePassword</code>
/// </example>
/// <example>
///   <summary>Acquire a token using a certificate password from a vault secret</summary>
///   <code>ConvertTo-GraphCertificateCredential -ClientId "id" -TenantId "tenant" -CertificatePath "cert.pfx" -SecretName "graph-certificate-password" -VaultName "MailSecrets"</code>
/// </example>
/// <remarks>
/// This cmdlet simplifies obtaining an app-only token for Microsoft Graph when using certificate authentication. The vault example requires a <c>Get-Secret</c> implementation such as Microsoft.PowerShell.SecretManagement.
/// </remarks>
/// </summary>
[Cmdlet(VerbsData.ConvertTo, "GraphCertificateCredential", DefaultParameterSetName = "PlainText")]
[OutputType(typeof(PSCredential))]
public class CmdletConvertToGraphCertificateCredential : AsyncPSCmdlet {
    /// <summary>
    /// Azure AD application (client) identifier.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? ClientId { get; set; }

    /// <summary>
    /// Azure AD tenant identifier.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? TenantId { get; set; }

    /// <summary>
    /// Path to the client certificate (PFX).
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Password for the client certificate.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "PlainText")]
    [ValidateNotNullOrEmpty]
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Password for the client certificate as a SecureString.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "SecureString")]
    [ValidateNotNull]
    public SecureString? CertificatePasswordSecureString { get; set; }

    /// <summary>
    /// Name of the certificate password secret resolved via Get-Secret.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "SecretManagement")]
    [ValidateNotNullOrEmpty]
    public string? SecretName { get; set; }

    /// <summary>
    /// Optional vault name used with Get-Secret.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "SecretManagement")]
    public string? VaultName { get; set; }

    /// <summary>
    /// Optional scopes to request.
    /// </summary>
    [Parameter]
    public string[]? Scopes { get; set; }

    /// <summary>
    /// Acquires an app-only access token using certificate authentication and returns it as a credential.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        var certificatePassword = this.ParameterSetName == "SecureString"
            ? CredentialHelpers.ToPlainText(CertificatePasswordSecureString)
            : this.ParameterSetName == "SecretManagement"
                ? CredentialHelpers.ToPlainText(CredentialHelpers.ResolveSecretFromVault(SecretName!, VaultName))
            : CertificatePassword;

        GraphAuthorization token;
        try {
            token = await Mailozaurr.OAuthHelpers.AcquireGraphCertificateTokenAsync(
                    ClientId!,
                    TenantId!,
                    CertificatePath!,
                    certificatePassword!,
                    Scopes);
        } catch (System.Exception ex) {
            WriteError(new ErrorRecord(ex, "GraphCertificateAuthFailed", ErrorCategory.AuthenticationError, null));
            return;
        }
        SecureString secure = new();
        foreach (var ch in token.AccessToken) secure.AppendChar(ch);
        var username = $"{ClientId}@{TenantId}";
        var credential = new PSCredential(username, secure);
        WriteObject(credential);
    }
}
