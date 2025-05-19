namespace Mailozaurr.PowerShell;

using System.Management.Automation;
using System.Security;

/// <summary>
/// <para type="synopsis">Creates a PSCredential object for Microsoft Graph authentication from client ID, secret, and directory ID.</para>
/// <para type="description">The <c>ConvertTo-GraphCredential</c> cmdlet creates a <see cref="PSCredential"/> object suitable for Microsoft Graph authentication, using the provided client ID, client secret (clear or encrypted), and directory (tenant) ID. The resulting credential can be used with cmdlets that require Graph authentication.</para>
/// <example>
///   <summary>Create a Graph credential from clear text secret</summary>
///   <code>ConvertTo-GraphCredential -ClientId "id" -ClientSecret "secret" -DirectoryId "tenant"</code>
/// </example>
/// <example>
///   <summary>Create a Graph credential from encrypted secret</summary>
///   <code>ConvertTo-GraphCredential -ClientId "id" -ClientSecretEncrypted "..." -DirectoryId "tenant"</code>
/// </example>
/// <remarks>
/// Use this cmdlet to prepare credentials for use with Microsoft Graph cmdlets in automation scenarios.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsData.ConvertTo, "GraphCredential")]
[OutputType(typeof(PSCredential))]
public class CmdletConvertToGraphCredential: PSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the client ID for Microsoft Graph authentication.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "ClearText")]
    [Parameter(Mandatory = true, ParameterSetName = "Encrypted")]
    public string? ClientId { get; set; }

    /// <summary>
    /// <para type="description">Specifies the client secret in clear text. Use only with the ClearText parameter set.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "ClearText")]
    public string? ClientSecret { get; set; }

    /// <summary>
    /// <para type="description">Specifies the client secret in encrypted form. Use only with the Encrypted parameter set.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Encrypted")]
    public string? ClientSecretEncrypted { get; set; }

    /// <summary>
    /// <para type="description">Specifies the directory (tenant) ID for Microsoft Graph authentication.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "ClearText")]
    [Parameter(Mandatory = true, ParameterSetName = "Encrypted")]
    public string? DirectoryId { get; set; }

    /// <summary>
    /// Creates a PSCredential object for Microsoft Graph authentication.
    /// </summary>
    protected override void ProcessRecord() {
        SecureString secret = this.ParameterSetName == "Encrypted"
            ? CredentialHelpers.ToSecureString(ClientSecretEncrypted)
            : CredentialHelpers.ToSecureString(ClientSecret);
        var username = $"{ClientId}@{DirectoryId}";
        var credential = new PSCredential(username, secret);
        WriteObject(credential);
    }
}