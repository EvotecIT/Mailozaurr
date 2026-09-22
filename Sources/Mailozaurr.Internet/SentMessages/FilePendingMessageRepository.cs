using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Mailozaurr;

/// <summary>
/// Stores pending message records in a single newline-delimited JSON file.
/// </summary>
public sealed class FilePendingMessageRepository : IPendingMessageRepository, IPendingMessageLeaseRenewer,
    IPendingMessageForcedLeaseRepository {
    private const string UpsertEntryType = "upsert";
    private const string TombstoneEntryType = "tombstone";
    private const int DefaultCompactionThreshold = 64;

    private readonly string filePath;
    private readonly string lockFilePath;
    private readonly string conflictPath = string.Empty;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, long> index = new(StringComparer.OrdinalIgnoreCase);
    private long indexedLength = -1;
    private DateTime indexedWriteTimeUtc;
    private DateTime indexedCreationTimeUtc;
    private static readonly byte[] NewlineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);
    private int dirtyEntryCount;

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

        if (!File.Exists(filePath)) {
            indexedLength = -1;
            return;
        }

        try {
            foreach (var line in LogFileLineReader.ReadLinesWithOffsets(filePath)) {
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
        CaptureFileStamp();
    }

    private void RefreshIndexIfChanged() {
        var file = new FileInfo(filePath);
        if (!file.Exists) {
            if (indexedLength != -1) BuildIndex();
            return;
        }
        if (file.Length != indexedLength || file.LastWriteTimeUtc != indexedWriteTimeUtc ||
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

    private static PendingMessageLogEnvelope CreateUpsertEnvelope(PendingMessageRecord record) => new() {
        EntryType = UpsertEntryType,
        MessageId = record.MessageId,
        Record = record
    };

    private static PendingMessageLogEnvelope CreateTombstoneEnvelope(string messageId) => new() {
        EntryType = TombstoneEntryType,
        MessageId = messageId
    };

    private static byte[] SerializeEnvelope(PendingMessageLogEnvelope envelope) => JsonSerializer.SerializeToUtf8Bytes(envelope, MailozaurrJsonContext.Default.PendingMessageLogEnvelope);

    private async Task<long> AppendEnvelopeAsync(PendingMessageLogEnvelope envelope, CancellationToken cancellationToken) {
        var payload = SerializeEnvelope(envelope);

        using var write = UnixFilePermissions.OpenRestrictedFile(
            filePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read);
        var offset = write.Position;

        await write.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
        await write.WriteAsync(NewlineBytes, 0, NewlineBytes.Length, cancellationToken).ConfigureAwait(false);
        await write.FlushAsync(cancellationToken).ConfigureAwait(false);

        return offset;
    }

    private async Task CompactIfNeededAsync(CancellationToken cancellationToken) {
        if (dirtyEntryCount < DefaultCompactionThreshold) {
            return;
        }

        if (!File.Exists(filePath)) {
            return;
        }

        await CompactAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task CompactAsync(CancellationToken cancellationToken) {
        var temp = filePath + ".compact";

        if (File.Exists(temp)) {
            File.Delete(temp);
        }

        var records = new Dictionary<string, PendingMessageRecord>(StringComparer.OrdinalIgnoreCase);
        var orderedIds = new LinkedList<string>();
        var nodesById = new Dictionary<string, LinkedListNode<string>>(StringComparer.OrdinalIgnoreCase);

        using (var read = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
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

        try {
            using (var write = UnixFilePermissions.OpenRestrictedFile(
                temp,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None)) {
                long position = 0;

                foreach (var id in orderedIds) {
                    if (!records.TryGetValue(id, out var record)) {
                        continue;
                    }

                    var envelope = CreateUpsertEnvelope(record);
                    var payload = SerializeEnvelope(envelope);

                    await write.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
                    await write.WriteAsync(NewlineBytes, 0, NewlineBytes.Length, cancellationToken).ConfigureAwait(false);

                    newIndex[id] = position;
                    position += payload.Length + NewlineBytes.Length;
                }

                await write.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            var backup = filePath + ".bak";
            if (File.Exists(filePath)) {
                File.Replace(temp, filePath, backup);
                if (File.Exists(backup)) {
                    File.Delete(backup);
                }
            } else {
                File.Move(temp, filePath);
            }

            index.Clear();
            foreach (var pair in newIndex) {
                index[pair.Key] = pair.Value;
            }

            dirtyEntryCount = 0;
        } finally {
            if (File.Exists(temp)) {
                File.Delete(temp);
            }
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
            using var storageLock = await AcquireStorageLockAsync(cancellationToken).ConfigureAwait(false);
            RefreshIndexIfChanged();

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

            await CompactIfNeededAsync(cancellationToken).ConfigureAwait(false);
            CaptureFileStamp();
        } finally {
            gate.Release();
        }
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
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ThrowIfDefaultQueueConflictAppeared();
            if (!File.Exists(filePath)) return null;
            using var storageLock = await AcquireStorageLockAsync(cancellationToken).ConfigureAwait(false);
            RefreshIndexIfChanged();
            var current = await GetByMessageIdCoreAsync(messageId, cancellationToken).ConfigureAwait(false);
            if (current == null || current.ProcessingLeaseUntil > now ||
                !ignoreSchedule && current.NextAttemptAt > now) {
                return null;
            }

            _ = current.ProviderData;
            current.NextAttemptAt = leaseUntil;
            current.ProcessingLeaseUntil = leaseUntil;
            var offset = await AppendEnvelopeAsync(CreateUpsertEnvelope(current), cancellationToken).ConfigureAwait(false);
            dirtyEntryCount++;
            index[current.MessageId] = offset;

            await CompactIfNeededAsync(cancellationToken).ConfigureAwait(false);
            CaptureFileStamp();
            return current;
        } catch (FileNotFoundException) {
            return null;
        } catch (DirectoryNotFoundException) {
            return null;
        } finally {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryRenewLeaseAsync(string messageId, DateTimeOffset expectedLeaseUntil,
        DateTimeOffset newLeaseUntil, CancellationToken cancellationToken = default) {
        if (newLeaseUntil <= expectedLeaseUntil) {
            throw new ArgumentOutOfRangeException(nameof(newLeaseUntil));
        }
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ThrowIfDefaultQueueConflictAppeared();
            if (!File.Exists(filePath)) return false;
            using var storageLock = await AcquireStorageLockAsync(cancellationToken).ConfigureAwait(false);
            RefreshIndexIfChanged();
            var current = await GetByMessageIdCoreAsync(messageId, cancellationToken).ConfigureAwait(false);
            if (current == null || current.NextAttemptAt != expectedLeaseUntil ||
                current.ProcessingLeaseUntil != expectedLeaseUntil ||
                current.DeliveryAcceptedAt.HasValue) {
                return false;
            }
            current.NextAttemptAt = newLeaseUntil;
            current.ProcessingLeaseUntil = newLeaseUntil;
            var offset = await AppendEnvelopeAsync(CreateUpsertEnvelope(current), cancellationToken).ConfigureAwait(false);
            dirtyEntryCount++;
            index[current.MessageId] = offset;
            await CompactIfNeededAsync(cancellationToken).ConfigureAwait(false);
            CaptureFileStamp();
            return true;
        } finally {
            gate.Release();
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
            using var storageLock = await AcquireStorageLockAsync(cancellationToken).ConfigureAwait(false);
            RefreshIndexIfChanged();
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

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ThrowIfDefaultQueueConflictAppeared();
            using var storageLock = await AcquireStorageLockAsync(cancellationToken).ConfigureAwait(false);
            var orderedIds = new LinkedList<string>();
            var nodesById = new Dictionary<string, LinkedListNode<string>>(StringComparer.OrdinalIgnoreCase);
            var recordsById = new Dictionary<string, PendingMessageRecord>(StringComparer.OrdinalIgnoreCase);
            using var read = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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
        } finally {
            gate.Release();
        }

        foreach (var record in snapshot) {
            cancellationToken.ThrowIfCancellationRequested();
            yield return record;
        }
    }

    /// <summary>Removes a pending message by its ID.</summary>
    public async Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ThrowIfDefaultQueueConflictAppeared();
            if (!File.Exists(filePath)) return;
            using var storageLock = await AcquireStorageLockAsync(cancellationToken).ConfigureAwait(false);
            RefreshIndexIfChanged();
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

            await CompactIfNeededAsync(cancellationToken).ConfigureAwait(false);
            CaptureFileStamp();
        } finally {
            gate.Release();
        }
    }
}
