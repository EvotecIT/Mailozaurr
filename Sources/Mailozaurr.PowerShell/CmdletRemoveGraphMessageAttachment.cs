using System.Management.Automation;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Removes attachments from a <see cref="GraphMessage"/> instance.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "GraphMessageAttachment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Low)]
[OutputType(typeof(GraphMessage))]
public sealed class CmdletRemoveGraphMessageAttachment : PSCmdlet {
    /// <summary>
    /// Graph message to process.
    /// </summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphMessage? Message { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        if (Message == null) {
            return;
        }
        if (!ShouldProcess("GraphMessage", "Removing attachments")) {
            WriteObject(Message);
            return;
        }
        Message.Attachments = null;
        WriteObject(Message);
    }
}