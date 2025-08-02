namespace Mailozaurr;

public sealed class FileSentMessageRepository : ISentMessageRepository {
    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public FileSentMessageRepository(string filePath) => this.filePath = filePath;

    public async Task SaveAsync(SentMessageRecord record, CancellationToken cancellationToken = default) {
        await gate.WaitAsync(cancellationToken);
        try {
            List<SentMessageRecord> records;
            if (File.Exists(filePath)) {
                using var read = File.OpenRead(filePath);
                records = await JsonSerializer.DeserializeAsync<List<SentMessageRecord>>(read, cancellationToken: cancellationToken) ?? new List<SentMessageRecord>();
            } else {
                records = new List<SentMessageRecord>();
            }
            records.Add(record);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }
            using var write = File.Create(filePath);
            await JsonSerializer.SerializeAsync(write, records, cancellationToken: cancellationToken);
        } finally {
            gate.Release();
        }
    }

    public async Task<SentMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) {
        if (!File.Exists(filePath)) {
            return null;
        }
        await gate.WaitAsync(cancellationToken);
        try {
            using var read = File.OpenRead(filePath);
            var records = await JsonSerializer.DeserializeAsync<List<SentMessageRecord>>(read, cancellationToken: cancellationToken);
            return records?.FirstOrDefault(r => string.Equals(r.MessageId, messageId, StringComparison.OrdinalIgnoreCase));
        } finally {
            gate.Release();
        }
    }
}
