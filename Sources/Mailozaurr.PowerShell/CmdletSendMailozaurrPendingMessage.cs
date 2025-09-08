using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Sends pending messages stored in a file based repository.
/// </summary>
[Cmdlet(VerbsCommunications.Send, "MailozaurrPendingMessage")]
public sealed class CmdletSendMailozaurrPendingMessage : AsyncPSCmdlet {
    /// <summary>Directory containing pending message log file.</summary>
    [Parameter(Mandatory = true)]
    [Alias("PendingPath")]
    public string? PendingMessagesPath { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        var options = new PendingMessageRepositoryOptions { DirectoryPath = PendingMessagesPath! };
        var smtp = new Smtp { PendingMessageRepository = new FilePendingMessageRepository(options) };
        await smtp.ProcessPendingMessagesAsync(CancelToken);
    }
}
