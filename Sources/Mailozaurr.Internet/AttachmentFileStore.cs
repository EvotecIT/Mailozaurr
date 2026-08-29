using System.Security.Cryptography;
using System.Runtime.InteropServices;

namespace Mailozaurr;

/// <summary>Determines how an attachment save handles an existing destination.</summary>
public enum AttachmentFileConflictPolicy {
    /// <summary>Fail without changing the existing file.</summary>
    Fail = 0,
    /// <summary>Keep the existing file and report that the attachment was skipped.</summary>
    Skip = 1,
    /// <summary>Create a collision-free sibling filename.</summary>
    Rename = 2,
    /// <summary>Atomically replace a regular destination file.</summary>
    Replace = 3
}

/// <summary>Outcome of one attachment file save.</summary>
public enum AttachmentFileSaveAction {
    /// <summary>The requested destination was created.</summary>
    Created = 0,
    /// <summary>An existing destination was retained.</summary>
    Skipped = 1,
    /// <summary>A collision-free sibling destination was created.</summary>
    Renamed = 2,
    /// <summary>An existing regular file was atomically replaced.</summary>
    Replaced = 3
}

/// <summary>Result of an attachment file save.</summary>
public sealed class AttachmentFileSaveResult {
    internal AttachmentFileSaveResult(string path, AttachmentFileSaveAction action) {
        Path = path;
        Action = action;
    }

    /// <summary>Canonical path that was created, replaced, or retained.</summary>
    public string Path { get; }
    /// <summary>Action taken at the destination.</summary>
    public AttachmentFileSaveAction Action { get; }
}

/// <summary>
/// Canonical attachment filename, containment, collision, and atomic-write owner shared by providers.
/// </summary>
public static class AttachmentFileStore {
    /// <summary>Resolves a remote attachment name beneath an explicit destination directory.</summary>
    public static string ResolvePathInDirectory(
        string destinationDirectory,
        string? remoteFileName,
        string? attachmentIdentity = null) {
        if (string.IsNullOrWhiteSpace(destinationDirectory)) {
            throw new ArgumentException("A destination directory is required.", nameof(destinationDirectory));
        }

        string directory = Path.GetFullPath(destinationDirectory);
        string fileName = GetSafeFileName(remoteFileName, attachmentIdentity);
        string resolved = Path.GetFullPath(Path.Combine(directory, fileName));
        EnsureContained(directory, resolved);
        return resolved;
    }

    /// <summary>Creates a portable filename from an untrusted remote attachment name.</summary>
    public static string GetSafeFileName(string? remoteFileName, string? attachmentIdentity = null) {
        string sourceFileName = remoteFileName ?? string.Empty;
        string raw = sourceFileName.Replace('\\', '/');
        int separatorIndex = raw.LastIndexOf('/');
        string fileName = separatorIndex >= 0 ? raw.Substring(separatorIndex + 1) : raw;
        if (string.IsNullOrWhiteSpace(fileName) || fileName == "." || fileName == "..") {
            fileName = CreateFallbackFileName(attachmentIdentity);
            sourceFileName = fileName;
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(fileName.Length);
        foreach (char character in fileName) {
            if (character < 32 || invalid.Contains(character) || IsPortableInvalidFileNameCharacter(character)) {
                builder.Append('_');
            } else {
                builder.Append(character);
            }
        }

        string sanitized = builder.ToString().TrimEnd(' ', '.');
        if (string.IsNullOrWhiteSpace(sanitized)) {
            sanitized = CreateFallbackFileName(attachmentIdentity);
        }
        if (IsReservedWindowsFileName(sanitized)) {
            sanitized = "_" + sanitized;
        }
        return AppendSourceNameHash(sanitized, sourceFileName, attachmentIdentity);
    }

    /// <summary>Atomically saves bytes beneath an explicit destination directory.</summary>
    public static AttachmentFileSaveResult SaveBytesToDirectory(
        string destinationDirectory,
        string? remoteFileName,
        byte[] content,
        AttachmentFileConflictPolicy conflictPolicy = AttachmentFileConflictPolicy.Fail,
        string? attachmentIdentity = null) {
        if (content == null) throw new ArgumentNullException(nameof(content));
        return SaveToDirectory(
            destinationDirectory,
            remoteFileName,
            stream => stream.Write(content, 0, content.Length),
            conflictPolicy,
            attachmentIdentity);
    }

    /// <summary>Atomically writes an attachment beneath an explicit destination directory.</summary>
    public static AttachmentFileSaveResult SaveToDirectory(
        string destinationDirectory,
        string? remoteFileName,
        Action<Stream> writeContent,
        AttachmentFileConflictPolicy conflictPolicy = AttachmentFileConflictPolicy.Fail,
        string? attachmentIdentity = null) {
        string destinationPath = ResolvePathInDirectory(
            destinationDirectory,
            remoteFileName,
            attachmentIdentity);
        return SaveToFile(destinationPath, writeContent, conflictPolicy);
    }

    /// <summary>Asynchronously and atomically writes an attachment beneath an explicit destination directory.</summary>
    public static Task<AttachmentFileSaveResult> SaveToDirectoryAsync(
        string destinationDirectory,
        string? remoteFileName,
        Func<Stream, CancellationToken, Task> writeContentAsync,
        AttachmentFileConflictPolicy conflictPolicy = AttachmentFileConflictPolicy.Fail,
        string? attachmentIdentity = null,
        CancellationToken cancellationToken = default) {
        string destinationPath = ResolvePathInDirectory(
            destinationDirectory,
            remoteFileName,
            attachmentIdentity);
        return SaveToFileAsync(
            destinationPath,
            writeContentAsync,
            conflictPolicy,
            cancellationToken);
    }

    /// <summary>Atomically writes an attachment to an explicit output file.</summary>
    public static AttachmentFileSaveResult SaveToFile(
        string outputPath,
        Action<Stream> writeContent,
        AttachmentFileConflictPolicy conflictPolicy = AttachmentFileConflictPolicy.Fail) {
        if (string.IsNullOrWhiteSpace(outputPath)) {
            throw new ArgumentException("An output path is required.", nameof(outputPath));
        }
        if (writeContent == null) throw new ArgumentNullException(nameof(writeContent));
        ValidateConflictPolicy(conflictPolicy);

        string destinationPath = Path.GetFullPath(outputPath);
        string directory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("The output path has no parent directory.");
        using var directoryLease = AttachmentDirectoryLease.Acquire(directory);
        if (!directoryLease.UsesNativeRelativePaths) RejectReparsePoint(destinationPath);
        if (conflictPolicy == AttachmentFileConflictPolicy.Skip &&
            (directoryLease.UsesNativeRelativePaths
                ? directoryLease.TrySkipExisting(destinationPath)
                : PathEntryExists(destinationPath))) {
            return new AttachmentFileSaveResult(destinationPath, AttachmentFileSaveAction.Skipped);
        }

        string temporaryPath = string.Empty;
        try {
            using (FileStream stream = directoryLease.CreateTemporaryFile(useAsync: false, out temporaryPath)) {
                UnixFilePermissions.RestrictFile(temporaryPath);
                writeContent(stream);
                stream.Flush(true);
            }
            return directoryLease.UsesNativeRelativePaths
                ? directoryLease.Commit(temporaryPath, destinationPath, conflictPolicy)
                : CommitTemporaryFile(temporaryPath, destinationPath, conflictPolicy);
        } finally {
            directoryLease.DeleteTemporary(temporaryPath);
        }
    }

    /// <summary>Asynchronously and atomically writes an attachment to an explicit output file.</summary>
    public static async Task<AttachmentFileSaveResult> SaveToFileAsync(
        string outputPath,
        Func<Stream, CancellationToken, Task> writeContentAsync,
        AttachmentFileConflictPolicy conflictPolicy = AttachmentFileConflictPolicy.Fail,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(outputPath)) {
            throw new ArgumentException("An output path is required.", nameof(outputPath));
        }
        if (writeContentAsync == null) throw new ArgumentNullException(nameof(writeContentAsync));
        ValidateConflictPolicy(conflictPolicy);
        cancellationToken.ThrowIfCancellationRequested();

        string destinationPath = Path.GetFullPath(outputPath);
        string directory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("The output path has no parent directory.");
        using var directoryLease = AttachmentDirectoryLease.Acquire(directory);
        if (!directoryLease.UsesNativeRelativePaths) RejectReparsePoint(destinationPath);
        if (conflictPolicy == AttachmentFileConflictPolicy.Skip &&
            (directoryLease.UsesNativeRelativePaths
                ? directoryLease.TrySkipExisting(destinationPath)
                : PathEntryExists(destinationPath))) {
            return new AttachmentFileSaveResult(destinationPath, AttachmentFileSaveAction.Skipped);
        }

        string temporaryPath = string.Empty;
        try {
            using (FileStream stream = directoryLease.CreateTemporaryFile(useAsync: true, out temporaryPath)) {
                UnixFilePermissions.RestrictFile(temporaryPath);
                await writeContentAsync(stream, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return directoryLease.UsesNativeRelativePaths
                ? directoryLease.Commit(temporaryPath, destinationPath, conflictPolicy)
                : CommitTemporaryFile(temporaryPath, destinationPath, conflictPolicy);
        } finally {
            directoryLease.DeleteTemporary(temporaryPath);
        }
    }

    private static AttachmentFileSaveResult CommitTemporaryFile(
        string temporaryPath,
        string destinationPath,
        AttachmentFileConflictPolicy conflictPolicy) {
        switch (conflictPolicy) {
            case AttachmentFileConflictPolicy.Fail:
                RejectReparsePoint(destinationPath);
                File.Move(temporaryPath, destinationPath);
                return new AttachmentFileSaveResult(destinationPath, AttachmentFileSaveAction.Created);
            case AttachmentFileConflictPolicy.Skip:
                if (PathEntryExists(destinationPath)) {
                    RejectReparsePoint(destinationPath);
                    return new AttachmentFileSaveResult(destinationPath, AttachmentFileSaveAction.Skipped);
                }
                try {
                    File.Move(temporaryPath, destinationPath);
                    return new AttachmentFileSaveResult(destinationPath, AttachmentFileSaveAction.Created);
                } catch (IOException) when (PathEntryExists(destinationPath)) {
                    RejectReparsePoint(destinationPath);
                    return new AttachmentFileSaveResult(destinationPath, AttachmentFileSaveAction.Skipped);
                }
            case AttachmentFileConflictPolicy.Rename:
                return CommitWithRename(temporaryPath, destinationPath);
            case AttachmentFileConflictPolicy.Replace:
                return CommitWithReplace(temporaryPath, destinationPath);
            default:
                throw new ArgumentOutOfRangeException(nameof(conflictPolicy));
        }
    }

    private static AttachmentFileSaveResult CommitWithRename(
        string temporaryPath,
        string destinationPath) {
        string directory = Path.GetDirectoryName(destinationPath)!;
        string extension = Path.GetExtension(destinationPath);
        string stem = Path.GetFileNameWithoutExtension(destinationPath);
        for (int suffix = 0; suffix < 10_000; suffix++) {
            string candidate = suffix == 0
                ? destinationPath
                : Path.Combine(directory, stem + " (" + suffix.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) + ")" + extension);
            RejectReparsePoint(candidate);
            try {
                File.Move(temporaryPath, candidate);
                return new AttachmentFileSaveResult(
                    candidate,
                    suffix == 0
                        ? AttachmentFileSaveAction.Created
                        : AttachmentFileSaveAction.Renamed);
            } catch (IOException) when (PathEntryExists(candidate)) {
                RejectReparsePoint(candidate);
            }
        }
        throw new IOException("No collision-free attachment filename was available.");
    }

    private static AttachmentFileSaveResult CommitWithReplace(
        string temporaryPath,
        string destinationPath) {
        while (true) {
            if (!PathEntryExists(destinationPath)) {
                try {
                    File.Move(temporaryPath, destinationPath);
                    return new AttachmentFileSaveResult(
                        destinationPath,
                        AttachmentFileSaveAction.Created);
                } catch (IOException) when (PathEntryExists(destinationPath)) {
                    // A concurrent writer won the create race. Validate and replace its regular file.
                }
            }

            RejectReparsePoint(destinationPath);
            try {
                if (!MoveFileEx(
                        temporaryPath,
                        destinationPath,
                        MoveFileReplaceExisting | MoveFileWriteThrough)) {
                    int error = Marshal.GetLastWin32Error();
                    if (error == 2 || error == 3) continue;
                    throw new IOException(
                        "Unable to atomically replace the attachment destination.",
                        new System.ComponentModel.Win32Exception(error));
                }
                return new AttachmentFileSaveResult(
                    destinationPath,
                    AttachmentFileSaveAction.Replaced);
            } catch (IOException) when (!PathEntryExists(destinationPath)) {
                // The destination disappeared after validation. Retry the create path.
            }
        }
    }

    private static void EnsureDirectory(string directory) {
        bool existed = Directory.Exists(directory);
        Directory.CreateDirectory(directory);
        if (!existed) UnixFilePermissions.RestrictDirectory(directory);
    }

    private static void EnsureContained(string directory, string destinationPath) {
        string directoryPrefix = directory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        StringComparison comparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!destinationPath.StartsWith(directoryPrefix, comparison)) {
            throw new InvalidOperationException("Attachment destination escaped the requested directory.");
        }
    }

    private static void RejectReparsePoint(string path) {
        try {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0) {
                throw new IOException("Attachment destinations cannot be symbolic links or reparse points.");
            }
            if ((attributes & FileAttributes.Directory) != 0) {
                throw new IOException("Attachment destination refers to a directory.");
            }
        } catch (FileNotFoundException) {
        } catch (DirectoryNotFoundException) {
        }
    }

    private static bool PathEntryExists(string path) {
        try {
            File.GetAttributes(path);
            return true;
        } catch (FileNotFoundException) {
            return false;
        } catch (DirectoryNotFoundException) {
            return false;
        }
    }

    internal static bool PathEntryExistsForLease(string path) => PathEntryExists(path);

    private static FileStream CreateTemporaryFile(string directory, out string path) =>
        CreateTemporaryFile(directory, useAsync: false, out path);

    private static FileStream CreateTemporaryFile(string directory, bool useAsync, out string path) {
        using var random = RandomNumberGenerator.Create();
        var bytes = new byte[16];
        for (int attempt = 0; attempt < 128; attempt++) {
            random.GetBytes(bytes);
            string name = ".mailozaurr-attachment-" +
                BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant() + ".tmp";
            path = Path.Combine(directory, name);
            try {
                return new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    FileOptions.WriteThrough | (useAsync ? FileOptions.Asynchronous : 0));
            } catch (IOException) when (PathEntryExists(path)) {
            }
        }
        path = string.Empty;
        throw new IOException("Unable to allocate a temporary attachment file.");
    }

    private static void TryDeleteTemporaryFile(string path) {
        try {
            if (File.Exists(path)) File.Delete(path);
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }

    internal static void TryDeleteTemporaryFileForLease(string path) => TryDeleteTemporaryFile(path);

    private static void ValidateConflictPolicy(AttachmentFileConflictPolicy conflictPolicy) {
        if (!Enum.IsDefined(typeof(AttachmentFileConflictPolicy), conflictPolicy)) {
            throw new ArgumentOutOfRangeException(nameof(conflictPolicy));
        }
    }

    private static string CreateFallbackFileName(string? attachmentIdentity) {
        if (string.IsNullOrWhiteSpace(attachmentIdentity)) return "attachment.bin";
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(attachmentIdentity));
        string hashText = BitConverter.ToString(hash, 0, 8)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
        return "attachment-" + hashText + ".bin";
    }

    private static bool IsPortableInvalidFileNameCharacter(char character) => character switch {
        '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*' => true,
        _ => false
    };

    private static bool IsReservedWindowsFileName(string fileName) {
        int separatorIndex = fileName.IndexOf('.');
        string stem = (separatorIndex < 0 ? fileName : fileName.Substring(0, separatorIndex))
            .TrimEnd(' ', '.');
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }
        if (stem.Length != 4) return false;
        string prefix = stem.Substring(0, 3);
        char suffix = stem[3];
        return suffix is >= '1' and <= '9'
            && (prefix.Equals("COM", StringComparison.OrdinalIgnoreCase)
                || prefix.Equals("LPT", StringComparison.OrdinalIgnoreCase));
    }

    private static string AppendSourceNameHash(
        string sanitizedFileName,
        string sourceFileName,
        string? attachmentIdentity) {
        using var sha256 = SHA256.Create();
        string identityInput = string.IsNullOrWhiteSpace(attachmentIdentity)
            ? sourceFileName
            : sourceFileName + "\0" + attachmentIdentity;
        byte[] hash = sha256.ComputeHash(Encoding.Unicode.GetBytes(identityInput));
        string hashText = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        string extension = TruncateUtf8(Path.GetExtension(sanitizedFileName), 32);
        string stem = TruncateUtf8(Path.GetFileNameWithoutExtension(sanitizedFileName), 96);
        if (string.IsNullOrWhiteSpace(stem)) stem = "attachment";
        return stem + "~" + hashText + extension;
    }

    private static string TruncateUtf8(string value, int maxBytes) {
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes) return value;
        var builder = new StringBuilder(value.Length);
        int byteCount = 0;
        for (int index = 0; index < value.Length;) {
            char character = value[index];
            int characterLength = char.IsHighSurrogate(character)
                && index + 1 < value.Length
                && char.IsLowSurrogate(value[index + 1])
                    ? 2
                    : 1;
            int characterBytes = characterLength == 2
                ? 4
                : character <= 0x7f
                    ? 1
                    : character <= 0x7ff ? 2 : 3;
            if (byteCount + characterBytes > maxBytes) break;
            builder.Append(value, index, characterLength);
            byteCount += characterBytes;
            index += characterLength;
        }
        return builder.ToString();
    }

    private const uint MoveFileReplaceExisting = 0x1;
    private const uint MoveFileWriteThrough = 0x8;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileEx(string existingFileName, string newFileName, uint flags);
}
