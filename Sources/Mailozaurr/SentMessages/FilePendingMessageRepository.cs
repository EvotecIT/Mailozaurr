using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Mailozaurr;

/// <summary>
/// Stores pending message records in a single newline-delimited JSON file.
/// </summary>
public sealed class FilePendingMessageRepository : IPendingMessageRepository {
    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, long> index = new(StringComparer.OrdinalIgnoreCase);
    private readonly byte[] newlineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

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

    private static PendingMessageRecord? DeserializeRecord(string json) {
        if (string.IsNullOrWhiteSpace(json)) {
            return null;
        }
        var record = JsonSerializer.Deserialize<PendingMessageRecord>(json, SerializerOptions);
        if (record != null) {
            _ = record.ProviderData;
        }
        return record;
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
        long position = 0;
        foreach (var line in File.ReadLines(filePath)) {
            if (string.IsNullOrWhiteSpace(line)) {
                position += newlineBytes.Length;
                continue;
            }
            var record = DeserializeRecord(line);
            if (record != null && !string.IsNullOrEmpty(record.MessageId)) {
                index[record.MessageId] = position;
            }
            position += Encoding.UTF8.GetByteCount(line) + newlineBytes.Length;
        }
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
            var payload = JsonSerializer.SerializeToUtf8Bytes(record, SerializerOptions);

            using var write = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            var offset = write.Position;
            await write.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
            await write.WriteAsync(newlineBytes, 0, newlineBytes.Length, cancellationToken).ConfigureAwait(false);
            await write.FlushAsync(cancellationToken).ConfigureAwait(false);

            index[record.MessageId] = offset;
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
            if (!index.TryGetValue(messageId, out var offset)) {
                return null;
            }
            using var read = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            read.Seek(offset, SeekOrigin.Begin);
            using var reader = new StreamReader(read, Encoding.UTF8, false, 1024, leaveOpen: true);
            string? line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line)) {
                return null;
            }
            var record = DeserializeRecord(line);
            if (record != null && string.Equals(record.MessageId, messageId, StringComparison.OrdinalIgnoreCase)) {
                return record;
            }
            return null;
        } finally {
            gate.Release();
        }
    }

    /// <summary>Enumerates all pending messages.</summary>
    public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default) {
        if (!File.Exists(filePath)) {
            yield break;
        }
        var snapshot = new List<PendingMessageRecord>();

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            using var read = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(read, Encoding.UTF8, false, 1024, leaveOpen: true);
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null) {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }

                var record = DeserializeRecord(line);
                if (record != null) {
                    snapshot.Add(record);
                }
            }
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
        if (!File.Exists(filePath)) {
            return;
        }
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if (!index.ContainsKey(messageId)) {
                return;
            }
            var temp = filePath + ".tmp";
            var newIndex = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            using (var read = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
            using (var write = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) {
                using var reader = new StreamReader(read, Encoding.UTF8, false, 1024, leaveOpen: true);
                long position = 0;
                string? line;
                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null) {
                    if (string.IsNullOrWhiteSpace(line)) {
                        await write.WriteAsync(newlineBytes, 0, newlineBytes.Length, cancellationToken).ConfigureAwait(false);
                        position += newlineBytes.Length;
                        continue;
                    }
                    var record = DeserializeRecord(line);
                    if (record == null || string.Equals(record.MessageId, messageId, StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                    var bytes = Encoding.UTF8.GetBytes(line);
                    await write.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
                    await write.WriteAsync(newlineBytes, 0, newlineBytes.Length, cancellationToken).ConfigureAwait(false);
                    newIndex[record.MessageId] = position;
                    position += bytes.Length + newlineBytes.Length;
                }
                await write.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Delete(filePath);
            File.Move(temp, filePath);
            index.Clear();
            foreach (var pair in newIndex) {
                index[pair.Key] = pair.Value;
            }
        } finally {
            gate.Release();
        }
    }
}
