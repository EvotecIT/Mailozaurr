namespace Mailozaurr.PowerShell;

using System.Management.Automation;
using System.Security;

[Cmdlet(VerbsData.ConvertTo, "OAuth2Credential")]
[OutputType(typeof(PSCredential))]
public class ConvertToOAuth2CredentialCmdlet : PSCmdlet {
    [Parameter(Mandatory = true)]
    public string UserName { get; set; }
    [Parameter(Mandatory = true)]
    public string Token { get; set; }

    protected override void ProcessRecord() {
        var secret = CredentialHelpers.ToSecureString(Token);
        var credential = new PSCredential(UserName, secret);
        WriteObject(credential);
    }
}