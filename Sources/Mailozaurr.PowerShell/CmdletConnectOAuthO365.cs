using System.Management.Automation;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Obtains an OAuth2 access token for an Office 365 (Microsoft 365) account for use with IMAP, SMTP, or Microsoft Graph.</para>
/// <para type="description">The <c>Connect-OAuthO365</c> cmdlet initiates an interactive OAuth2 authentication flow for an Office 365 account, returning a <see cref="PSCredential"/> object containing the access token. This credential can be used with other cmdlets (such as <c>Connect-IMAP</c> or <c>Send-EmailMessage</c>) that support OAuth2 authentication.</para>
/// <example>
///   <summary>Obtain an OAuth2 credential for Office 365</summary>
///   <code>Connect-OAuthO365 -Login "user@tenant.onmicrosoft.com" -ClientID "id" -TenantID "tenant"</code>
/// </example>
/// <remarks>
/// The returned PSCredential contains the access token as the password. Use with IMAP/SMTP/Graph cmdlets that support OAuth2.
/// </remarks>
/// <seealso cref="CmdletConnectIMAP"/>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsCommunications.Connect, "OAuthO365")]
[OutputType(typeof(PSCredential))]
public class CmdletConnectOAuthO365 : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the login (user principal name) for the Office 365 account. Optional; if not provided, interactive login is used.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public string? Login { get; set; }

    /// <summary>
    /// <para type="description">Specifies the OAuth2 client ID from Azure AD App Registration.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? ClientID { get; set; }

    /// <summary>
    /// <para type="description">Specifies the Azure AD tenant ID (Directory ID).</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? TenantID { get; set; }

    /// <summary>
    /// <para type="description">Specifies the redirect URI for the OAuth2 flow. Default is the recommended Microsoft URI.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public string? RedirectUri { get; set; } = "https://login.microsoftonline.com/common/oauth2/nativeclient";

    /// <summary>
    /// <para type="description">Specifies the OAuth2 scopes to request. Default includes IMAP, POP, and SMTP permissions.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public string[]? Scopes { get; set; } = new[] {
        "email",
        "offline_access",
        "https://outlook.office.com/IMAP.AccessAsUser.All",
        "https://outlook.office.com/POP.AccessAsUser.All",
        "https://outlook.office.com/SMTP.Send"
    };

    /// <summary>
    /// Performs the interactive OAuth2 authentication and returns a PSCredential with the access token.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        if (ClientID is null || TenantID is null || RedirectUri is null || Scopes is null) {
            WriteError(new ErrorRecord(new PSArgumentNullException("ClientID"), "OAuthO365InvalidParameters", ErrorCategory.InvalidArgument, null));
            return;
        }

        Mailozaurr.OAuthCredential? cred = null;
        try {
            cred = await Mailozaurr.OAuthHelpers.AcquireO365TokenCachedAsync(Login, ClientID, TenantID, RedirectUri, Scopes);
        } catch (System.Exception ex) {
            WriteError(new ErrorRecord(ex, "OAuthO365AuthFailed", ErrorCategory.AuthenticationError, null));
            return;
        }

        if (cred != null) {
            var secure = CredentialHelpers.ToSecureString(cred.AccessToken);
            var psCred = new PSCredential(cred.UserName, secure);
            WriteObject(psCred);
        }
    }
}