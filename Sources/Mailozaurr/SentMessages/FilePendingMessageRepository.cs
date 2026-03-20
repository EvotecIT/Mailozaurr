using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Mailozaurr;

/// <summary>
/// Stores pending message records in a single newline-delimited JSON file.
/// </summary>
public sealed class FilePendingMessageRepository : IPendingMessageRepository {
    private const string UpsertEntryType = "upsert";
    private const string TombstoneEntryType = "tombstone";
    private const int DefaultCompactionThreshold = 64;

    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, long> index = new(StringComparer.OrdinalIgnoreCase);
    private static readonly byte[] NewlineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);
    private int dirtyEntryCount;

    /// <summary>Creates a new repository using the specified options.</summary>
    /// <param name="options">Configuration for directory and file naming.</param>
    public FilePendingMessageRepository(PendingMessageRepositoryOptions? options = null)
        : this(GetFilePath(options)) { }

    /// <summary>
    /// Creates a new repository using the specified file path.
    /// </summary>
    /// <param name="filePath">Path to the file that stores pending messages.</param>
    public FilePendingMessageRepository(string filePath) {
        this.filePath = filePath;
        if (File.Exists(filePath)) {
            BuildIndex();
        }
    }

    private static string GetFilePath(PendingMessageRepositoryOptions? options) {
        options ??= new PendingMessageRepositoryOptions();
        var directory = string.IsNullOrWhiteSpace(options.DirectoryPath) ? Path.GetTempPath() : options.DirectoryPath;
        string name;
        try {
            name = options.FileNamingScheme?.Invoke() ?? "pending.log";
        } catch (Exception ex) {
            throw new InvalidOperationException("FileNamingScheme failed to provide a file name", ex);
        }
        return Path.Combine(directory, name);
    }

    private void BuildIndex() {
        index.Clear();
        dirtyEntryCount = 0;

        if (!File.Exists(filePath)) {
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

        using var write = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
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
        var orderedIds = new List<string>();

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
                        if (!records.ContainsKey(entry.MessageId)) {
                            orderedIds.Add(entry.MessageId);
                        }
                        records[entry.MessageId] = entry.Record;
                        break;
                    case LogEntryKind.Tombstone:
                        records.Remove(entry.MessageId);
                        orderedIds.RemoveAll(id => string.Equals(id, entry.MessageId, StringComparison.OrdinalIgnoreCase));
                        break;
                }
            }
        }

        var newIndex = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        try {
            using (var write = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) {
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

            if (File.Exists(filePath)) {
                File.Delete(filePath);
            }

            File.Move(temp, filePath);

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
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

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
        } finally {
            gate.Release();
        }
    }

    /// <summary>Attempts to lease a due pending message for processing.</summary>
    public async Task<PendingMessageRecord?> TryAcquireLeaseAsync(
        string messageId,
        DateTimeOffset dueBeforeOrAt,
        DateTimeOffset leaseUntil,
        CancellationToken cancellationToken = default) {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var current = await GetByMessageIdCoreAsync(messageId, cancellationToken).ConfigureAwait(false);
            if (current == null || current.NextAttemptAt > dueBeforeOrAt) {
                return null;
            }

            _ = current.ProviderData;
            current.NextAttemptAt = leaseUntil;
            var offset = await AppendEnvelopeAsync(CreateUpsertEnvelope(current), cancellationToken).ConfigureAwait(false);
            dirtyEntryCount++;
            index[current.MessageId] = offset;

            await CompactIfNeededAsync(cancellationToken).ConfigureAwait(false);
            return current;
        } catch (FileNotFoundException) {
            return null;
        } catch (DirectoryNotFoundException) {
            return null;
        } finally {
            gate.Release();
        }
    }

    /// <summary>Retrieves a pending message by its ID.</summary>
    public async Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) {
        if (!File.Exists(filePath)) {
            return null;
        }
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
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
        if (!File.Exists(filePath)) {
            yield break;
        }
        var snapshot = new List<PendingMessageRecord>();

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var orderedIds = new List<string>();
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
                        if (!recordsById.ContainsKey(entry.MessageId)) {
                            orderedIds.Add(entry.MessageId);
                        }

                        recordsById[entry.MessageId] = entry.Record;
                        break;
                    case LogEntryKind.Tombstone:
                        recordsById.Remove(entry.MessageId);
                        orderedIds.RemoveAll(id => string.Equals(id, entry.MessageId, StringComparison.OrdinalIgnoreCase));
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
        } finally {
            gate.Release();
        }
    }
}
