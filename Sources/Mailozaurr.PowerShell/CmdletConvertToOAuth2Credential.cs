namespace Mailozaurr.PowerShell;

using System.Management.Automation;
using System.Security;

/// <summary>
/// <para type="synopsis">Creates a PSCredential object for OAuth2 authentication from a username and token.</para>
/// <para type="description">The <c>ConvertTo-OAuth2Credential</c> cmdlet creates a <see cref="PSCredential"/> object suitable for OAuth2 authentication, using the provided username and access token. The resulting credential can be used with cmdlets that require OAuth2 authentication (such as IMAP, SMTP, or POP3 with OAuth2).</para>
/// <example>
///   <summary>Create an OAuth2 credential</summary>
///   <code>ConvertTo-OAuth2Credential -UserName "user@example.com" -Token "ya29.a0Af..."</code>
/// </example>
/// <remarks>
/// Use this cmdlet to prepare credentials for use with OAuth2-enabled cmdlets in automation scenarios.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsData.ConvertTo, "OAuth2Credential")]
[OutputType(typeof(PSCredential))]
public class CmdletConvertToOAuth2Credential : PSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the username for OAuth2 authentication.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    public string? UserName { get; set; }
    /// <summary>
    /// <para type="description">Specifies the OAuth2 access token.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    public string? Token { get; set; }

    /// <summary>
    /// Creates a PSCredential object for OAuth2 authentication.
    /// </summary>
    protected override void ProcessRecord() {
        var secret = CredentialHelpers.ToSecureString(Token);
        var credential = new PSCredential(UserName, secret);
        WriteObject(credential);
    }
}