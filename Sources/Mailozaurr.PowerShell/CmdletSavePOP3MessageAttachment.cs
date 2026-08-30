using Mailozaurr;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Saves attachments from a POP3 message to disk.</para>
/// <para type="description">The <c>Save-POP3MessageAttachment</c> cmdlet saves all attachments from a POP3 message identified by its index to the specified directory.</para>
/// </summary>
[Cmdlet(VerbsData.Save, "POP3MessageAttachment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public sealed class CmdletSavePOP3MessageAttachment : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">The <see cref="PopConnectionInfo"/> object representing the active POP3 connection.</para>
    /// </summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public PopConnectionInfo? Client { get; set; }

    /// <summary>
    /// <para type="description">Specifies the index of the message to process.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 1)]
    public int Index { get; set; }

    /// <summary>
    /// <para type="description">Specifies the directory path where attachments will be saved.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 2)]
    [ValidateNotNullOrEmpty]
    public string? Path { get; set; }

    /// <summary>Controls how existing destination files are handled.</summary>
    [Parameter]
    public AttachmentFileConflictPolicy ConflictPolicy { get; set; } = AttachmentFileConflictPolicy.Fail;

    /// <summary>Replaces existing regular files. Equivalent to ConflictPolicy Replace.</summary>
    [Parameter]
    [Alias("Overwrite")]
    public SwitchParameter Force { get; set; }

    /// <summary>Writes one save result for each attachment.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <summary>
    /// Saves attachments from the specified POP3 message to disk.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.Pop3Session;
        if (conn != null && conn.Data != null) {
            if (Index < conn.Data.Count) {
                var message = await conn.Data.GetMessageAsync(Index, CancelToken).ConfigureAwait(false);
                if (Path is not null) {
                    AttachmentFileConflictPolicy policy = AttachmentCmdletConflictPolicy.Resolve(
                        this,
                        Force,
                        ConflictPolicy);
                    if (ShouldProcess(Path, "Save POP3 message attachments")) {
                    IReadOnlyList<AttachmentFileSaveResult> results = await MimeKitUtils.SaveAttachmentsAsync(
                        message.Attachments,
                        Path,
                        policy,
                        CancelToken).ConfigureAwait(false);
                        if (PassThru.IsPresent) WriteObject(results, enumerateCollection: true);
                    }
                }
            } else {
                WriteWarning($"Save-POP3MessageAttachment - Index is out of range. Use index less than {conn.Data.Count}.");
            }
        } else {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Save-POP3MessageAttachment - POP3 client not provided or not connected."),
                "ClientNotConnected",
                ErrorCategory.InvalidOperation,
                null));
        }
    }
}
