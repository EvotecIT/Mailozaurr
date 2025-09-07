using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Sends pending messages stored in a file based repository.
/// </summary>
[Cmdlet(VerbsCommunications.Send, "MailozaurrPendingMessage")]
public sealed class CmdletSendMailozaurrPendingMessage : AsyncPSCmdlet {
    /// <summary>Path to the pending message log file.</summary>
    [Parameter(Mandatory = true)]
    public string? PendingPath { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        var smtp = new Smtp { PendingMessageRepository = new FilePendingMessageRepository(PendingPath!) };
        await smtp.ProcessPendingMessagesAsync(CancelToken);
    }
}
