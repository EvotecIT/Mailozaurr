using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Mailozaurr.Application;

/// <summary>Coordinates typed whole-document JSON persistence across instances and processes.</summary>
internal sealed class JsonFileDocumentStore<TDocument> where TDocument : class {
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(25);
    private readonly string _filePath;
    private readonly string _invalidPathMessage;
    private readonly string _lockPath;
    private readonly JsonTypeInfo<TDocument> _jsonTypeInfo;
    private readonly Func<TDocument> _createDocument;
    private readonly Action<TDocument>? _normalizeDocument;
    private readonly SemaphoreSlim _gate = new(1, 1);

    internal JsonFileDocumentStore(
        string filePath,
        string invalidPathMessage,
        JsonTypeInfo<TDocument> jsonTypeInfo,
        Func<TDocument> createDocument,
        Action<TDocument>? normalizeDocument = null) {
        _filePath = Path.GetFullPath(filePath ?? throw new ArgumentNullException(nameof(filePath)));
        _invalidPathMessage = invalidPathMessage ?? throw new ArgumentNullException(nameof(invalidPathMessage));
        _jsonTypeInfo = jsonTypeInfo ?? throw new ArgumentNullException(nameof(jsonTypeInfo));
        _createDocument = createDocument ?? throw new ArgumentNullException(nameof(createDocument));
        _normalizeDocument = normalizeDocument;

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
        return ExecuteAsync(document => Task.FromResult(read(document)), save: false, cancellationToken);
    }

    internal Task UpdateAsync(Action<TDocument> update, CancellationToken cancellationToken = default) {
        if (update == null) throw new ArgumentNullException(nameof(update));
        return ExecuteAsync(document => {
            update(document);
            return Task.FromResult(true);
        }, save: true, cancellationToken);
    }

    internal Task<bool> RemoveAsync(Func<TDocument, bool> remove, CancellationToken cancellationToken = default) {
        if (remove == null) throw new ArgumentNullException(nameof(remove));
        return ExecuteConditionalUpdateAsync(remove, cancellationToken);
    }

    private async Task<bool> ExecuteConditionalUpdateAsync(
        Func<TDocument, bool> update,
        CancellationToken cancellationToken) {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            using FileStream fileLock = await AcquireFileLockAsync(cancellationToken).ConfigureAwait(false);
            TDocument document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            bool changed = update(document);
            if (changed) {
                await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            }
            return changed;
        } finally {
            _gate.Release();
        }
    }

    private async Task<TResult> ExecuteAsync<TResult>(
        Func<TDocument, Task<TResult>> action,
        bool save,
        CancellationToken cancellationToken) {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            using FileStream fileLock = await AcquireFileLockAsync(cancellationToken).ConfigureAwait(false);
            TDocument document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            TResult result = await action(document).ConfigureAwait(false);
            if (save) {
                await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            }
            return result;
        } finally {
            _gate.Release();
        }
    }

    private async Task<FileStream> AcquireFileLockAsync(CancellationToken cancellationToken) {
        string? lockDirectory = Path.GetDirectoryName(_lockPath);
        if (string.IsNullOrWhiteSpace(lockDirectory)) {
            throw new InvalidOperationException(_invalidPathMessage);
        }
        Directory.CreateDirectory(lockDirectory);

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                return new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            } catch (IOException) {
                await Task.Delay(LockRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<TDocument> LoadDocumentAsync(CancellationToken cancellationToken) {
        TDocument document;
        if (!File.Exists(_filePath)) {
            document = _createDocument();
        } else {
            using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
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
