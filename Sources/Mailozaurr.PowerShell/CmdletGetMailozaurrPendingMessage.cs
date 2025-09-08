using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Retrieves pending email messages from a file based repository.
/// </summary>
[Cmdlet(VerbsCommon.Get, "MailozaurrPendingMessage")]
[OutputType(typeof(PendingMessageRecord))]
public sealed class CmdletGetMailozaurrPendingMessage : AsyncPSCmdlet {
    /// <summary>Path to the pending message log file.</summary>
    [Parameter(Mandatory = true)]
    public string? PendingMessagesPath { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        var repo = new FilePendingMessageRepository(PendingMessagesPath!);
        await foreach (var record in repo.GetAllAsync(CancelToken)) {
            WriteObject(record);
        }
    }
}
