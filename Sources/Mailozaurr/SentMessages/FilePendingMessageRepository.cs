using System.Runtime.CompilerServices;

namespace Mailozaurr;

/// <summary>
/// Stores pending message records in a single newline-delimited JSON file.
/// </summary>
public sealed class FilePendingMessageRepository : IPendingMessageRepository {
    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, long> index = new(StringComparer.OrdinalIgnoreCase);
    private readonly byte[] newlineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);

    /// <summary>
    /// Path to the file storing pending messages.
    /// </summary>
    public string FilePath => filePath;

    /// <summary>
    /// Creates a new repository using the specified file path.
    /// </summary>
    /// <param name="filePath">Path to the file that stores pending messages.</param>
    public FilePendingMessageRepository(string filePath)
        : this(new PendingMessageRepositoryOptions {
            DirectoryPath = Path.GetDirectoryName(filePath),
            FileNameFactory = _ => Path.GetFileName(filePath)
        }) {
    }

    /// <summary>
    /// Creates a new repository using options.
    /// </summary>
    /// <param name="options">Configuration options for the repository.</param>
    public FilePendingMessageRepository(PendingMessageRepositoryOptions? options = null) {
        options ??= new PendingMessageRepositoryOptions();
        var directory = options.DirectoryPath;
        if (string.IsNullOrWhiteSpace(directory)) {
            directory = Path.Combine(Path.GetTempPath(), "Mailozaurr");
        }
        var fileName = options.FileNameFactory?.Invoke(DateTimeOffset.UtcNow) ?? "pending.log";
        filePath = Path.Combine(directory, fileName);
        if (File.Exists(filePath)) {
            BuildIndex();
        }
    }

    private void BuildIndex() {
        long position = 0;
        foreach (var line in File.ReadLines(filePath)) {
            if (string.IsNullOrWhiteSpace(line)) {
                position += newlineBytes.Length;
                continue;
            }
            var record = JsonSerializer.Deserialize<PendingMessageRecord>(line);
            if (record != null && !string.IsNullOrEmpty(record.MessageId)) {
                index[record.MessageId] = position;
            }
            position += Encoding.UTF8.GetByteCount(line) + newlineBytes.Length;
        }
    }

    /// <summary>Saves a pending message to the repository.</summary>
    public async Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default) {
        await gate.WaitAsync(cancellationToken);
        try {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }
            if (record.NextAttemptAt == default) {
                record.NextAttemptAt = DateTimeOffset.UtcNow;
            }
            using var write = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            var offset = write.Position;
            await JsonSerializer.SerializeAsync(write, record, cancellationToken: cancellationToken);
            await write.WriteAsync(newlineBytes, 0, newlineBytes.Length, cancellationToken);
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
        await gate.WaitAsync(cancellationToken);
        try {
            if (!index.TryGetValue(messageId, out var offset)) {
                return null;
            }
            using var read = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            read.Seek(offset, SeekOrigin.Begin);
            using var reader = new StreamReader(read, Encoding.UTF8, false, 1024, leaveOpen: true);
            string? line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) {
                return null;
            }
            var record = JsonSerializer.Deserialize<PendingMessageRecord>(line);
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
        await gate.WaitAsync(cancellationToken);
        try {
            using var read = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(read, Encoding.UTF8, false, 1024, leaveOpen: true);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null) {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }
                var record = JsonSerializer.Deserialize<PendingMessageRecord>(line);
                if (record != null) {
                    yield return record;
                }
            }
        } finally {
            gate.Release();
        }
    }

    /// <summary>Removes a pending message by its ID.</summary>
    public async Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
        if (!File.Exists(filePath)) {
            return;
        }
        await gate.WaitAsync(cancellationToken);
        try {
            if (!index.ContainsKey(messageId)) {
                return;
            }
            var temp = filePath + ".tmp";
            using (var read = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
            using (var write = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) {
                using var reader = new StreamReader(read, Encoding.UTF8, false, 1024, leaveOpen: true);
                long position = 0;
                string? line;
                index.Clear();
                while ((line = await reader.ReadLineAsync()) != null) {
                    if (string.IsNullOrWhiteSpace(line)) {
                        await write.WriteAsync(newlineBytes, 0, newlineBytes.Length, cancellationToken);
                        position += newlineBytes.Length;
                        continue;
                    }
                    var record = JsonSerializer.Deserialize<PendingMessageRecord>(line);
                    if (record == null || string.Equals(record.MessageId, messageId, StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                    var bytes = Encoding.UTF8.GetBytes(line);
                    await write.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                    await write.WriteAsync(newlineBytes, 0, newlineBytes.Length, cancellationToken);
                    index[record.MessageId] = position;
                    position += bytes.Length + newlineBytes.Length;
                }
            }
            File.Delete(filePath);
            File.Move(temp, filePath);
        } finally {
            gate.Release();
        }
    }
}
