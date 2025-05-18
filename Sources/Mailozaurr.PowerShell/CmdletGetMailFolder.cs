using System.Management.Automation;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

[Cmdlet(VerbsCommon.Get, "MailFolder")]
[OutputType(typeof(object))]
public class CmdletGetMailFolder : PSCmdlet {
    [Parameter(Mandatory = true)]
    public string UserPrincipalName { get; set; }
    [Parameter]
    public string ClientId { get; set; }
    [Parameter]
    public string ClientSecret { get; set; }
    [Parameter]
    public string DirectoryId { get; set; }

    protected override void ProcessRecord() {
        var cred = new GraphCredential { ClientId = ClientId, ClientSecret = ClientSecret, DirectoryId = DirectoryId };
        var task = MicrosoftGraphUtils.GetMailFoldersAsync(cred, UserPrincipalName);
        task.Wait();
        foreach (var folder in task.Result) {
            WriteObject(folder);
        }
    }
}
