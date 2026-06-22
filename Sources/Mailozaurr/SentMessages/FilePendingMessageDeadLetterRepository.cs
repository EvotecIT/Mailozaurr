using System;
using System.Collections.Generic;
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
    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly byte[] NewlineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);

    /// <summary>Creates a repository using pending-message options and a dead-letter file name.</summary>
    public FilePendingMessageDeadLetterRepository(PendingMessageRepositoryOptions? options = null)
        : this(GetFilePath(options)) {
    }

    /// <summary>Creates a repository using an explicit file path.</summary>
    public FilePendingMessageDeadLetterRepository(string filePath) {
        this.filePath = Path.GetFullPath(filePath ?? throw new ArgumentNullException(nameof(filePath)));
    }

    private static string GetFilePath(PendingMessageRepositoryOptions? options) {
        options ??= new PendingMessageRepositoryOptions();
        var directory = string.IsNullOrWhiteSpace(options.DirectoryPath) ? Path.GetTempPath() : options.DirectoryPath;
        return Path.Combine(directory, "dead-letter.log");
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
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory)) {
                Directory.CreateDirectory(directory);
            }

            var payload = JsonSerializer.SerializeToUtf8Bytes(record, MailozaurrJsonContext.Default.PendingMessageDeadLetterRecord);
            using var write = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            await write.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
            await write.WriteAsync(NewlineBytes, 0, NewlineBytes.Length, cancellationToken).ConfigureAwait(false);
            await write.FlushAsync(cancellationToken).ConfigureAwait(false);
        } finally {
            gate.Release();
        }
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
        if (!File.Exists(filePath)) {
            yield break;
        }

        List<PendingMessageDeadLetterRecord> snapshot = new();
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

                try {
                    var record = JsonSerializer.Deserialize(line, MailozaurrJsonContext.Default.PendingMessageDeadLetterRecord);
                    if (record != null && !string.IsNullOrWhiteSpace(record.Message.MessageId)) {
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

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
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

                    if (record != null && !string.Equals(record.Message.MessageId, messageId, StringComparison.OrdinalIgnoreCase)) {
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

        var tempPath = filePath + ".tmp";
        var backupPath = filePath + ".bak";
        try {
            using (var write = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                foreach (var record in records) {
                    var payload = JsonSerializer.SerializeToUtf8Bytes(record, MailozaurrJsonContext.Default.PendingMessageDeadLetterRecord);
                    await write.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
                    await write.WriteAsync(NewlineBytes, 0, NewlineBytes.Length, cancellationToken).ConfigureAwait(false);
                }

                await write.FlushAsync(cancellationToken).ConfigureAwait(false);
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
}
