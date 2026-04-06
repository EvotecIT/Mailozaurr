namespace Mailozaurr.PowerShell;

using System.Management.Automation;
using System.Security;

/// <summary>
/// <para type="synopsis">Creates a PSCredential object for OAuth2 authentication from a username and token.</para>
/// <para type="description">The <c>ConvertTo-OAuth2Credential</c> cmdlet creates a <see cref="PSCredential"/> object suitable for OAuth2 authentication, using the provided username and access token. The resulting credential can be used with cmdlets that require OAuth2 authentication (such as IMAP, SMTP, or POP3 with OAuth2).</para>
/// <para type="description">For automation and interactive use, prefer <c>-TokenSecureString</c> or <c>-SecretName</c> over passing the token as plain text.</para>
/// <example>
///   <summary>Create an OAuth2 credential from a SecureString token</summary>
///   <code>$token = Read-Host "OAuth token" -AsSecureString
/// ConvertTo-OAuth2Credential -UserName "user@example.com" -TokenSecureString $token</code>
/// </example>
/// <example>
///   <summary>Create an OAuth2 credential from a vault secret</summary>
///   <code>ConvertTo-OAuth2Credential -UserName "user@example.com" -SecretName "gmail-access-token" -VaultName "MailSecrets"</code>
/// </example>
/// <remarks>
/// Use this cmdlet to prepare credentials for use with OAuth2-enabled cmdlets in automation scenarios. The vault example requires a <c>Get-Secret</c> implementation such as Microsoft.PowerShell.SecretManagement.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsData.ConvertTo, "OAuth2Credential", DefaultParameterSetName = "PlainText")]
[OutputType(typeof(PSCredential))]
public class CmdletConvertToOAuth2Credential : PSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the username for OAuth2 authentication.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserName { get; set; }
    /// <summary>
    /// <para type="description">Specifies the OAuth2 access token.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "PlainText")]
    [ValidateNotNullOrEmpty]
    public string? Token { get; set; }

    /// <summary>
    /// <para type="description">Specifies the OAuth2 access token as a SecureString.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "SecureString")]
    [ValidateNotNull]
    public SecureString? TokenSecureString { get; set; }

    /// <summary>
    /// <para type="description">Specifies the name of a secret to resolve using Get-Secret.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "SecretManagement")]
    [ValidateNotNullOrEmpty]
    public string? SecretName { get; set; }

    /// <summary>
    /// <para type="description">Optional vault name used with Get-Secret.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "SecretManagement")]
    public string? VaultName { get; set; }

    /// <summary>
    /// Creates a PSCredential object for OAuth2 authentication.
    /// </summary>
    protected override void ProcessRecord() {
        var secret = this.ParameterSetName switch {
            "SecureString" => TokenSecureString!,
            "SecretManagement" => CredentialHelpers.ResolveSecretFromVault(SecretName!, VaultName),
            _ => CredentialHelpers.ToSecureString(Token)
        };
        var credential = new PSCredential(UserName, secret);
        WriteObject(credential);
    }
}
