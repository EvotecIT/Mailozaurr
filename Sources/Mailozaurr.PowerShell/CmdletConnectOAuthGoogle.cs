using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Obtains an OAuth2 access token for a Google (Gmail) account for use with IMAP, SMTP, or other Google APIs.</para>
/// <para type="description">The <c>Connect-OAuthGoogle</c> cmdlet initiates an interactive OAuth2 authentication flow for a Gmail account, returning a <see cref="PSCredential"/> object containing the access token. This credential can be used with other cmdlets (such as <c>Connect-IMAP</c>) that support OAuth2 authentication.</para>
/// <example>
///   <summary>Obtain an OAuth2 credential for Gmail</summary>
///   <code>Connect-OAuthGoogle -GmailAccount "user@gmail.com" -ClientID "id" -ClientSecret "secret"</code>
/// </example>
/// <remarks>
/// The returned PSCredential contains the access token as the password. Use with IMAP/SMTP cmdlets that support OAuth2.
/// </remarks>
/// <seealso cref="CmdletConnectIMAP"/>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsCommunications.Connect, "OAuthGoogle")]
[OutputType(typeof(PSCredential))]
public class CmdletConnectOAuthGoogle : PSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the Gmail account (email address) to authenticate.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? GmailAccount { get; set; }

    /// <summary>
    /// <para type="description">Specifies the OAuth2 client ID from the Google Developer Console.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? ClientID { get; set; }

    /// <summary>
    /// <para type="description">Specifies the OAuth2 client secret from the Google Developer Console.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? ClientSecret { get; set; }

    /// <summary>
    /// <para type="description">Specifies the OAuth2 scopes to request. Default is "https://mail.google.com/".</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public string[]? Scope { get; set; } = new[] { "https://mail.google.com/" };

    /// <summary>
    /// Performs the interactive OAuth2 authentication and returns a PSCredential with the access token.
    /// </summary>
    protected override void ProcessRecord() {
        OAuthCredential? cred = null;
        try {
            cred = Task.Run(() => Mailozaurr.OAuthHelpers.AcquireGoogleTokenCachedAsync(GmailAccount, ClientID, ClientSecret, Scope)).GetAwaiter().GetResult();
        } catch (System.Exception ex) {
            WriteError(new ErrorRecord(ex, "OAuthGoogleAuthFailed", ErrorCategory.AuthenticationError, null));
            return;
        }
        if (cred != null) {
            var secure = CredentialHelpers.ToSecureString(cred.AccessToken);
            var psCred = new PSCredential(cred.UserName, secure);
            WriteObject(psCred);
        }
    }
}