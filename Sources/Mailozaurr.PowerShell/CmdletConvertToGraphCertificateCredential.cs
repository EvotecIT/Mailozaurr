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
public class CmdletConvertToGraphCertificateCredential : PSCmdlet {
    [Parameter(Mandatory = true)]
    public string? ClientId { get; set; }

    [Parameter(Mandatory = true)]
    public string? TenantId { get; set; }

    [Parameter(Mandatory = true)]
    public string? CertificatePath { get; set; }

    [Parameter(Mandatory = true)]
    public string? CertificatePassword { get; set; }

    [Parameter]
    public string[]? Scopes { get; set; }

    protected override void ProcessRecord() {
        GraphAuthorization token;
        try {
            token = Task.Run(() => Mailozaurr.OAuthHelpers.AcquireGraphCertificateTokenAsync(
                    ClientId!,
                    TenantId!,
                    CertificatePath!,
                    CertificatePassword!,
                    Scopes)).GetAwaiter().GetResult();
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
