namespace Mailozaurr.PowerShell;

using System.Management.Automation;
using System.Security;

/// <summary>
/// <para type="synopsis">Creates a PSCredential object for Mailgun API authentication from an API key.</para>
/// <para type="description">Use <c>ConvertTo-MailgunCredential</c> to generate a <see cref="PSCredential"/> that can be passed to <c>Send-EmailMessage</c> when using the Mailgun provider.</para>
/// <para type="description">For new scripts, prefer <c>-ApiKeySecureString</c> or <c>-SecretName</c> instead of passing the API key as plain text.</para>
/// <example>
///   <summary>Create a Mailgun credential from a SecureString API key</summary>
///   <code>$apiKey = Read-Host "Mailgun API key" -AsSecureString
/// ConvertTo-MailgunCredential -ApiKeySecureString $apiKey</code>
/// </example>
/// <example>
///   <summary>Create a Mailgun credential from a vault secret</summary>
///   <code>ConvertTo-MailgunCredential -SecretName "mailgun-api-key" -VaultName "MailSecrets"</code>
/// </example>
/// </summary>
[Cmdlet(VerbsData.ConvertTo, "MailgunCredential", DefaultParameterSetName = "PlainText")]
[OutputType(typeof(PSCredential))]
public class CmdletConvertToMailgunCredential : PSCmdlet {
    /// <summary>
    /// Mailgun API key used for authentication.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "PlainText")]
    [ValidateNotNullOrEmpty]
    public string? ApiKey { get; set; }

    /// <summary>
    /// Mailgun API key used for authentication as a SecureString.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "SecureString")]
    [ValidateNotNull]
    public SecureString? ApiKeySecureString { get; set; }

    /// <summary>
    /// Mailgun secret name used with Get-Secret.
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
    /// Converts the provided API key into a PSCredential for Mailgun.
    /// </summary>
    protected override void ProcessRecord() {
        SecureString secret = this.ParameterSetName == "SecureString"
            ? ApiKeySecureString!
            : this.ParameterSetName == "SecretManagement"
                ? CredentialHelpers.ResolveSecretFromVault(SecretName!, VaultName)
            : CredentialHelpers.ToSecureString(ApiKey);
        var credential = new PSCredential("Mailgun", secret);
        WriteObject(credential);
    }
}