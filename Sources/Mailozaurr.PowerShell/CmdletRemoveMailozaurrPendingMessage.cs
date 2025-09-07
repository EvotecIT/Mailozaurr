using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Removes pending messages from a file based repository.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "MailozaurrPendingMessage", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public sealed class CmdletRemoveMailozaurrPendingMessage : AsyncPSCmdlet {
    /// <summary>Identifier of the message to remove.</summary>
    [Parameter(Mandatory = true)]
    public string? MessageId { get; set; }

    /// <summary>Path to the pending message log file.</summary>
    [Parameter(Mandatory = true)]
    public string? PendingPath { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (!ShouldProcess(MessageId ?? string.Empty, "Removing pending message")) {
            return;
        }
        var repo = new FilePendingMessageRepository(PendingPath!);
        await repo.RemoveAsync(MessageId!, CancelToken);
    }
}
