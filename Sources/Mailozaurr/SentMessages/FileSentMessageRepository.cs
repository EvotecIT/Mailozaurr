namespace Mailozaurr;

/// <summary>
/// Stores sent message records in a single newline-delimited JSON file.
/// </summary>
public sealed class FileSentMessageRepository : ISentMessageRepository {
    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, long> index = new(StringComparer.OrdinalIgnoreCase);
    private readonly byte[] newlineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);

    /// <summary>
    /// Creates a new repository using the specified file path.
    /// </summary>
    /// <param name="filePath">Path to the log file that stores sent message records.</param>
    public FileSentMessageRepository(string filePath) {
        this.filePath = filePath;
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
            var record = JsonSerializer.Deserialize(line, MailozaurrJsonContext.Default.SentMessageRecord);
            if (record != null && !string.IsNullOrEmpty(record.MessageId)) {
                index[record.MessageId] = position;
            }
            position += Encoding.UTF8.GetByteCount(line) + newlineBytes.Length;
        }
    }

    /// <summary>Saves a record to the log file.</summary>
    public async Task SaveAsync(SentMessageRecord record, CancellationToken cancellationToken = default) {
        await gate.WaitAsync(cancellationToken);
        try {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }
            using var write = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            var offset = write.Position;
            await JsonSerializer.SerializeAsync(write, record, MailozaurrJsonContext.Default.SentMessageRecord, cancellationToken);
            await write.WriteAsync(newlineBytes, 0, newlineBytes.Length, cancellationToken);
            index[record.MessageId] = offset;
        } finally {
            gate.Release();
        }
    }

    /// <summary>Retrieves a record by message ID from the log file.</summary>
    public async Task<SentMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) {
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
            using var reader = new StreamReader(read, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            string? line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) {
                return null;
            }
            var record = JsonSerializer.Deserialize(line, MailozaurrJsonContext.Default.SentMessageRecord);
            if (record != null && string.Equals(record.MessageId, messageId, StringComparison.OrdinalIgnoreCase)) {
                return record;
            }
            return null;
        } finally {
            gate.Release();
        }
    }
}
