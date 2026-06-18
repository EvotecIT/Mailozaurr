namespace Mailozaurr.PowerShell;

using System.Management.Automation;
using System.Security;

/// <summary>
/// <para type="synopsis">Creates a PSCredential object for Microsoft Graph authentication from client ID, secret, and directory ID.</para>
/// <para type="description">The <c>ConvertTo-GraphCredential</c> cmdlet creates a <see cref="PSCredential"/> object suitable for Microsoft Graph authentication, using the provided client ID, client secret, and directory (tenant) ID. The resulting credential can be used with cmdlets that require Graph authentication.</para>
/// <para type="description">For new scripts, prefer <c>-ClientSecretSecureString</c> or <c>-SecretName</c> instead of passing the client secret in plain text.</para>
/// <example>
///   <summary>Create a Graph credential from a SecureString secret</summary>
///   <code>$secret = Read-Host "Graph client secret" -AsSecureString
/// ConvertTo-GraphCredential -ClientId "id" -ClientSecretSecureString $secret -DirectoryId "tenant"</code>
/// </example>
/// <example>
///   <summary>Create a Graph credential from a vault secret</summary>
///   <code>ConvertTo-GraphCredential -ClientId "id" -SecretName "graph-client-secret" -VaultName "MailSecrets" -DirectoryId "tenant"</code>
/// </example>
/// <remarks>
/// Use this cmdlet to prepare credentials for use with Microsoft Graph cmdlets in automation scenarios. The vault example requires a <c>Get-Secret</c> implementation such as Microsoft.PowerShell.SecretManagement.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsData.ConvertTo, "GraphCredential", DefaultParameterSetName = "ClearText")]
[OutputType(typeof(PSCredential))]
public class CmdletConvertToGraphCredential : PSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the client ID for Microsoft Graph authentication.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "ClearText")]
    [Parameter(Mandatory = true, ParameterSetName = "Encrypted")]
    [Parameter(Mandatory = true, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "SecretManagement")]
    [ValidateNotNullOrEmpty]
    public string? ClientId { get; set; }

    /// <summary>
    /// <para type="description">Specifies the client secret in clear text. Use only with the ClearText parameter set.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "ClearText")]
    [ValidateNotNullOrEmpty]
    public string? ClientSecret { get; set; }

    /// <summary>
    /// <para type="description">Specifies the client secret in encrypted form. Use only with the Encrypted parameter set.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Encrypted")]
    [ValidateNotNullOrEmpty]
    public string? ClientSecretEncrypted { get; set; }

    /// <summary>
    /// <para type="description">Specifies the client secret as a SecureString. Use only with the SecureString parameter set.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "SecureString")]
    [ValidateNotNull]
    public SecureString? ClientSecretSecureString { get; set; }

    /// <summary>
    /// <para type="description">Specifies the name of a secret to resolve using Get-Secret. Use only with the SecretManagement parameter set.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "SecretManagement")]
    [ValidateNotNullOrEmpty]
    public string? SecretName { get; set; }

    /// <summary>
    /// <para type="description">Optional vault name used with Get-Secret. Use only with the SecretManagement parameter set.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "SecretManagement")]
    public string? VaultName { get; set; }

    /// <summary>
    /// <para type="description">Specifies the directory (tenant) ID for Microsoft Graph authentication.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "ClearText")]
    [Parameter(Mandatory = true, ParameterSetName = "Encrypted")]
    [Parameter(Mandatory = true, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "SecretManagement")]
    [ValidateNotNullOrEmpty]
    public string? DirectoryId { get; set; }

    /// <summary>
    /// Creates a PSCredential object for Microsoft Graph authentication.
    /// </summary>
    protected override void ProcessRecord() {
        SecureString secret = this.ParameterSetName == "Encrypted"
            ? CredentialHelpers.ToSecureStringFromEncryptedString(ClientSecretEncrypted)
            : this.ParameterSetName == "SecureString"
                ? ClientSecretSecureString!
                : this.ParameterSetName == "SecretManagement"
                    ? CredentialHelpers.ResolveSecretFromVault(SecretName!, VaultName)
                : CredentialHelpers.ToSecureString(ClientSecret);
        var username = $"{ClientId}@{DirectoryId}";
        var credential = new PSCredential(username, secret);
        WriteObject(credential);
    }
}
