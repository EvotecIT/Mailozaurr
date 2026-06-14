namespace Mailozaurr.PowerShell;

using System.Management.Automation;

/// <summary>
/// <para type="synopsis">Extracts username and token from an OAuth2 PSCredential.</para>
/// <para type="description">The <c>ConvertFrom-OAuth2Credential</c> cmdlet converts a <see cref="PSCredential"/> containing an OAuth2 access token into a PSCustomObject with <c>UserName</c> and <c>Token</c> properties.</para>
/// <example>
///   <summary>Convert credential back to plain token</summary>
///   <code>$info = ConvertFrom-OAuth2Credential -Credential $OAuthCred</code>
/// </example>
/// </summary>
[Cmdlet(VerbsData.ConvertFrom, "OAuth2Credential")]
[OutputType(typeof(PSObject))]
public class CmdletConvertFromOAuth2Credential : PSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the PSCredential containing the OAuth2 token.</para>
    /// </summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    public PSCredential? Credential { get; set; }

    /// <summary>
    /// Processes the record by extracting the user name and token from the credential.
    /// </summary>
    protected override void ProcessRecord() {
        if (Credential is null) {
            return;
        }
        var network = Credential.GetNetworkCredential();
        var (userName, token) = Helpers.ConvertFromOAuth2Credential(network);
        var obj = new PSObject();
        obj.Properties.Add(new PSNoteProperty("UserName", userName));
        obj.Properties.Add(new PSNoteProperty("Token", token));
        WriteObject(obj);
    }
}