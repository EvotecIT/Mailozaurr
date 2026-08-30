using MimeKit;
using System.Text;

namespace Mailozaurr;

internal static class MimeAttachmentStorage {
    public static MimeEntity? ResolveAttachment(IReadOnlyList<MimeEntity> attachments, string attachmentId) {
        var index = ResolveAttachmentIndex(attachments, attachmentId);
        return index >= 0 ? attachments[index] : null;
    }

    public static int ResolveAttachmentIndex(IReadOnlyList<MimeEntity> attachments, string attachmentId) {
        return ResolveAttachmentIndex(attachments, attachmentId, fallbackIdentityFactory: null);
    }

    public static int ResolveAttachmentIndex(
        IReadOnlyList<MimeEntity> attachments,
        string attachmentId,
        Func<int, string?>? fallbackIdentityFactory) {
        if (int.TryParse(attachmentId, out var index) && index >= 0 && index < attachments.Count) {
            return index;
        }

        for (var attachmentIndex = 0; attachmentIndex < attachments.Count; attachmentIndex++) {
            var fallbackIdentity = fallbackIdentityFactory?.Invoke(attachmentIndex);
            if (string.Equals(GetAttachmentFileName(attachments[attachmentIndex], fallbackIdentity), attachmentId,
                StringComparison.OrdinalIgnoreCase)) {
                return attachmentIndex;
            }
        }

        return -1;
    }

    public static string ResolveDestinationPath(string requestedPath, MimeEntity attachment) =>
        ResolveDestinationPath(requestedPath, GetAttachmentFileName(attachment));

    public static string ResolveDestinationPath(
        string requestedPath,
        MimeEntity attachment,
        string storageIdentity) =>
        ResolveDestinationPath(requestedPath, GetAttachmentFileName(attachment, storageIdentity), storageIdentity);

    public static string ResolveDestinationPath(
        string requestedPath,
        string remoteFileName,
        string? attachmentIdentity = null) {
        var destinationPath = Path.GetFullPath(requestedPath);
        if (Directory.Exists(destinationPath)) {
            return AttachmentFileStore.ResolvePathInDirectory(
                destinationPath,
                remoteFileName,
                attachmentIdentity);
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        return destinationPath;
    }

    public static string ResolveDestinationPath(
        string requestedPath,
        string remoteFileName,
        string? attachmentIdentity,
        AttachmentDestinationKind destinationKind) => destinationKind switch {
            AttachmentDestinationKind.Auto => ResolveDestinationPath(requestedPath, remoteFileName, attachmentIdentity),
            AttachmentDestinationKind.File => Path.GetFullPath(requestedPath),
            AttachmentDestinationKind.Directory => ResolveDestinationPathInDirectory(
                requestedPath,
                remoteFileName,
                attachmentIdentity),
            _ => throw new ArgumentOutOfRangeException(nameof(destinationKind))
        };

    public static string ResolveDestinationPath(
        string requestedPath,
        MimeEntity attachment,
        string storageIdentity,
        AttachmentDestinationKind destinationKind) =>
        ResolveDestinationPath(
            requestedPath,
            GetAttachmentFileName(attachment, storageIdentity),
            storageIdentity,
            destinationKind);

    public static string ResolveDestinationPathInDirectory(
        string destinationDirectory,
        string remoteFileName,
        string? attachmentIdentity = null) =>
        AttachmentFileStore.ResolvePathInDirectory(
            destinationDirectory,
            remoteFileName,
            attachmentIdentity);

    public static string CreateStorageIdentity(params string?[] components) {
        if (components == null) {
            throw new ArgumentNullException(nameof(components));
        }

        var builder = new StringBuilder();
        foreach (var component in components) {
            var value = component ?? string.Empty;
            builder.Append(value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(value);
        }
        return builder.ToString();
    }

    public static string GetAttachmentFileName(MimeEntity attachment, string? fallbackIdentity = null) {
        var fileName = attachment switch {
            MimePart part => part.FileName,
            MessagePart messagePart => messagePart.ContentDisposition?.FileName ?? messagePart.ContentType?.Name,
            _ => null
        };
        return string.IsNullOrWhiteSpace(fileName)
            ? CreateFallbackFileName(fallbackIdentity)
            : fileName!;
    }

    public static string GetAttachmentFileName(string? fileName, string? fallbackIdentity = null) =>
        string.IsNullOrWhiteSpace(fileName) ? CreateFallbackFileName(fallbackIdentity) : fileName!;

    private static string CreateFallbackFileName(string? attachmentIdentity) {
        if (string.IsNullOrWhiteSpace(attachmentIdentity)) {
            return "attachment.bin";
        }

        string safe = AttachmentFileStore.GetSafeFileName(null, attachmentIdentity);
        int hashSeparator = safe.IndexOf('~');
        return hashSeparator > 0 ? safe.Substring(0, hashSeparator) + ".bin" : safe;
    }

    public static AttachmentFileSaveResult SaveAttachment(
        MimeEntity attachment,
        string destinationPath,
        AttachmentFileConflictPolicy conflictPolicy) =>
        AttachmentFileStore.SaveToFile(
            destinationPath,
            stream => {
                switch (attachment) {
                case MimePart part:
                    if (part.Content != null) {
                        part.Content.DecodeTo(stream);
                    } else {
                        part.WriteTo(stream);
                    }
                    break;
                case MessagePart messagePart:
                    if (messagePart.Message != null) {
                        messagePart.Message.WriteTo(stream);
                    } else {
                        messagePart.WriteTo(stream);
                    }
                    break;
                default:
                    throw new InvalidOperationException("Unsupported attachment type.");
                }
            },
            conflictPolicy);
}
