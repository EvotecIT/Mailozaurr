namespace Mailozaurr.PowerShell;

using System.Management.Automation;
using System.Security;
using System.Threading.Tasks;

/// <summary>
/// <para type="synopsis">Creates a PSCredential containing a Microsoft Graph access token obtained using certificate authentication.</para>
/// <para type="description">The <c>ConvertTo-GraphCertificateCredential</c> cmdlet authenticates with Microsoft Graph using a client certificate and returns a <see cref="PSCredential"/> with the access token. Use the resulting credential with cmdlets that accept Graph tokens.</para>
/// <example>
///   <summary>Acquire a token using a certificate</summary>
///   <code>ConvertTo-GraphCertificateCredential -ClientId "id" -TenantId "tenant" -CertificatePath "cert.pfx" -CertificatePassword "pass"</code>
/// </example>
/// <remarks>
/// This cmdlet simplifies obtaining an app-only token for Microsoft Graph when using certificate authentication.
/// </remarks>
/// </summary>
[Cmdlet(VerbsData.ConvertTo, "GraphCertificateCredential")]
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
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Optional scopes to request.
    /// </summary>
    [Parameter]
    public string[]? Scopes { get; set; }

    /// <summary>
    /// Acquires an app-only access token using certificate authentication and returns it as a credential.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        GraphAuthorization token;
        try {
            token = await Mailozaurr.OAuthHelpers.AcquireGraphCertificateTokenAsync(
                    ClientId!,
                    TenantId!,
                    CertificatePath!,
                    CertificatePassword!,
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
