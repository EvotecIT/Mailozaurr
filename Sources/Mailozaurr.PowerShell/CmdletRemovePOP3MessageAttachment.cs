using System.Management.Automation;
using MimeKit;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Removes attachments from a POP3 <see cref="MimeMessage"/> instance.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "POP3MessageAttachment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Low)]
[OutputType(typeof(MimeMessage))]
public sealed class CmdletRemovePOP3MessageAttachment : PSCmdlet {
    /// <summary>
    /// MIME message to process.
    /// </summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [ValidateNotNull]
    public MimeMessage? Message { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        if (Message == null) {
            return;
        }
        if (!ShouldProcess("MimeMessage", "Removing attachments")) {
            WriteObject(Message);
            return;
        }
        RemoveMimeAttachments(Message.Body);
        WriteObject(Message);
    }

    private static void RemoveMimeAttachments(MimeEntity? entity) {
        if (entity is Multipart multipart) {
            for (int i = multipart.Count - 1; i >= 0; i--) {
                var part = multipart[i];
                if (part is MimePart mp && mp.IsAttachment || part is MessagePart) {
                    multipart.RemoveAt(i);
                } else {
                    RemoveMimeAttachments(part);
                }
            }
        }
    }
}
