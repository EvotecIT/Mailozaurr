using System.Management.Automation;
using System.Threading.Tasks;
using MailKit;
using Mailozaurr.PowerShell;

namespace Mailozaurr.PowerShell;

[Cmdlet(VerbsCommon.Get, "IMAPFolder")]
public sealed class CmdletGetIMAPFolder : AsyncPSCmdlet {
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public ImapConnectionInfo Client { get; set; }

    [Parameter(Position = 1)]
    public FolderAccess FolderAccess { get; set; } = FolderAccess.ReadOnly;

    protected override Task ProcessRecordAsync() {
        if (Client != null) {
            var folder = Client.Data.Inbox;
            folder.Open(FolderAccess);
            WriteVerbose($"Get-IMAPMessage - Total messages {folder.Count}, Recent messages {folder.Recent}");
            Client.Messages = folder;
            Client.Count = folder.Count;
            Client.Recent = folder.Recent;
            WriteObject(Client);
        } else {
            WriteVerbose("Get-IMAPMessage - Client not connected?");
        }
        return Task.CompletedTask;
    }
}