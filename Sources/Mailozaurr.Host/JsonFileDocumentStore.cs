using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Mailozaurr.Hosting;

/// <summary>Coordinates typed whole-document JSON persistence across instances and processes.</summary>
internal sealed class JsonFileDocumentStore<TDocument> where TDocument : class {
    private static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(25);
    private readonly string _filePath;
    private readonly string _invalidPathMessage;
    private readonly string _lockPath;
    private readonly TimeSpan _lockTimeout;
    private readonly JsonTypeInfo<TDocument> _jsonTypeInfo;
    private readonly Func<TDocument> _createDocument;
    private readonly Action<TDocument>? _normalizeDocument;
    private readonly SemaphoreSlim _writerGate;

    internal JsonFileDocumentStore(
        string filePath,
        string invalidPathMessage,
        JsonTypeInfo<TDocument> jsonTypeInfo,
        Func<TDocument> createDocument,
        Action<TDocument>? normalizeDocument = null,
        TimeSpan? lockTimeout = null) {
        _filePath = Path.GetFullPath(filePath ?? throw new ArgumentNullException(nameof(filePath)));
        _invalidPathMessage = invalidPathMessage ?? throw new ArgumentNullException(nameof(invalidPathMessage));
        _jsonTypeInfo = jsonTypeInfo ?? throw new ArgumentNullException(nameof(jsonTypeInfo));
        _createDocument = createDocument ?? throw new ArgumentNullException(nameof(createDocument));
        _normalizeDocument = normalizeDocument;
        _lockTimeout = lockTimeout ?? DefaultLockTimeout;
        if (_lockTimeout <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(lockTimeout), "A positive lock timeout is required.");
        }
        _writerGate = JsonFileDocumentStoreCoordinator.GetWriterGate(_filePath);

        string? directory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrWhiteSpace(directory)) {
            throw new InvalidOperationException(_invalidPathMessage);
        }
        _lockPath = Path.Combine(directory, ".mailozaurr-locks", Path.GetFileName(_filePath) + ".lock");
    }

    internal Task<TResult> ReadAsync<TResult>(
        Func<TDocument, TResult> read,
        CancellationToken cancellationToken = default) {
        if (read == null) throw new ArgumentNullException(nameof(read));
        return ExecuteReadAsync(read, cancellationToken);
    }

    internal Task UpdateAsync(Action<TDocument> update, CancellationToken cancellationToken = default) {
        if (update == null) throw new ArgumentNullException(nameof(update));
        return ExecuteUpdateAsync(document => {
            update(document);
            return Task.FromResult(true);
        }, cancellationToken);
    }

    internal Task<bool> RemoveAsync(Func<TDocument, bool> remove, CancellationToken cancellationToken = default) {
        if (remove == null) throw new ArgumentNullException(nameof(remove));
        return ExecuteConditionalUpdateAsync(remove, cancellationToken);
    }

    internal Task<TResult> ExecuteUnderWriterLockAsync<TResult>(
        Func<TDocument, Task<TResult>> operation,
        CancellationToken cancellationToken = default) {
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        return ExecuteLockedReadAsync(operation, cancellationToken);
    }

    private async Task<bool> ExecuteConditionalUpdateAsync(
        Func<TDocument, bool> update,
        CancellationToken cancellationToken) {
        var elapsed = Stopwatch.StartNew();
        await AcquireWriterGateAsync(elapsed, cancellationToken).ConfigureAwait(false);
        try {
            using FileStream fileLock = await AcquireFileLockAsync(elapsed, cancellationToken).ConfigureAwait(false);
            TDocument document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            bool changed = update(document);
            if (changed) {
                await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            }
            return changed;
        } finally {
            _writerGate.Release();
        }
    }

    private async Task<TResult> ExecuteReadAsync<TResult>(
        Func<TDocument, TResult> read,
        CancellationToken cancellationToken) {
        TDocument document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
        return read(document);
    }

    private async Task<TResult> ExecuteLockedReadAsync<TResult>(
        Func<TDocument, Task<TResult>> operation,
        CancellationToken cancellationToken) {
        var elapsed = Stopwatch.StartNew();
        await AcquireWriterGateAsync(elapsed, cancellationToken).ConfigureAwait(false);
        try {
            using FileStream fileLock = await AcquireFileLockAsync(elapsed, cancellationToken).ConfigureAwait(false);
            TDocument document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            return await operation(document).ConfigureAwait(false);
        } finally {
            _writerGate.Release();
        }
    }

    private async Task<TResult> ExecuteUpdateAsync<TResult>(
        Func<TDocument, Task<TResult>> update,
        CancellationToken cancellationToken) {
        var elapsed = Stopwatch.StartNew();
        await AcquireWriterGateAsync(elapsed, cancellationToken).ConfigureAwait(false);
        try {
            using FileStream fileLock = await AcquireFileLockAsync(elapsed, cancellationToken).ConfigureAwait(false);
            TDocument document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            TResult result = await update(document).ConfigureAwait(false);
            await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            return result;
        } finally {
            _writerGate.Release();
        }
    }

    private async Task AcquireWriterGateAsync(Stopwatch elapsed, CancellationToken cancellationToken) {
        TimeSpan remaining = GetRemainingLockTime(elapsed, lastError: null);
        bool acquired = await _writerGate.WaitAsync(remaining, cancellationToken).ConfigureAwait(false);
        if (!acquired) {
            throw CreateLockTimeoutException(lastError: null);
        }
    }

    private async Task<FileStream> AcquireFileLockAsync(Stopwatch elapsed, CancellationToken cancellationToken) {
        string? lockDirectory = Path.GetDirectoryName(_lockPath);
        if (string.IsNullOrWhiteSpace(lockDirectory)) {
            throw new InvalidOperationException(_invalidPathMessage);
        }
        Directory.CreateDirectory(lockDirectory);

        IOException? lastError = null;
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                return new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            } catch (IOException ex) {
                lastError = ex;
                TimeSpan remaining = GetRemainingLockTime(elapsed, lastError);
                TimeSpan delay = remaining < LockRetryDelay ? remaining : LockRetryDelay;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private TimeSpan GetRemainingLockTime(Stopwatch elapsed, IOException? lastError) {
        TimeSpan remaining = _lockTimeout - elapsed.Elapsed;
        if (remaining <= TimeSpan.Zero) {
            throw CreateLockTimeoutException(lastError);
        }
        return remaining;
    }

    private TimeoutException CreateLockTimeoutException(IOException? lastError) =>
        new($"Timed out after {_lockTimeout.TotalSeconds:0.###} seconds waiting to update '{_filePath}'.", lastError);

    private async Task<TDocument> LoadDocumentAsync(CancellationToken cancellationToken) {
        TDocument document;
        if (!File.Exists(_filePath)) {
            document = _createDocument();
        } else {
            using var stream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            document = await JsonSerializer.DeserializeAsync(stream, _jsonTypeInfo, cancellationToken)
                .ConfigureAwait(false) ?? _createDocument();
        }

        _normalizeDocument?.Invoke(document);
        return document;
    }

    private Task SaveDocumentAsync(TDocument document, CancellationToken cancellationToken) =>
        AtomicFileWriter.WriteAsync(
            _filePath,
            _invalidPathMessage,
            (stream, token) => JsonSerializer.SerializeAsync(stream, document, _jsonTypeInfo, token),
            cancellationToken);
}

/// <summary>Shares writer gates for canonical document paths across store instances in this process.</summary>
internal static class JsonFileDocumentStoreCoordinator {
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> WriterGates =
        new(Path.DirectorySeparatorChar == '\\'
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);

    internal static SemaphoreSlim GetWriterGate(string canonicalPath) =>
        WriterGates.GetOrAdd(canonicalPath, static _ => new SemaphoreSlim(1, 1));
}