namespace Mailozaurr;

/// <summary>
/// Stores sent message records in a single newline-delimited JSON file.
/// </summary>
public sealed class FileSentMessageRepository : ISentMessageRepository {
    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, long> index = new(StringComparer.OrdinalIgnoreCase);
    private static readonly byte[] NewlineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);

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
        index.Clear();
        try {
            foreach (var entry in LogFileLineReader.ReadLinesWithOffsets(filePath)) {
                var line = entry.Line;
                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }

                if (TryDeserializeRecord(line, out var record) && !string.IsNullOrEmpty(record!.MessageId)) {
                    index[record.MessageId] = entry.Offset;
                }
            }
        } catch (FileNotFoundException) {
            index.Clear();
        } catch (DirectoryNotFoundException) {
            index.Clear();
        }
    }

    private static bool TryDeserializeRecord(string json, out SentMessageRecord? record) {
        record = null;
        if (string.IsNullOrWhiteSpace(json)) {
            return false;
        }

        try {
            record = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.SentMessageRecord);
            return record != null;
        } catch (JsonException) {
            return false;
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
            await write.WriteAsync(NewlineBytes, 0, NewlineBytes.Length, cancellationToken);
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
            if (TryDeserializeRecord(line, out var record) && string.Equals(record!.MessageId, messageId, StringComparison.OrdinalIgnoreCase)) {
                return record;
            }
            return null;
        } catch (FileNotFoundException) {
            return null;
        } catch (DirectoryNotFoundException) {
            return null;
        } finally {
            gate.Release();
        }
    }
}
