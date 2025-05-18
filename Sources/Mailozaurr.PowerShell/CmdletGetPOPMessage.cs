using System.Management.Automation;
using System.Threading.Tasks;
using Mailozaurr.PowerShell;

namespace Mailozaurr.PowerShell;

[Cmdlet(VerbsCommon.Get, "POPMessage")]
[Alias("Get-POP3Message")]
public sealed class CmdletGetPOPMessage : AsyncPSCmdlet {
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public PopConnectionInfo Client { get; set; }

    [Parameter(Position = 1)]
    public int Index { get; set; }

    [Parameter(Position = 2)]
    public int Count { get; set; } = 1;

    [Parameter()]
    public SwitchParameter All { get; set; }

    protected override Task ProcessRecordAsync() {
        if (Client != null && Client.Data != null) {
            if (All.IsPresent) {
                var messages = Client.Data.GetMessages(Index, Count);
                WriteObject(messages, true);
            } else {
                if (Index < Client.Data.Count) {
                    var messages = Client.Data.GetMessages(Index, Count);
                    WriteObject(messages, true);
                } else {
                    WriteWarning($"Get-POP3Message - Index is out of range. Use index less than {Client.Data.Count}.");
                }
            }
        } else {
            WriteWarning("Get-POP3Message - Is POP3 connected?");
        }
        return Task.CompletedTask;
    }
}