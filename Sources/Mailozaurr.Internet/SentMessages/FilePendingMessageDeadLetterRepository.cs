using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Stores terminal pending-message failures in a newline-delimited JSON file.
/// </summary>
public sealed class FilePendingMessageDeadLetterRepository : IPendingMessageDeadLetterRepository {
    private const string DefaultPendingFileName = "pending.log";
    private const string DefaultDeadLetterFileName = "dead-letter.log";

    private readonly string filePath;
    private readonly string lockFilePath;
    private readonly string conflictPath = string.Empty;
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTime lastStaleRewriteCleanupUtc;
    private static readonly byte[] NewlineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);

    /// <summary>Creates a repository using pending-message options and a dead-letter file name.</summary>
    public FilePendingMessageDeadLetterRepository(PendingMessageRepositoryOptions? options = null)
        : this(GetStorageFiles(options)) {
    }

    private FilePendingMessageDeadLetterRepository((string Path, string ConflictPath) storageFiles)
        : this(storageFiles.Path) => conflictPath = storageFiles.ConflictPath;

    /// <summary>Creates a repository using an explicit file path.</summary>
    public FilePendingMessageDeadLetterRepository(string filePath) {
        this.filePath = Path.GetFullPath(filePath ?? throw new ArgumentNullException(nameof(filePath)));
        lockFilePath = this.filePath + ".lock";
    }

    internal FilePendingMessageDeadLetterRepository(string filePath, string conflictPath)
        : this(filePath) => this.conflictPath = Path.GetFullPath(conflictPath);

    private static (string Path, string ConflictPath) GetStorageFiles(PendingMessageRepositoryOptions? options) {
        options ??= new PendingMessageRepositoryOptions();
        var directory = options.DirectoryPath;
        string name;
        try {
            name = options.FileNamingScheme?.Invoke() ?? DefaultPendingFileName;
        } catch (Exception ex) {
            throw new InvalidOperationException("FileNamingScheme failed to provide a file name", ex);
        }

        if (string.IsNullOrWhiteSpace(name)
            || Path.IsPathRooted(name)
            || name == "."
            || name == ".."
            || name.IndexOfAny(new[] { '/', '\\' }) >= 0
            || !string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal)) {
            throw new InvalidOperationException("FileNamingScheme must return a single file name without directory components.");
        }
        string deadLetterFileName = CreateDeadLetterFileName(name);
        string targetPath = Path.Combine(Path.GetFullPath(directory), deadLetterFileName);
        return options.UsesDefaultDirectory
            ? MailozaurrStoragePaths.ResolveDefaultStorageFiles(
                targetPath,
                Path.Combine(Path.GetTempPath(), deadLetterFileName))
            : (targetPath, string.Empty);
    }

    private void ThrowIfDefaultQueueConflictAppeared() =>
        MailozaurrStoragePaths.ThrowIfConflictingQueueAppeared(filePath, conflictPath);

    private static string CreateDeadLetterFileName(string? pendingFileName) {
        if (string.IsNullOrWhiteSpace(pendingFileName)) {
            return DefaultDeadLetterFileName;
        }

        var fileName = Path.GetFileName(pendingFileName);
        if (string.IsNullOrWhiteSpace(fileName) ||
            string.Equals(fileName, DefaultPendingFileName, StringComparison.OrdinalIgnoreCase)) {
            return DefaultDeadLetterFileName;
        }

        var extension = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(baseName)) {
            return DefaultDeadLetterFileName;
        }

        return string.IsNullOrEmpty(extension)
            ? $"{baseName}.dead-letter.log"
            : $"{baseName}.dead-letter{extension}";
    }

    /// <inheritdoc />
    public async Task SaveAsync(PendingMessageDeadLetterRecord record, CancellationToken cancellationToken = default) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }

        if (record.Message == null || string.IsNullOrWhiteSpace(record.Message.MessageId)) {
            throw new InvalidOperationException("Dead-letter record message id is required.");
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ThrowIfDefaultQueueConflictAppeared();
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory)) {
                bool directoryExisted = Directory.Exists(directory);
                Directory.CreateDirectory(directory);
                if (!directoryExisted) UnixFilePermissions.RestrictDirectory(directory);
            }

            using var storageLock = await AcquireStorageLockAsync(cancellationToken).ConfigureAwait(false);

            var payload = JsonSerializer.SerializeToUtf8Bytes(record, MailozaurrJsonContext.Default.PendingMessageDeadLetterRecord);
            var line = new byte[payload.Length + NewlineBytes.Length];
            Buffer.BlockCopy(payload, 0, line, 0, payload.Length);
            Buffer.BlockCopy(NewlineBytes, 0, line, payload.Length, NewlineBytes.Length);
            using var write = UnixFilePermissions.OpenRestrictedFile(
                filePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read);
            RepairIncompleteTail(write);
            if (ContainsMessageId(record.Message.MessageId)) return;
            write.Position = write.Length;
            await write.WriteAsync(line, 0, line.Length, CancellationToken.None).ConfigureAwait(false);
            await write.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            write.Flush(flushToDisk: true);
        } finally {
            gate.Release();
        }
    }

    private bool ContainsMessageId(string messageId) {
        if (!File.Exists(filePath)) return false;
        foreach (var line in LogFileLineReader.ReadLinesWithOffsets(filePath)) {
            if (string.IsNullOrWhiteSpace(line.Line)) continue;
            try {
                var record = JsonSerializer.Deserialize(line.Line,
                    MailozaurrJsonContext.Default.PendingMessageDeadLetterRecord);
                if (record?.Message != null && string.Equals(record.Message.MessageId,
                        messageId, StringComparison.OrdinalIgnoreCase)) return true;
            } catch (JsonException) {
                // Ignore malformed historical entries while looking for this id.
            }
        }
        return false;
    }

    /// <inheritdoc />
    public async Task<PendingMessageDeadLetterRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("Message id is required.", nameof(messageId));
        }

        PendingMessageDeadLetterRecord? match = null;
        await foreach (var record in GetAllAsync(cancellationToken).ConfigureAwait(false)) {
            if (string.Equals(record.Message.MessageId, messageId, StringComparison.OrdinalIgnoreCase)) {
                match = record;
            }
        }

        return match;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<PendingMessageDeadLetterRecord> GetAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default) {
        ThrowIfDefaultQueueConflictAppeared();
        if (!File.Exists(filePath)) {
            yield break;
        }

        List<PendingMessageDeadLetterRecord> snapshot = new();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ThrowIfDefaultQueueConflictAppeared();
            using var storageLock = await AcquireStorageLockAsync(cancellationToken).ConfigureAwait(false);
            if (!File.Exists(filePath)) yield break;
            using var read = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(read, Encoding.UTF8, false, 1024, leaveOpen: true);
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null) {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }

                try {
                    var record = JsonSerializer.Deserialize(line, MailozaurrJsonContext.Default.PendingMessageDeadLetterRecord);
                    if (record?.Message != null && !string.IsNullOrWhiteSpace(record.Message.MessageId)) {
                        snapshot.Add(record);
                    }
                } catch (JsonException) {
                    continue;
                }
            }
        } catch (FileNotFoundException) {
            snapshot.Clear();
        } catch (DirectoryNotFoundException) {
            snapshot.Clear();
        } finally {
            gate.Release();
        }

        foreach (var record in snapshot) {
            cancellationToken.ThrowIfCancellationRequested();
            yield return record;
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("Message id is required.", nameof(messageId));
        }
        ThrowIfDefaultQueueConflictAppeared();
        if (!File.Exists(filePath)) return;

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ThrowIfDefaultQueueConflictAppeared();
            using var storageLock = await AcquireStorageLockAsync(cancellationToken).ConfigureAwait(false);
            if (!File.Exists(filePath)) {
                return;
            }

            var retained = new List<PendingMessageDeadLetterRecord>();
            using (var read = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(read, Encoding.UTF8, false, 1024, leaveOpen: true)) {
                string? line;
                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null) {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(line)) {
                        continue;
                    }

                    PendingMessageDeadLetterRecord? record;
                    try {
                        record = JsonSerializer.Deserialize(line, MailozaurrJsonContext.Default.PendingMessageDeadLetterRecord);
                    } catch (JsonException) {
                        continue;
                    }

                    if (record?.Message != null && !string.Equals(record.Message.MessageId, messageId, StringComparison.OrdinalIgnoreCase)) {
                        retained.Add(record);
                    }
                }
            }

            await RewriteAsync(retained, cancellationToken).ConfigureAwait(false);
        } finally {
            gate.Release();
        }
    }

    private async Task RewriteAsync(IReadOnlyList<PendingMessageDeadLetterRecord> records, CancellationToken cancellationToken) {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        var tempPath = filePath + ".tmp." + Guid.NewGuid().ToString("N");
        var backupPath = filePath + ".bak";
        try {
            using (var write = UnixFilePermissions.OpenRestrictedFile(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None)) {
                foreach (var record in records) {
                    var payload = JsonSerializer.SerializeToUtf8Bytes(record, MailozaurrJsonContext.Default.PendingMessageDeadLetterRecord);
                    await write.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
                    await write.WriteAsync(NewlineBytes, 0, NewlineBytes.Length, cancellationToken).ConfigureAwait(false);
                }

                await write.FlushAsync(cancellationToken).ConfigureAwait(false);
                write.Flush(flushToDisk: true);
            }

            if (File.Exists(filePath)) {
                File.Replace(tempPath, filePath, backupPath);
                tempPath = string.Empty;
                if (File.Exists(backupPath)) {
                    File.Delete(backupPath);
                }
                backupPath = string.Empty;
                return;
            }

            File.Move(tempPath, filePath);
            tempPath = string.Empty;
        } finally {
            if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath)) {
                File.Delete(tempPath);
            }

            if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath)) {
                File.Delete(backupPath);
            }
        }
    }

    private async Task<FileStream> AcquireStorageLockAsync(CancellationToken cancellationToken) {
        var elapsed = Stopwatch.StartNew();
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                var storageLock = UnixFilePermissions.OpenRestrictedFile(lockFilePath, FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None);
                try {
                    CleanupStaleRewriteFiles();
                    return storageLock;
                } catch {
                    storageLock.Dispose();
                    throw;
                }
            } catch (IOException) when (elapsed.Elapsed < TimeSpan.FromSeconds(30)) {
                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void CleanupStaleRewriteFiles() {
        var now = DateTime.UtcNow;
        if (now - lastStaleRewriteCleanupUtc < TimeSpan.FromHours(1)) return;
        lastStaleRewriteCleanupUtc = now;
        var directory = Path.GetDirectoryName(filePath)!;
        var prefix = Path.GetFileName(filePath) + ".tmp.";
        string[] candidates;
        try {
            candidates = Directory.GetFiles(directory, prefix + "*", SearchOption.TopDirectoryOnly)
                .Concat(new[] { filePath + ".tmp" }).Where(File.Exists).ToArray();
        } catch (IOException) {
            return;
        } catch (UnauthorizedAccessException) {
            return;
        }
        foreach (var path in candidates) {
            if (!string.Equals(path, filePath + ".tmp", StringComparison.Ordinal)) {
                var suffix = Path.GetFileName(path).Substring(prefix.Length);
                if (!Guid.TryParseExact(suffix, "N", out _)) continue;
            }
            try {
                if (File.GetLastWriteTimeUtc(path) > now - TimeSpan.FromHours(24)) continue;
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                File.Delete(path);
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }
        }
    }

    private void RepairIncompleteTail(FileStream write) {
        if (write.Length == 0) return;
        write.Position = write.Length - 1;
        if (write.ReadByte() == '\n') return;
        long lastOffset = 0;
        string? lastLine = null;
        foreach (var line in LogFileLineReader.ReadLinesWithOffsets(filePath)) {
            lastOffset = line.Offset;
            lastLine = line.Line;
        }
        if (lastLine == null) return;
        try {
            var record = JsonSerializer.Deserialize(lastLine,
                MailozaurrJsonContext.Default.PendingMessageDeadLetterRecord);
            if (record?.Message != null && !string.IsNullOrWhiteSpace(record.Message.MessageId)) {
                write.Position = write.Length;
                write.Write(NewlineBytes, 0, NewlineBytes.Length);
                return;
            }
        } catch (JsonException) {
            // A partial record cannot be treated as a successful dead letter.
        }
        write.SetLength(lastOffset);
    }
}
