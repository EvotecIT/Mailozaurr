using System.Management.Automation;
using MimeKit;
using System.Linq;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Removes attachments from a message object.</para>
/// <para type="description">The <c>Remove-MessageAttachment</c> cmdlet strips all attachments from either a <see cref="MimeMessage"/> or <see cref="GraphMessage"/> before further processing or forwarding.</para>
/// </summary>
[Cmdlet(VerbsCommon.Remove, "MessageAttachment")]
[OutputType(typeof(MimeMessage), typeof(GraphMessage))]
public sealed class CmdletRemoveMessageAttachment : PSCmdlet {
    /// <summary>
    /// Message in MIME format.
    /// </summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Mime")]
    [ValidateNotNull]
    public MimeMessage? MimeMessage { get; set; }

    /// <summary>
    /// Message in Microsoft Graph format.
    /// </summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Graph")]
    [ValidateNotNull]
    public GraphMessage? GraphMessage { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        switch (ParameterSetName) {
            case "Mime":
                if (MimeMessage != null) {
                    RemoveMimeAttachments(MimeMessage.Body);
                    WriteObject(MimeMessage);
                }
                break;
            case "Graph":
                if (GraphMessage != null) {
                    GraphMessage.Attachments = null;
                    WriteObject(GraphMessage);
                }
                break;
        }
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
