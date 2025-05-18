using System.Management.Automation;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

[Cmdlet(VerbsData.Save, "MailMessage")]
public class CmdletSaveMailMessage : PSCmdlet {
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    public GraphEmailMessage[] Message { get; set; }
    [Parameter(Mandatory = true)]
    public string Path { get; set; }

    protected override void ProcessRecord() {
        MicrosoftGraphUtils.SaveMailMessages(Message, Path);
    }
}
