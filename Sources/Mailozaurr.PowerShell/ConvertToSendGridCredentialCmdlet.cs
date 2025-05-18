namespace Mailozaurr.PowerShell;

using System.Management.Automation;
using System.Security;

[Cmdlet(VerbsData.ConvertTo, "SendGridCredential")]
[OutputType(typeof(PSCredential))]
public class ConvertToSendGridCredentialCmdlet : PSCmdlet {
    [Parameter(Mandatory = true)]
    public string ApiKey { get; set; }

    protected override void ProcessRecord() {
        var secret = CredentialHelpers.ToSecureString(ApiKey);
        var credential = new PSCredential("SendGrid", secret);
        WriteObject(credential);
    }
}