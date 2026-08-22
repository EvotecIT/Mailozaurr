using MimeKit;
using System.Security.Cryptography;
using System.Text;

namespace Mailozaurr;

internal static class MimeAttachmentStorage {
    public static MimeEntity? ResolveAttachment(IReadOnlyList<MimeEntity> attachments, string attachmentId) {
        var index = ResolveAttachmentIndex(attachments, attachmentId);
        return index >= 0 ? attachments[index] : null;
    }

    public static int ResolveAttachmentIndex(IReadOnlyList<MimeEntity> attachments, string attachmentId) {
        if (int.TryParse(attachmentId, out var index) && index >= 0 && index < attachments.Count) {
            return index;
        }

        for (var attachmentIndex = 0; attachmentIndex < attachments.Count; attachmentIndex++) {
            if (string.Equals(GetAttachmentFileName(attachments[attachmentIndex]), attachmentId,
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
            var fileName = GetSafeAttachmentFileName(remoteFileName, attachmentIdentity);
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

    private static string GetSafeAttachmentFileName(string remoteFileName, string? attachmentIdentity) {
        var sourceFileName = remoteFileName ?? string.Empty;
        var raw = sourceFileName.Replace('\\', '/');
        var separatorIndex = raw.LastIndexOf('/');
        var fileName = separatorIndex >= 0 ? raw.Substring(separatorIndex + 1) : raw;
        if (string.IsNullOrWhiteSpace(fileName) || fileName == "." || fileName == "..") {
            fileName = CreateFallbackFileName(attachmentIdentity);
            sourceFileName = fileName;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(fileName.Length);
        foreach (var character in fileName) {
            if (character < 32 || invalid.Contains(character) || IsPortableInvalidFileNameCharacter(character)) {
                builder.Append('_');
            } else {
                builder.Append(character);
            }
        }

        var sanitized = builder.ToString().TrimEnd(' ', '.');
        if (string.IsNullOrWhiteSpace(sanitized)) {
            sanitized = CreateFallbackFileName(attachmentIdentity);
        }

        if (IsReservedWindowsFileName(sanitized)) {
            sanitized = "_" + sanitized;
        }

        return AppendSourceNameHash(sanitized, sourceFileName, attachmentIdentity);
    }

    private static string CreateFallbackFileName(string? attachmentIdentity) {
        if (string.IsNullOrWhiteSpace(attachmentIdentity)) {
            return "attachment.bin";
        }

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(attachmentIdentity));
        var hashText = BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
        return "attachment-" + hashText + ".bin";
    }

    private static bool IsPortableInvalidFileNameCharacter(char character) => character switch {
        '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*' => true,
        _ => false
    };

    private static bool IsReservedWindowsFileName(string fileName) {
        var separatorIndex = fileName.IndexOf('.');
        var stem = (separatorIndex < 0 ? fileName : fileName.Substring(0, separatorIndex)).TrimEnd(' ', '.');
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (stem.Length != 4) {
            return false;
        }

        var prefix = stem.Substring(0, 3);
        var suffix = stem[3];
        return suffix is >= '1' and <= '9'
            && (prefix.Equals("COM", StringComparison.OrdinalIgnoreCase)
                || prefix.Equals("LPT", StringComparison.OrdinalIgnoreCase));
    }

    private static string AppendSourceNameHash(
        string sanitizedFileName,
        string sourceFileName,
        string? attachmentIdentity) {
        using var sha256 = SHA256.Create();
        var identityInput = string.IsNullOrWhiteSpace(attachmentIdentity)
            ? sourceFileName
            : sourceFileName + "\0" + attachmentIdentity;
        var hash = sha256.ComputeHash(Encoding.Unicode.GetBytes(identityInput));
        var hashText = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        var extension = TruncateUtf8(Path.GetExtension(sanitizedFileName), 32);
        var stem = TruncateUtf8(Path.GetFileNameWithoutExtension(sanitizedFileName), 96);
        if (string.IsNullOrWhiteSpace(stem)) {
            stem = "attachment";
        }
        return stem + "~" + hashText + extension;
    }

    private static string TruncateUtf8(string value, int maxBytes) {
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes) {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        var byteCount = 0;
        for (var index = 0; index < value.Length;) {
            var character = value[index];
            var characterLength = char.IsHighSurrogate(character)
                && index + 1 < value.Length
                && char.IsLowSurrogate(value[index + 1])
                    ? 2
                    : 1;
            var characterBytes = characterLength == 2
                ? 4
                : character <= 0x7f
                    ? 1
                    : character <= 0x7ff
                        ? 2
                        : 3;
            if (byteCount + characterBytes > maxBytes) {
                break;
            }

            builder.Append(value, index, characterLength);
            byteCount += characterBytes;
            index += characterLength;
        }

        return builder.ToString();
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
