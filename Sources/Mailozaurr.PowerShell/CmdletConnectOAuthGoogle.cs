using System.Management.Automation;
using System.Security;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Obtains an OAuth2 access token for a Google (Gmail) account for use with IMAP, SMTP, or other Google APIs.</para>
/// <para type="description">The <c>Connect-OAuthGoogle</c> cmdlet initiates an interactive OAuth2 authentication flow for a Gmail account, returning a <see cref="PSCredential"/> object containing the access token. This credential can be used with other cmdlets (such as <c>Connect-IMAP</c>) that support OAuth2 authentication.</para>
/// <para type="description">For new scripts, prefer <c>-ClientSecretSecureString</c> or <c>-ClientSecretSecretName</c> rather than passing the client secret as plain text.</para>
/// <example>
///   <summary>Obtain an OAuth2 credential for Gmail using a SecureString secret</summary>
///   <code>$clientSecret = Read-Host "Google client secret" -AsSecureString
/// Connect-OAuthGoogle -GmailAccount "user@gmail.com" -ClientID "id" -ClientSecretSecureString $clientSecret</code>
/// </example>
/// <example>
///   <summary>Obtain an OAuth2 credential for Gmail using a vault secret</summary>
///   <code>Connect-OAuthGoogle -GmailAccount "user@gmail.com" -ClientID "id" -ClientSecretSecretName "gmail-client-secret" -ClientSecretVaultName "MailSecrets"</code>
/// </example>
/// <remarks>
/// The returned PSCredential contains the access token as the password. Use with IMAP/SMTP cmdlets that support OAuth2. The vault example requires a <c>Get-Secret</c> implementation such as Microsoft.PowerShell.SecretManagement.
/// </remarks>
/// <seealso cref="CmdletConnectIMAP"/>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsCommunications.Connect, "OAuthGoogle", DefaultParameterSetName = "PlainText")]
[OutputType(typeof(PSCredential))]
public sealed class CmdletConnectOAuthGoogle : AsyncPSCmdlet {
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
    [Parameter(Mandatory = true, ParameterSetName = "PlainText")]
    [ValidateNotNullOrEmpty]
    public string? ClientSecret { get; set; }

    /// <summary>
    /// <para type="description">Specifies the OAuth2 client secret from the Google Developer Console as a SecureString.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "SecureString")]
    [ValidateNotNull]
    public SecureString? ClientSecretSecureString { get; set; }

    /// <summary>
    /// <para type="description">Specifies the name of a secret to resolve using Get-Secret for the OAuth2 client secret.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "SecretManagement")]
    [ValidateNotNullOrEmpty]
    public string? ClientSecretSecretName { get; set; }

    /// <summary>
    /// <para type="description">Optional vault name used with Get-Secret for the OAuth2 client secret.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "SecretManagement")]
    public string? ClientSecretVaultName { get; set; }

    /// <summary>
    /// <para type="description">Specifies the OAuth2 scopes to request. Default is "https://mail.google.com/".</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public string[]? Scope { get; set; } = new[] { "https://mail.google.com/" };

    /// <summary>
    /// Performs the interactive OAuth2 authentication and returns a PSCredential with the access token.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        var clientSecret = this.ParameterSetName == "SecureString"
            ? CredentialHelpers.ToPlainText(ClientSecretSecureString)
            : this.ParameterSetName == "SecretManagement"
                ? CredentialHelpers.ToPlainText(CredentialHelpers.ResolveSecretFromVault(ClientSecretSecretName!, ClientSecretVaultName))
                : ClientSecret;

        if (GmailAccount is null || ClientID is null || string.IsNullOrWhiteSpace(clientSecret) || Scope is null) {
            WriteError(new ErrorRecord(new PSArgumentNullException("GmailAccount"), "OAuthGoogleInvalidParameters", ErrorCategory.InvalidArgument, null));
            return;
        }

        OAuthCredential? cred = null;
        try {
            cred = await Mailozaurr.OAuthHelpers
                .AcquireGoogleTokenCachedAsync(GmailAccount, ClientID, clientSecret!, Scope);
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
