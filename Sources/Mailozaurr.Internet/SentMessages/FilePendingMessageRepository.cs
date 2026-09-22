using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Mailozaurr;

/// <summary>
/// Stores pending message records in a single newline-delimited JSON file.
/// </summary>
public sealed class FilePendingMessageRepository : IPendingMessageRepository, IPendingMessageLeaseRenewer,
    IPendingMessageLeaseExpirationRenewer, IPendingMessageLeaseCommitter,
    IPendingMessageForcedLeaseRepository {
    private const string UpsertEntryType = "upsert";
    private const string TombstoneEntryType = "tombstone";
    private const string GenerationPrefix = "mailozaurr-generation:";
    private const int LeaseAwareFormatVersion = 2;
    private const int DefaultCompactionThreshold = 64;

    private readonly string filePath;
    private readonly string lockFilePath;
    private readonly string conflictPath = string.Empty;
    private readonly Func<CancellationToken, Task>? compactionOverride;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly SemaphoreSlim compactionGate = new(1, 1);
    private readonly Dictionary<string, long> index = new(StringComparer.OrdinalIgnoreCase);
    private long indexedLength = -1;
    private DateTime indexedWriteTimeUtc;
    private DateTime indexedCreationTimeUtc;
    private string? indexedGeneration;
    private static readonly byte[] NewlineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);
    private int dirtyEntryCount;
    private DateTime lastStaleCompactionCleanupUtc;

    /// <summary>Creates a new repository using the specified options.</summary>
    /// <param name="options">Configuration for directory and file naming.</param>
    public FilePendingMessageRepository(PendingMessageRepositoryOptions? options = null)
        : this(GetStorageFiles(options)) { }

    private FilePendingMessageRepository((string Path, string ConflictPath) storageFiles)
        : this(storageFiles.Path) => conflictPath = storageFiles.ConflictPath;

    /// <summary>
    /// Creates a new repository using the specified file path.
    /// </summary>
    /// <param name="filePath">Path to the file that stores pending messages.</param>
    public FilePendingMessageRepository(string filePath) {
        this.filePath = Path.GetFullPath(filePath ?? throw new ArgumentNullException(nameof(filePath)));
        lockFilePath = this.filePath + ".lock";
    }

    internal FilePendingMessageRepository(string filePath, string conflictPath)
        : this(filePath) => this.conflictPath = Path.GetFullPath(conflictPath);

    internal FilePendingMessageRepository(string filePath,
        Func<CancellationToken, Task> compactionOverride)
        : this(filePath) => this.compactionOverride = compactionOverride;

    private static (string Path, string ConflictPath) GetStorageFiles(PendingMessageRepositoryOptions? options) {
        options ??= new PendingMessageRepositoryOptions();
        var directory = options.DirectoryPath;
        string name;
        try {
            name = options.FileNamingScheme?.Invoke() ?? "pending.log";
        } catch (Exception ex) {
            throw new InvalidOperationException("FileNamingScheme failed to provide a file name", ex);
        }
        ValidateFileName(name);
        string targetPath = Path.Combine(Path.GetFullPath(directory), name);
        return options.UsesDefaultDirectory
            ? MailozaurrStoragePaths.ResolveDefaultStorageFiles(
                targetPath,
                Path.Combine(Path.GetTempPath(), name))
            : (targetPath, string.Empty);
    }

    private void ThrowIfDefaultQueueConflictAppeared() =>
        MailozaurrStoragePaths.ThrowIfConflictingQueueAppeared(filePath, conflictPath);

    private static void ValidateFileName(string name) {
        if (string.IsNullOrWhiteSpace(name)
            || Path.IsPathRooted(name)
            || name == "."
            || name == ".."
            || name.IndexOfAny(new[] { '/', '\\' }) >= 0
            || !string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal)) {
            throw new InvalidOperationException("FileNamingScheme must return a single file name without directory components.");
        }
    }

    private void BuildIndex() {
        index.Clear();
        dirtyEntryCount = 0;
        indexedGeneration = null;
        var stampBefore = GetFileStamp();

        if (!File.Exists(filePath)) {
            indexedLength = -1;
            return;
        }

        long completeLength = 0;
        try {
            foreach (var line in LogFileLineReader.ReadLinesWithOffsets(filePath)) {
                if (!line.IsComplete && !TryParseLogEntry(line.Line, out _)) break;
                completeLength = line.EndOffset;
                if (line.Offset == 0 && line.Line.StartsWith(GenerationPrefix, StringComparison.Ordinal)) {
                    indexedGeneration = line.Line.Substring(GenerationPrefix.Length);
                    continue;
                }
                if (!TryParseLogEntry(line.Line, out var entry)) {
                    continue;
                }

                switch (entry.Kind) {
                    case LogEntryKind.Upsert:
                        if (index.ContainsKey(entry.MessageId)) {
                            dirtyEntryCount++;
                        }
                        index[entry.MessageId] = line.Offset;
                        break;
                    case LogEntryKind.Tombstone:
                        index.Remove(entry.MessageId);
                        dirtyEntryCount++;
                        break;
                }
            }
        } catch (FileNotFoundException) {
            index.Clear();
            dirtyEntryCount = 0;
        } catch (DirectoryNotFoundException) {
            index.Clear();
            dirtyEntryCount = 0;
        }
        var stampAfter = GetFileStamp();
        CaptureFileStamp();
        indexedLength = stampAfter != null && stampBefore == stampAfter ? completeLength : -1;
    }

    private long ApplyAppendedEntries(long startOffset) {
        long completeLength = startOffset;
        foreach (var line in LogFileLineReader.ReadLinesWithOffsets(filePath, startOffset)) {
            if (!line.IsComplete && !TryParseLogEntry(line.Line, out _)) break;
            completeLength = line.EndOffset;
            if (!TryParseLogEntry(line.Line, out var entry)) continue;
            switch (entry.Kind) {
                case LogEntryKind.Upsert:
                    if (index.ContainsKey(entry.MessageId)) dirtyEntryCount++;
                    index[entry.MessageId] = line.Offset;
                    break;
                case LogEntryKind.Tombstone:
                    index.Remove(entry.MessageId);
                    dirtyEntryCount++;
                    break;
            }
        }
        return completeLength;
    }

    private void RefreshIndexIfChanged() {
        var file = new FileInfo(filePath);
        if (!file.Exists) {
            if (indexedLength != -1) BuildIndex();
            return;
        }
        if (indexedLength >= 0 &&
            !StringComparer.Ordinal.Equals(indexedGeneration, ReadCurrentGeneration())) {
            BuildIndex();
        } else if (indexedLength >= 0 && file.CreationTimeUtc == indexedCreationTimeUtc &&
            file.Length > indexedLength) {
            var stampBefore = GetFileStamp();
            long completeLength = ApplyAppendedEntries(indexedLength);
            var stampAfter = GetFileStamp();
            CaptureFileStamp();
            indexedLength = stampBefore == stampAfter ? completeLength : -1;
        } else if (file.Length != indexedLength || file.LastWriteTimeUtc != indexedWriteTimeUtc ||
                   file.CreationTimeUtc != indexedCreationTimeUtc) {
            BuildIndex();
        }
    }

    private void CaptureFileStamp() {
        var file = new FileInfo(filePath);
        if (!file.Exists) {
            indexedLength = -1;
            indexedWriteTimeUtc = default;
            indexedCreationTimeUtc = default;
            return;
        }
        indexedLength = file.Length;
        indexedWriteTimeUtc = file.LastWriteTimeUtc;
        indexedCreationTimeUtc = file.CreationTimeUtc;
    }

    private async Task<FileStream> AcquireStorageLockAsync(CancellationToken cancellationToken) {
        var elapsed = Stopwatch.StartNew();
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                var stream = new FileStream(lockFilePath, FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None);
                try {
                    UnixFilePermissions.RestrictFile(lockFilePath);
                    CleanupStaleCompactionFiles();
                    return stream;
                } catch {
                    stream.Dispose();
                    throw;
                }
            } catch (IOException) when (elapsed.Elapsed < TimeSpan.FromSeconds(30)) {
                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void CleanupStaleCompactionFiles() {
        var now = DateTime.UtcNow;
        if (now - lastStaleCompactionCleanupUtc < TimeSpan.FromHours(1)) return;
        lastStaleCompactionCleanupUtc = now;
        var directory = Path.GetDirectoryName(filePath)!;
        var prefix = Path.GetFileName(filePath) + ".compact.";
        string[] candidates;
        try {
            candidates = Directory.GetFiles(directory, prefix + "*", SearchOption.TopDirectoryOnly)
                .Concat(new[] { filePath + ".compact" })
                .Where(File.Exists)
                .ToArray();
        } catch (IOException) {
            return;
        } catch (UnauthorizedAccessException) {
            return;
        }
        foreach (var path in candidates) {
            if (!string.Equals(path, filePath + ".compact", StringComparison.Ordinal)) {
                var suffix = Path.GetFileName(path).Substring(prefix.Length);
                if (!Guid.TryParseExact(suffix, "N", out _)) continue;
            }
            try {
                // An active compaction writes with FileShare.None. Recent files
                // may also be awaiting their short commit lock.
                if (File.GetLastWriteTimeUtc(path) > now - TimeSpan.FromHours(24)) continue;
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                File.Delete(path);
            } catch (IOException) {
                // An active worker may still own the candidate.
            } catch (UnauthorizedAccessException) {
                // Cleanup must not stop queue processing.
            }
        }
    }

    private async Task<FileStream> AcquireIndexedStorageLockAsync(CancellationToken cancellationToken) {
        // Full index construction happens before the cross-process lock. Once
        // acquired, only a concurrent append's suffix normally remains.
        for (var attempt = 0; attempt < 3; attempt++) {
            RefreshIndexIfChanged();
            if (indexedLength >= 0 || !File.Exists(filePath)) break;
        }
        var storageLock = await AcquireStorageLockAsync(cancellationToken).ConfigureAwait(false);
        try {
            RefreshIndexIfChanged();
            return storageLock;
        } catch {
            storageLock.Dispose();
            throw;
        }
    }

    private enum LogEntryKind {
        Upsert,
        Tombstone
    }

    private readonly struct LogEntry {
        public LogEntry(LogEntryKind kind, string messageId, PendingMessageRecord? record) {
            Kind = kind;
            MessageId = messageId;
            Record = record;
        }

        public LogEntryKind Kind { get; }

        public string MessageId { get; }

        public PendingMessageRecord? Record { get; }
    }

    private static PendingMessageLogEnvelope CreateUpsertEnvelope(
        PendingMessageRecord record, bool preserveFormat = false) {
        var formatVersion = preserveFormat && !record.IsLeaseAwareEnvelope
            ? 0
            : LeaseAwareFormatVersion;
        record.IsLeaseAwareEnvelope = formatVersion >= LeaseAwareFormatVersion;
        return new PendingMessageLogEnvelope {
            FormatVersion = formatVersion,
            EntryType = UpsertEntryType,
            MessageId = record.MessageId,
            Record = record
        };
    }

    private static PendingMessageLogEnvelope CreateTombstoneEnvelope(string messageId) => new() {
        FormatVersion = LeaseAwareFormatVersion,
        EntryType = TombstoneEntryType,
        MessageId = messageId
    };

    private static byte[] SerializeEnvelope(PendingMessageLogEnvelope envelope) => JsonSerializer.SerializeToUtf8Bytes(envelope, MailozaurrJsonContext.Default.PendingMessageLogEnvelope);

    private async Task<long> AppendEnvelopeAsync(PendingMessageLogEnvelope envelope, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = SerializeEnvelope(envelope);
        var line = new byte[payload.Length + NewlineBytes.Length];
        Buffer.BlockCopy(payload, 0, line, 0, payload.Length);
        Buffer.BlockCopy(NewlineBytes, 0, line, payload.Length, NewlineBytes.Length);
        using var write = UnixFilePermissions.OpenRestrictedFile(
            filePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read);
        // The storage lock is held by every caller. Drop an incomplete tail left
        // by a prior interrupted write before appending the next envelope.
        if (indexedLength >= 0 && indexedLength < write.Length) write.SetLength(indexedLength);
        if (write.Length > 0) {
            write.Position = write.Length - 1;
            if (write.ReadByte() != '\n') {
                write.Position = write.Length;
                await write.WriteAsync(NewlineBytes, 0, NewlineBytes.Length, CancellationToken.None).ConfigureAwait(false);
            }
        }
        write.Position = write.Length;
        var offset = write.Position;

        // Once an append starts, cancellation must not leave a fragment that
        // can be concatenated with a later acceptance marker.
        await write.WriteAsync(line, 0, line.Length, CancellationToken.None).ConfigureAwait(false);
        await write.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        write.Flush(flushToDisk: true);

        return offset;
    }

    private async Task CompactIfNeededAsync(CancellationToken cancellationToken) {
        bool needed;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            // Keep churn proportional to the live queue size. A fixed
            // threshold rewrites a large queue after every small batch.
            needed = dirtyEntryCount >= Math.Max(DefaultCompactionThreshold, index.Count / 2);
        } finally {
            gate.Release();
        }
        if (needed && File.Exists(filePath)) await CompactAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task CompactAfterCommittedMutationAsync() {
        try {
            await compactionGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try {
                if (compactionOverride != null)
                    await compactionOverride(CancellationToken.None).ConfigureAwait(false);
                else
                    await CompactIfNeededAsync(CancellationToken.None).ConfigureAwait(false);
            } finally {
                compactionGate.Release();
            }
        } catch (Exception ex) {
            Trace.TraceWarning("Pending-message compaction failed after a committed mutation: {0}", ex.Message);
        }
    }

    private async Task CompactAsync(CancellationToken cancellationToken) {
        var originalStamp = GetFileStamp();
        if (originalStamp == null) return;
        var originalGeneration = ReadCurrentGeneration();
        var temp = filePath + ".compact." + Guid.NewGuid().ToString("N");

        var records = new Dictionary<string, PendingMessageRecord>(StringComparer.OrdinalIgnoreCase);
        var orderedIds = new LinkedList<string>();
        var nodesById = new Dictionary<string, LinkedListNode<string>>(StringComparer.OrdinalIgnoreCase);

        using (var read = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(read, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true)) {
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null) {
                cancellationToken.ThrowIfCancellationRequested();

                if (!TryParseLogEntry(line, out var entry)) {
                    continue;
                }

                switch (entry.Kind) {
                    case LogEntryKind.Upsert when entry.Record != null:
                        if (!nodesById.ContainsKey(entry.MessageId)) {
                            nodesById[entry.MessageId] = orderedIds.AddLast(entry.MessageId);
                        }
                        records[entry.MessageId] = entry.Record;
                        break;
                    case LogEntryKind.Tombstone:
                        records.Remove(entry.MessageId);
                        if (nodesById.TryGetValue(entry.MessageId, out var node)) {
                            orderedIds.Remove(node);
                            nodesById.Remove(entry.MessageId);
                        }
                        break;
                }
            }
        }

        var newIndex = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var generation = Guid.NewGuid().ToString("N");

        try {
            using (var write = UnixFilePermissions.OpenRestrictedFile(
                temp,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None)) {
                var header = Encoding.ASCII.GetBytes(GenerationPrefix + generation + Environment.NewLine);
                await write.WriteAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
                long position = header.Length;

                foreach (var id in orderedIds) {
                    if (!records.TryGetValue(id, out var record)) {
                        continue;
                    }

                    var envelope = CreateUpsertEnvelope(record, preserveFormat: true);
                    var payload = SerializeEnvelope(envelope);

                    await write.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
                    await write.WriteAsync(NewlineBytes, 0, NewlineBytes.Length, cancellationToken).ConfigureAwait(false);

                    newIndex[id] = position;
                    position += payload.Length + NewlineBytes.Length;
                }

                await write.FlushAsync(cancellationToken).ConfigureAwait(false);
                write.Flush(flushToDisk: true);
            }

            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                using var storageLock = await AcquireStorageLockAsync(cancellationToken).ConfigureAwait(false);
                if (GetFileStamp() != originalStamp ||
                    !StringComparer.Ordinal.Equals(ReadCurrentGeneration(), originalGeneration)) return;
                var backup = filePath + ".bak";
                File.Replace(temp, filePath, backup);
                if (File.Exists(backup)) {
                    File.Delete(backup);
                }

                index.Clear();
                foreach (var pair in newIndex) {
                    index[pair.Key] = pair.Value;
                }

                dirtyEntryCount = 0;
                indexedGeneration = generation;
                CaptureFileStamp();
            } finally {
                gate.Release();
            }
        } finally {
            if (File.Exists(temp)) {
                File.Delete(temp);
            }
        }
    }

    private (long Length, DateTime WriteTimeUtc, DateTime CreationTimeUtc)? GetFileStamp() {
        try {
            var file = new FileInfo(filePath);
            return file.Exists ? (file.Length, file.LastWriteTimeUtc, file.CreationTimeUtc) : null;
        } catch (FileNotFoundException) {
            return null;
        } catch (DirectoryNotFoundException) {
            return null;
        }
    }

    private string? ReadCurrentGeneration() {
        try {
            using var read = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var header = new byte[GenerationPrefix.Length + 32];
            var count = 0;
            while (count < header.Length) {
                var received = read.Read(header, count, header.Length - count);
                if (received == 0) return null;
                count += received;
            }
            var value = Encoding.ASCII.GetString(header);
            return value.StartsWith(GenerationPrefix, StringComparison.Ordinal)
                ? value.Substring(GenerationPrefix.Length)
                : null;
        } catch (FileNotFoundException) {
            return null;
        } catch (DirectoryNotFoundException) {
            return null;
        }
    }

    private static bool TryParseLogEntry(string json, out LogEntry entry) {
        entry = default;

        if (string.IsNullOrWhiteSpace(json)) {
            return false;
        }

        try {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (TryGetPropertyCaseInsensitive(root, "entryType", out var entryTypeElement) && entryTypeElement.ValueKind == JsonValueKind.String) {
                var entryType = entryTypeElement.GetString();

                if (string.Equals(entryType, TombstoneEntryType, StringComparison.OrdinalIgnoreCase)) {
                    if (TryReadMessageId(root, out var tombstoneMessageId)) {
                        entry = new LogEntry(LogEntryKind.Tombstone, tombstoneMessageId, null);
                        return true;
                    }

                    return false;
                }

                if (string.Equals(entryType, UpsertEntryType, StringComparison.OrdinalIgnoreCase)) {
                    PendingMessageRecord? record = null;
                    if (TryGetPropertyCaseInsensitive(root, "record", out var recordElement) && recordElement.ValueKind == JsonValueKind.Object) {
                        record = recordElement.Deserialize(MailozaurrJsonContext.Default.PendingMessageRecord);
                        if (record != null) {
                            _ = record.ProviderData;
                            record.IsLeaseAwareEnvelope =
                                TryGetPropertyCaseInsensitive(root, "formatVersion", out var versionElement) &&
                                versionElement.ValueKind == JsonValueKind.Number &&
                                versionElement.TryGetInt32(out var version) &&
                                version >= LeaseAwareFormatVersion;
                        }
                    }

                    string? messageId = record?.MessageId;

                    if (string.IsNullOrWhiteSpace(messageId) && TryReadMessageId(root, out var entryMessageId)) {
                        messageId = entryMessageId;
                        if (record != null) {
                            record.MessageId = messageId;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(messageId) && record != null) {
                        entry = new LogEntry(LogEntryKind.Upsert, messageId!, record);
                        return true;
                    }

                    return false;
                }

                return false;
            }

            var legacyRecord = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.PendingMessageRecord);
            if (legacyRecord != null && !string.IsNullOrWhiteSpace(legacyRecord.MessageId)) {
                _ = legacyRecord.ProviderData;
                legacyRecord.IsLeaseAwareEnvelope = false;
                entry = new LogEntry(LogEntryKind.Upsert, legacyRecord.MessageId, legacyRecord);
                return true;
            }
        } catch (JsonException) {
            return false;
        }

        return false;
    }

    private static bool TryReadMessageId(JsonElement element, out string messageId) {
        if (TryGetPropertyCaseInsensitive(element, "messageId", out var messageIdElement) && messageIdElement.ValueKind == JsonValueKind.String) {
            messageId = messageIdElement.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(messageId);
        }

        messageId = string.Empty;
        return false;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value) {
        foreach (var property in element.EnumerateObject()) {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)) {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>Saves a pending message to the repository.</summary>
    public async Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default) {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ThrowIfDefaultQueueConflictAppeared();
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
                UnixFilePermissions.RestrictDirectory(directory);
            }
            using var storageLock = await AcquireIndexedStorageLockAsync(cancellationToken).ConfigureAwait(false);

            if (record.NextAttemptAt == default) {
                record.NextAttemptAt = DateTimeOffset.UtcNow;
            }

            _ = record.ProviderData;
            var hasExistingRecord = index.ContainsKey(record.MessageId);
            var envelope = CreateUpsertEnvelope(record);
            var offset = await AppendEnvelopeAsync(envelope, cancellationToken).ConfigureAwait(false);

            if (hasExistingRecord) {
                dirtyEntryCount++;
            }

            index[record.MessageId] = offset;

            CaptureFileStamp();
        } finally {
            gate.Release();
        }
        await CompactAfterCommittedMutationAsync().ConfigureAwait(false);
    }

    /// <summary>Attempts to lease a due pending message for processing.</summary>
    public async Task<PendingMessageRecord?> TryAcquireLeaseAsync(
        string messageId,
        DateTimeOffset dueBeforeOrAt,
        DateTimeOffset leaseUntil,
        CancellationToken cancellationToken = default) =>
        await TryAcquireLeaseCoreAsync(messageId, dueBeforeOrAt, leaseUntil, false, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<PendingMessageRecord?> TryAcquireForcedLeaseAsync(string messageId,
        DateTimeOffset now, DateTimeOffset leaseUntil, CancellationToken cancellationToken = default) =>
        TryAcquireLeaseCoreAsync(messageId, now, leaseUntil, true, cancellationToken);

    private async Task<PendingMessageRecord?> TryAcquireLeaseCoreAsync(string messageId,
        DateTimeOffset now, DateTimeOffset leaseUntil, bool ignoreSchedule,
        CancellationToken cancellationToken) {
        var acquisitionWait = Stopwatch.StartNew();
        bool changed = false;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ThrowIfDefaultQueueConflictAppeared();
            if (!File.Exists(filePath)) return null;
            using var storageLock = await AcquireIndexedStorageLockAsync(cancellationToken).ConfigureAwait(false);
            var current = await GetByMessageIdCoreAsync(messageId, cancellationToken).ConfigureAwait(false);
            if (current == null) return null;
            var legacyLeaseMayBeActive = ignoreSchedule && !current.IsLeaseAwareEnvelope &&
                !current.ProcessingLeaseUntil.HasValue && current.NextAttemptAt > now;
            if (current.ProcessingLeaseUntil > now || legacyLeaseMayBeActive ||
                !ignoreSchedule && current.NextAttemptAt > now) {
                return null;
            }

            // The caller computes the expiry before waiting for the local gate,
            // cross-process lock, and index refresh. Give the owner its intended
            // lease time when that wait consumed the renewal safety margin.
            var duration = leaseUntil - now;
            var elapsed = acquisitionWait.Elapsed;
            if (duration > TimeSpan.Zero && elapsed.Ticks > duration.Ticks / 3) {
                var available = DateTimeOffset.MaxValue - leaseUntil;
                leaseUntil += elapsed < available ? elapsed : available;
            }

            _ = current.ProviderData;
            current.NextAttemptAt = leaseUntil;
            current.ProcessingLeaseUntil = leaseUntil;
            current.ProcessingLeaseId = Guid.NewGuid().ToString("N");
            var offset = await AppendEnvelopeAsync(CreateUpsertEnvelope(current), cancellationToken).ConfigureAwait(false);
            dirtyEntryCount++;
            index[current.MessageId] = offset;

            CaptureFileStamp();
            changed = true;
            return current;
        } catch (FileNotFoundException) {
            return null;
        } catch (DirectoryNotFoundException) {
            return null;
        } finally {
            gate.Release();
            if (changed) await CompactAfterCommittedMutationAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryRenewLeaseAsync(string messageId, string leaseId, DateTimeOffset expectedLeaseUntil,
        DateTimeOffset newLeaseUntil, CancellationToken cancellationToken = default) =>
        await TryRenewLeaseAndGetExpirationAsync(messageId, leaseId, expectedLeaseUntil,
            newLeaseUntil, cancellationToken).ConfigureAwait(false) != null;

    /// <inheritdoc />
    public async Task<DateTimeOffset?> TryRenewLeaseAndGetExpirationAsync(string messageId, string leaseId,
        DateTimeOffset expectedLeaseUntil, DateTimeOffset newLeaseUntil,
        CancellationToken cancellationToken = default) {
        if (newLeaseUntil <= expectedLeaseUntil) {
            throw new ArgumentOutOfRangeException(nameof(newLeaseUntil));
        }
        var renewalWait = Stopwatch.StartNew();
        bool changed = false;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ThrowIfDefaultQueueConflictAppeared();
            if (!File.Exists(filePath)) return null;
            using var storageLock = await AcquireIndexedStorageLockAsync(cancellationToken).ConfigureAwait(false);
            var current = await GetByMessageIdCoreAsync(messageId, cancellationToken).ConfigureAwait(false);
            if (current == null || current.NextAttemptAt != expectedLeaseUntil ||
                current.ProcessingLeaseUntil != expectedLeaseUntil ||
                current.ProcessingLeaseId != leaseId) {
                return null;
            }
            var duration = newLeaseUntil - expectedLeaseUntil;
            var elapsed = renewalWait.Elapsed;
            if (elapsed.Ticks > duration.Ticks / 3) {
                var available = DateTimeOffset.MaxValue - newLeaseUntil;
                newLeaseUntil += elapsed < available ? elapsed : available;
            }
            current.NextAttemptAt = newLeaseUntil;
            current.ProcessingLeaseUntil = newLeaseUntil;
            var offset = await AppendEnvelopeAsync(CreateUpsertEnvelope(current), cancellationToken).ConfigureAwait(false);
            dirtyEntryCount++;
            index[current.MessageId] = offset;
            CaptureFileStamp();
            changed = true;
            return newLeaseUntil;
        } finally {
            gate.Release();
            if (changed) await CompactAfterCommittedMutationAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<bool> TrySaveWithLeaseAsync(PendingMessageRecord record, string leaseId,
        CancellationToken cancellationToken = default) {
        bool changed = false;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ThrowIfDefaultQueueConflictAppeared();
            if (!File.Exists(filePath)) return false;
            using var storageLock = await AcquireIndexedStorageLockAsync(cancellationToken).ConfigureAwait(false);
            var current = await GetByMessageIdCoreAsync(record.MessageId, cancellationToken).ConfigureAwait(false);
            if (current?.ProcessingLeaseId != leaseId || !current.ProcessingLeaseUntil.HasValue) return false;
            if (record.ProcessingLeaseId == leaseId) {
                // Acceptance can follow one or more renewals while the sender
                // still holds the original record. Keep the persisted expiry.
                record.ProcessingLeaseUntil = current.ProcessingLeaseUntil;
                record.NextAttemptAt = current.ProcessingLeaseUntil.Value;
            }
            var offset = await AppendEnvelopeAsync(CreateUpsertEnvelope(record), cancellationToken).ConfigureAwait(false);
            dirtyEntryCount++;
            index[record.MessageId] = offset;
            CaptureFileStamp();
            changed = true;
            return true;
        } finally {
            gate.Release();
            if (changed) await CompactAfterCommittedMutationAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryRemoveWithLeaseAsync(string messageId, string leaseId,
        CancellationToken cancellationToken = default) {
        bool changed = false;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ThrowIfDefaultQueueConflictAppeared();
            if (!File.Exists(filePath)) return false;
            using var storageLock = await AcquireIndexedStorageLockAsync(cancellationToken).ConfigureAwait(false);
            var current = await GetByMessageIdCoreAsync(messageId, cancellationToken).ConfigureAwait(false);
            if (current?.ProcessingLeaseId != leaseId || !current.ProcessingLeaseUntil.HasValue) return false;
            await AppendEnvelopeAsync(CreateTombstoneEnvelope(messageId), cancellationToken).ConfigureAwait(false);
            index.Remove(messageId);
            dirtyEntryCount++;
            CaptureFileStamp();
            changed = true;
            return true;
        } finally {
            gate.Release();
            if (changed) await CompactAfterCommittedMutationAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Retrieves a pending message by its ID.</summary>
    public async Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) {
        ThrowIfDefaultQueueConflictAppeared();
        if (!File.Exists(filePath)) {
            return null;
        }
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ThrowIfDefaultQueueConflictAppeared();
            using var storageLock = await AcquireIndexedStorageLockAsync(cancellationToken).ConfigureAwait(false);
            return await GetByMessageIdCoreAsync(messageId, cancellationToken).ConfigureAwait(false);
        } catch (FileNotFoundException) {
            return null;
        } catch (DirectoryNotFoundException) {
            return null;
        } finally {
            gate.Release();
        }
    }

    private async Task<PendingMessageRecord?> GetByMessageIdCoreAsync(string messageId, CancellationToken cancellationToken) {
        if (!index.TryGetValue(messageId, out var offset)) {
            return null;
        }

        using var read = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        read.Seek(offset, SeekOrigin.Begin);
        using var reader = new StreamReader(read, Encoding.UTF8, false, 1024, leaveOpen: true);
        cancellationToken.ThrowIfCancellationRequested();
        string? line = await reader.ReadLineAsync().ConfigureAwait(false);
        if (line == null) {
            return null;
        }

        if (!TryParseLogEntry(line, out var entry) || entry.Kind != LogEntryKind.Upsert || entry.Record == null) {
            return null;
        }

        if (!string.Equals(entry.MessageId, messageId, StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        return entry.Record;
    }

    /// <summary>Enumerates all pending messages.</summary>
    public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default) {
        ThrowIfDefaultQueueConflictAppeared();
        if (!File.Exists(filePath)) {
            yield break;
        }
        var snapshot = new List<PendingMessageRecord>();

        try {
            ThrowIfDefaultQueueConflictAppeared();
            var orderedIds = new LinkedList<string>();
            var nodesById = new Dictionary<string, LinkedListNode<string>>(StringComparer.OrdinalIgnoreCase);
            var recordsById = new Dictionary<string, PendingMessageRecord>(StringComparer.OrdinalIgnoreCase);
            using var read = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(read, Encoding.UTF8, false, 1024, leaveOpen: true);
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null) {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryParseLogEntry(line, out var entry)) {
                    continue;
                }

                switch (entry.Kind) {
                    case LogEntryKind.Upsert when entry.Record != null:
                        if (!nodesById.ContainsKey(entry.MessageId)) {
                            nodesById[entry.MessageId] = orderedIds.AddLast(entry.MessageId);
                        }

                        recordsById[entry.MessageId] = entry.Record;
                        break;
                    case LogEntryKind.Tombstone:
                        recordsById.Remove(entry.MessageId);
                        if (nodesById.TryGetValue(entry.MessageId, out var node)) {
                            orderedIds.Remove(node);
                            nodesById.Remove(entry.MessageId);
                        }
                        break;
                }
            }

            foreach (var id in orderedIds) {
                if (recordsById.TryGetValue(id, out var record)) {
                    snapshot.Add(record);
                }
            }
        } catch (FileNotFoundException) {
            snapshot.Clear();
        } catch (DirectoryNotFoundException) {
            snapshot.Clear();
        }

        foreach (var record in snapshot) {
            cancellationToken.ThrowIfCancellationRequested();
            yield return record;
        }
    }

    /// <summary>Removes a pending message by its ID.</summary>
    public async Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
        bool removed = false;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ThrowIfDefaultQueueConflictAppeared();
            if (!File.Exists(filePath)) return;
            using var storageLock = await AcquireIndexedStorageLockAsync(cancellationToken).ConfigureAwait(false);
            if (!index.ContainsKey(messageId)) {
                return;
            }

            if (!File.Exists(filePath)) {
                index.Remove(messageId);
                return;
            }
            var envelope = CreateTombstoneEnvelope(messageId);
            await AppendEnvelopeAsync(envelope, cancellationToken).ConfigureAwait(false);

            index.Remove(messageId);
            dirtyEntryCount++;

            removed = true;
            CaptureFileStamp();
        } finally {
            gate.Release();
        }
        if (removed) await CompactAfterCommittedMutationAsync().ConfigureAwait(false);
    }
}
