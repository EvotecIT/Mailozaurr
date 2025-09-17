using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Removes pending messages from a file based repository.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "EmailPendingMessage", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public sealed class CmdletRemoveEmailPendingMessage : AsyncPSCmdlet {
    /// <summary>Identifier of the message to remove.</summary>
    [Parameter(Mandatory = true)]
    public string? MessageId { get; set; }

    /// <summary>Directory containing pending message log file.</summary>
    [Parameter(Mandatory = true)]
    [Alias("PendingPath")]
    public string? PendingMessagesPath { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (!ShouldProcess(MessageId ?? string.Empty, "Removing pending message")) {
            return;
        }
        var options = new PendingMessageRepositoryOptions { DirectoryPath = PendingMessagesPath! };
        var repo = new FilePendingMessageRepository(options);
        await repo.RemoveAsync(MessageId!, CancelToken);
    }
}
