using MimeKit;

namespace Mailozaurr;

internal static class MimeAttachmentStorage {
    public static MimeEntity? ResolveAttachment(IReadOnlyList<MimeEntity> attachments, string attachmentId) {
        if (int.TryParse(attachmentId, out var index) && index >= 0 && index < attachments.Count) {
            return attachments[index];
        }

        return attachments.FirstOrDefault(attachment =>
            string.Equals(GetAttachmentFileName(attachment), attachmentId, StringComparison.OrdinalIgnoreCase));
    }

    public static string ResolveDestinationPath(string requestedPath, MimeEntity attachment) =>
        ResolveDestinationPath(requestedPath, GetAttachmentFileName(attachment));

    public static string ResolveDestinationPath(string requestedPath, string remoteFileName) {
        var destinationPath = Path.GetFullPath(requestedPath);
        if (Directory.Exists(destinationPath)) {
            var fileName = GetSafeAttachmentFileName(remoteFileName);
            var resolvedPath = Path.GetFullPath(Path.Combine(destinationPath, fileName));
            var directoryPrefix = destinationPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!resolvedPath.StartsWith(directoryPrefix, comparison)) {
                throw new InvalidOperationException("Attachment destination escaped the requested directory.");
            }
            return resolvedPath;
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

    private static string GetSafeAttachmentFileName(string remoteFileName) {
        var raw = (remoteFileName ?? string.Empty).Replace('\\', '/');
        var fileName = Path.GetFileName(raw);
        if (string.IsNullOrWhiteSpace(fileName) || fileName == "." || fileName == "..") {
            return Path.GetRandomFileName();
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(character => invalid.Contains(character) ? '_' : character).ToArray())
            .TrimEnd(' ', '.');
        return string.IsNullOrWhiteSpace(sanitized) ? Path.GetRandomFileName() : sanitized;
    }

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
