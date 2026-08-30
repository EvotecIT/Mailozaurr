using System.Management.Automation;

namespace Mailozaurr.PowerShell;

internal static class AttachmentCmdletConflictPolicy {
    internal static AttachmentFileConflictPolicy Resolve(
        PSCmdlet cmdlet,
        SwitchParameter force,
        AttachmentFileConflictPolicy conflictPolicy) {
        if (cmdlet == null) throw new ArgumentNullException(nameof(cmdlet));
        bool explicitPolicy = cmdlet.MyInvocation.BoundParameters.ContainsKey("ConflictPolicy");
        if (force.IsPresent && explicitPolicy &&
            conflictPolicy != AttachmentFileConflictPolicy.Replace) {
            throw new PSArgumentException(
                "Force cannot be combined with a non-Replace ConflictPolicy.");
        }
        return force.IsPresent
            ? AttachmentFileConflictPolicy.Replace
            : conflictPolicy;
    }
}
