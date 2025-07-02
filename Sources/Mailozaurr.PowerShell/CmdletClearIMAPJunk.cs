using System.Management.Automation;
using MailKit.Net.Imap;
using System.Threading.Tasks;
using System.Linq;
using MailKit;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Clears messages from an IMAP junk folder.
/// </summary>
[Cmdlet(VerbsCommon.Clear, "IMAPJunk", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class CmdletClearIMAPJunk : AsyncPSCmdlet {
    /// <summary>Active IMAP connection info.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    /// <summary>Junk folder name.</summary>
    [Parameter(Position = 1)]
    public string? Folder { get; set; }

    [Parameter]
    public SwitchParameter Preview { get; set; }

    [Parameter]
    public string[]? SkipFrom { get; set; }

    [Parameter]
    public string[]? SkipTo { get; set; }

    [Parameter]
    public string[]? SkipSubjectContains { get; set; }

    [Parameter]
    public string[]? SkipMessageId { get; set; }

    [Parameter]
    public uint[]? SkipUid { get; set; }

    [Parameter]
    public SwitchParameter SkipHasAttachment { get; set; }

    [Parameter]
    public string[]? SkipAttachmentExtension { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            var folder = Folder ?? "Junk";
            if (Preview.IsPresent) {
                await foreach (var msg in JunkCleaner.GetImapJunkAsync(
                    conn.Data,
                    folder,
                    SkipFrom,
                    SkipTo,
                    SkipSubjectContains,
                    SkipMessageId,
                    SkipUid?.Select(id => new UniqueId(id)),
                    SkipHasAttachment.IsPresent,
                    SkipAttachmentExtension,
                    CancelToken)) {
                    WriteObject(msg);
                }
                return;
            }
            if (!ShouldProcess(folder, "Clearing IMAP junk")) return;
            await JunkCleaner.ClearImapJunkAsync(
                conn.Data,
                folder,
                SkipFrom,
                SkipTo,
                SkipSubjectContains,
                SkipMessageId,
                SkipUid?.Select(id => new UniqueId(id)),
                SkipHasAttachment.IsPresent,
                SkipAttachmentExtension,
                CancelToken);
        } else {
            WriteWarning("Clear-IMAPJunk - Is IMAP connected?");
        }
    }
}
