namespace Mailozaurr.PowerShell;

using System.Management.Automation;
using System.Security;

[Cmdlet(VerbsData.ConvertTo, "GraphCredential")]
[OutputType(typeof(PSCredential))]
public class ConvertToGraphCredentialCmdlet : PSCmdlet {
    [Parameter(Mandatory = true, ParameterSetName = "ClearText")]
    [Parameter(Mandatory = true, ParameterSetName = "Encrypted")]
    public string ClientId { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "ClearText")]
    public string ClientSecret { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Encrypted")]
    public string ClientSecretEncrypted { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "ClearText")]
    [Parameter(Mandatory = true, ParameterSetName = "Encrypted")]
    public string DirectoryId { get; set; }

    protected override void ProcessRecord() {
        SecureString secret = this.ParameterSetName == "Encrypted"
            ? CredentialHelpers.ToSecureString(ClientSecretEncrypted)
            : CredentialHelpers.ToSecureString(ClientSecret);
        var username = $"{ClientId}@{DirectoryId}";
        var credential = new PSCredential(username, secret);
        WriteObject(credential);
    }
}