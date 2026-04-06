namespace Mailozaurr.PowerShell;

using System.Management.Automation;
using System.Security;

/// <summary>
/// <para type="synopsis">Creates a PSCredential object for SendGrid API authentication from an API key.</para>
/// <para type="description">The <c>ConvertTo-SendGridCredential</c> cmdlet creates a <see cref="PSCredential"/> object suitable for SendGrid API authentication, using the provided API key. The resulting credential can be used with cmdlets that require SendGrid authentication (such as <c>Send-EmailMessage</c> with the <c>-SendGrid</c> switch).</para>
/// <para type="description">For new scripts, prefer <c>-ApiKeySecureString</c> or <c>-SecretName</c> instead of passing the API key as plain text.</para>
/// <example>
///   <summary>Create a SendGrid credential from a SecureString API key</summary>
///   <code>$apiKey = Read-Host "SendGrid API key" -AsSecureString
/// ConvertTo-SendGridCredential -ApiKeySecureString $apiKey</code>
/// </example>
/// <example>
///   <summary>Create a SendGrid credential from a vault secret</summary>
///   <code>ConvertTo-SendGridCredential -SecretName "sendgrid-api-key" -VaultName "MailSecrets"</code>
/// </example>
/// <remarks>
/// Use this cmdlet to prepare credentials for use with SendGrid-enabled cmdlets in automation scenarios. The vault example requires a <c>Get-Secret</c> implementation such as Microsoft.PowerShell.SecretManagement.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsData.ConvertTo, "SendGridCredential", DefaultParameterSetName = "PlainText")]
[OutputType(typeof(PSCredential))]
public class CmdletConvertToSendGridCredential : PSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the SendGrid API key.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "PlainText")]
    [ValidateNotNullOrEmpty]
    public string? ApiKey { get; set; }

    /// <summary>
    /// <para type="description">Specifies the SendGrid API key as a SecureString.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "SecureString")]
    [ValidateNotNull]
    public SecureString? ApiKeySecureString { get; set; }

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
    /// Creates a PSCredential object for SendGrid API authentication.
    /// </summary>
    protected override void ProcessRecord() {
        var secret = this.ParameterSetName == "SecureString"
            ? ApiKeySecureString!
            : this.ParameterSetName == "SecretManagement"
                ? CredentialHelpers.ResolveSecretFromVault(SecretName!, VaultName)
                : CredentialHelpers.ToSecureString(ApiKey);
        var credential = new PSCredential("SendGrid", secret);
        WriteObject(credential);
    }
}
