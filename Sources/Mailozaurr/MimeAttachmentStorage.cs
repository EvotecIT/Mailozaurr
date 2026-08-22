using MimeKit;
using System.Security.Cryptography;
using System.Text;

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
        var sourceFileName = remoteFileName ?? string.Empty;
        var raw = sourceFileName.Replace('\\', '/');
        var separatorIndex = raw.LastIndexOf('/');
        var fileName = separatorIndex >= 0 ? raw.Substring(separatorIndex + 1) : raw;
        if (string.IsNullOrWhiteSpace(fileName) || fileName == "." || fileName == "..") {
            return Path.GetRandomFileName();
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
            return Path.GetRandomFileName();
        }

        if (IsReservedWindowsFileName(sanitized)) {
            sanitized = "_" + sanitized;
        }

        return AppendSourceNameHash(sanitized, sourceFileName);
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

    private static string AppendSourceNameHash(string sanitizedFileName, string sourceFileName) {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.Unicode.GetBytes(sourceFileName));
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
