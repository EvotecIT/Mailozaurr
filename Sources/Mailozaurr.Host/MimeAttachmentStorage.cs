using MimeKit;

namespace Mailozaurr.Hosting;

internal static class MimeAttachmentStorage {
    public static MimeEntity? ResolveAttachment(IReadOnlyList<MimeEntity> attachments, string attachmentId) {
        if (int.TryParse(attachmentId, out var index) && index >= 0 && index < attachments.Count) {
            return attachments[index];
        }

        return attachments.FirstOrDefault(attachment =>
            string.Equals(GetAttachmentFileName(attachment), attachmentId, StringComparison.OrdinalIgnoreCase));
    }

    public static string ResolveDestinationPath(string requestedPath, MimeEntity attachment) {
        var destinationPath = Path.GetFullPath(requestedPath);
        if (Directory.Exists(destinationPath)) {
            return Path.Combine(destinationPath, GetAttachmentFileName(attachment));
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        return destinationPath;
    }

    public static string GetAttachmentFileName(MimeEntity attachment) => attachment switch {
        MimePart part => part.FileName ?? Path.GetRandomFileName(),
        MessagePart messagePart => messagePart.ContentDisposition?.FileName ?? messagePart.ContentType?.Name ?? Path.GetRandomFileName(),
        _ => Path.GetRandomFileName()
    };

    public static void SaveAttachment(MimeEntity attachment, string destinationPath) {
        switch (attachment) {
            case MimePart part:
                using (var stream = File.Create(destinationPath)) {
                    if (part.Content != null) {
                        part.Content.DecodeTo(stream);
                    } else {
                        part.WriteTo(stream);
                    }
                }
                break;
            case MessagePart messagePart:
                if (messagePart.Message != null) {
                    messagePart.Message.WriteTo(destinationPath);
                } else {
                    using (var stream = File.Create(destinationPath)) {
                        messagePart.WriteTo(stream);
                    }
                }
                break;
            default:
                throw new InvalidOperationException("Unsupported attachment type.");
        }
    }
}