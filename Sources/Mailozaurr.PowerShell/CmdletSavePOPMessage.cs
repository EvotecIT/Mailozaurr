using System.Management.Automation;
using System.Threading.Tasks;
using Mailozaurr.PowerShell;

namespace Mailozaurr.PowerShell;

[Cmdlet(VerbsData.Save, "POPMessage")]
[Alias("Save-POP3Message")]
public sealed class CmdletSavePOPMessage : AsyncPSCmdlet {
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public PopConnectionInfo Client { get; set; }

    [Parameter(Mandatory = true, Position = 1)]
    public int Index { get; set; }

    [Parameter(Mandatory = true, Position = 2)]
    public string Path { get; set; }

    protected override Task ProcessRecordAsync() {
        if (Client != null && Client.Data != null) {
            if (Index < Client.Data.Count) {
                var message = Client.Data.GetMessage(Index);
                message.WriteTo(Path);
            } else {
                WriteWarning($"Save-POP3Message - Index is out of range. Use index less than {Client.Data.Count}.");
            }
        } else {
            WriteWarning("Save-POP3Message - Is POP3 connected?");
        }
        return Task.CompletedTask;
    }
}