using MimeKit;
using System.Collections.Generic;
using System.IO;

namespace Mailozaurr;

/// <summary>
/// Utility methods for working with <see cref="MimeKit"/> objects.
/// </summary>
public static class MimeKitUtils {
    /// <summary>
    /// Saves the provided MIME attachments to the specified directory.
    /// </summary>
    /// <param name="attachments">Collection of MIME entities representing attachments.</param>
    /// <param name="path">Directory path where attachments should be saved.</param>
    public static void SaveAttachments(IEnumerable<MimeEntity> attachments, string path) {
        var resolved = Path.GetFullPath(path);
        if (!Directory.Exists(resolved)) Directory.CreateDirectory(resolved);
        foreach (var attachment in attachments) {
            if (attachment is MimePart mp) {
                var file = Path.Combine(resolved, mp.FileName ?? Path.GetRandomFileName());
                using var fs = File.Create(file);
                mp.Content.DecodeTo(fs);
            } else if (attachment is MessagePart msgPart) {
                var name = msgPart.ContentDisposition?.FileName ?? msgPart.ContentType.Name ?? Path.GetRandomFileName();
                var file = Path.Combine(resolved, name);
                msgPart.Message.WriteTo(file);
            }
        }
    }
}
