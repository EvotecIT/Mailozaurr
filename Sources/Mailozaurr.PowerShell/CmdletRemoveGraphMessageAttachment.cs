using System.Management.Automation;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Removes attachments from a <see cref="GraphMessage"/> instance.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "GraphMessageAttachment")]
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
        if (Message != null) {
            Message.Attachments = null;
            WriteObject(Message);
        }
    }
}
