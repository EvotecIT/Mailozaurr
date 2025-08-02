namespace Mailozaurr;

/// <summary>
/// Stores sent message records in a single newline-delimited JSON file.
/// </summary>
public sealed class FileSentMessageRepository : ISentMessageRepository {
    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>
    /// Creates a new repository using the specified file path.
    /// </summary>
    /// <param name="filePath">Path to the log file that stores sent message records.</param>
    public FileSentMessageRepository(string filePath) => this.filePath = filePath;

    /// <summary>Saves a record to the log file.</summary>
    public async Task SaveAsync(SentMessageRecord record, CancellationToken cancellationToken = default) {
        await gate.WaitAsync(cancellationToken);
        try {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }
            using var write = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            await JsonSerializer.SerializeAsync(write, record, cancellationToken: cancellationToken);
            var newline = Encoding.UTF8.GetBytes(Environment.NewLine);
            await write.WriteAsync(newline, 0, newline.Length, cancellationToken);
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
            using var read = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(read);
            while (!reader.EndOfStream) {
                cancellationToken.ThrowIfCancellationRequested();
                string? line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }
                var record = JsonSerializer.Deserialize<SentMessageRecord>(line);
                if (record != null && string.Equals(record.MessageId, messageId, StringComparison.OrdinalIgnoreCase)) {
                    return record;
                }
            }
            return null;
        } finally {
            gate.Release();
        }
    }
}
