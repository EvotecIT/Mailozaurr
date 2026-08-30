using Mailozaurr.Definitions;
using System.Management.Automation;

namespace Mailozaurr.PowerShell;

internal static class AttachmentInputConverter {
    internal static List<AttachmentDescriptor>? Convert(object[]? attachments) {
        if (attachments == null) {
            return null;
        }

        var descriptors = new List<AttachmentDescriptor>();
        foreach (var entry in attachments.Where(entry => entry != null).Select(entry => entry!)) {
            var input = UnwrapPowerShellObject(entry);
            if (input is AttachmentDescriptor descriptor) {
                descriptors.Add(descriptor);
                continue;
            }

            var path = GetPath(input);
            if (path == null) {
                throw new ArgumentException($"Unsupported attachment type: {input.GetType().Name}");
            }

            descriptors.Add(new FileAttachmentDescriptor(path));
        }

        return descriptors;
    }

    internal static string? GetPath(object entry) => entry switch {
        string path => path,
        FileInfo fileInfo => fileInfo.FullName,
        PathInfo pathInfo => pathInfo.ProviderPath,
        _ => null
    };

    internal static void ReleaseStaging(object[]? attachments) {
        if (attachments == null) return;
        AttachmentDescriptorLifetime.ReleaseStaging(
            attachments
                .Where(entry => entry != null)
                .Select(entry => UnwrapPowerShellObject(entry!))
                .OfType<AttachmentDescriptor>());
    }

    internal static void MarkSendAttempted(object[]? attachments) {
        if (attachments == null) return;
        AttachmentDescriptorLifetime.MarkSendAttempted(
            attachments
                .Where(entry => entry != null)
                .Select(entry => UnwrapPowerShellObject(entry!))
                .OfType<AttachmentDescriptor>());
    }

    private static object UnwrapPowerShellObject(object entry) {
        while (entry is PSObject powerShellObject &&
               powerShellObject.BaseObject != null &&
               !ReferenceEquals(powerShellObject.BaseObject, entry)) {
            entry = powerShellObject.BaseObject;
        }

        return entry;
    }
}
