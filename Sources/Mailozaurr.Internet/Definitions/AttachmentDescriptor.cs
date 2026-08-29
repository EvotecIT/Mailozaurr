namespace Mailozaurr.Definitions;

using MimeKit;
using MimeKit.Utils;

/// <summary>
/// Base descriptor describing an attachment that can be added to outbound messages.
/// </summary>
public abstract class AttachmentDescriptor {
    /// <summary>Known content length, or null when it cannot be determined without reading.</summary>
    public virtual long? Length => null;

    /// <summary>
    /// Gets or sets the file name used for the attachment.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Gets or sets the MIME type applied to the attachment.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the content identifier for inline attachments.
    /// </summary>
    public string? ContentId { get; set; }

    /// <summary>
    /// Gets or sets the content description header value.
    /// </summary>
    public string? ContentDescription { get; set; }

    /// <summary>
    /// Gets or sets the content disposition applied to the attachment.
    /// </summary>
    public ContentDisposition? ContentDisposition { get; set; }

    /// <summary>
    /// Gets or sets the transfer encoding applied to the attachment.
    /// </summary>
    public ContentEncoding? TransferEncoding { get; set; }

    /// <summary>
    /// Gets or sets any additional headers that should be added to the attachment.
    /// </summary>
    public IDictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Gets the source path associated with the attachment if one exists.
    /// </summary>
    internal virtual string? SourcePath => null;

    /// <summary>
    /// Creates a <see cref="MimeEntity"/> representing the attachment.
    /// </summary>
    /// <param name="inline">When set to <c>true</c>, the descriptor will be treated as an inline resource.</param>
    /// <returns>The created <see cref="MimeEntity"/>.</returns>
    internal virtual MimeEntity CreateMimeEntity(bool inline) {
        var stream = OpenContentStream();
        var mediaType = !string.IsNullOrWhiteSpace(ContentType)
            ? ContentType
            : !string.IsNullOrWhiteSpace(FileName)
                ? MimeTypes.GetMimeType(FileName!)
                : "application/octet-stream";

        var part = new MimePart(mediaType ?? "application/octet-stream") {
            Content = new MimeContent(stream),
            FileName = FileName,
            ContentTransferEncoding = TransferEncoding.GetValueOrDefault(ContentEncoding.Base64),
        };

        var disposition = ContentDisposition ?? new ContentDisposition(
            inline ? ContentDisposition.Inline : ContentDisposition.Attachment);
        part.ContentDisposition = disposition;

        var contentId = ContentId;
        if (inline) {
            if (string.IsNullOrWhiteSpace(contentId)) {
                contentId = !string.IsNullOrWhiteSpace(FileName)
                    ? FileName
                    : MimeUtils.GenerateMessageId();
            }
        }

        if (!string.IsNullOrWhiteSpace(contentId)) {
            part.ContentId = contentId;
        }

        if (!string.IsNullOrWhiteSpace(ContentDescription)) {
            part.ContentDescription = ContentDescription;
        }

        if (Headers != null) {
            foreach (var header in Headers) {
                if (!string.IsNullOrWhiteSpace(header.Key) && header.Value is not null) {
                    part.Headers[header.Key] = header.Value;
                }
            }
        }

        return part;
    }

    /// <summary>
    /// Returns the attachment content as a stream positioned at the beginning.
    /// </summary>
    /// <returns>A readable stream containing the attachment content.</returns>
    protected abstract Stream CreateContentStream();

    /// <summary>Asynchronously creates a fresh readable content stream.</summary>
    protected virtual Task<Stream> CreateContentStreamAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateContentStream());
    }

    /// <summary>Opens a new readable attachment stream. The caller owns the returned stream.</summary>
    public Stream OpenContentStream() => ValidateReadableStream(CreateContentStream());

    /// <summary>Asynchronously opens a new readable attachment stream.</summary>
    public async Task<Stream> OpenContentStreamAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = await CreateContentStreamAsync(cancellationToken).ConfigureAwait(false);
        return ValidateReadableStream(stream);
    }

    /// <summary>
    /// Returns the attachment content as a byte array.
    /// </summary>
    /// <returns>Attachment content represented as a byte array.</returns>
    internal virtual byte[] GetContentBytes() {
        return GetContentBytes(AttachmentStreamStagingOptions.DefaultMaxBytes, expectedLength: Length);
    }

    internal virtual byte[] GetContentBytes(long maxBytes, long? expectedLength) {
        if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        if (expectedLength is < 0) throw new ArgumentOutOfRangeException(nameof(expectedLength));
        if (expectedLength > maxBytes) {
            throw new InvalidDataException($"Attachment content exceeds the {maxBytes} byte materialization limit.");
        }

        using var stream = OpenContentStream();
        int capacity = expectedLength is > 0
            ? (int)Math.Min(expectedLength.Value, 64 * 1024)
            : 0;
        using var memory = capacity > 0 ? new MemoryStream(capacity) : new MemoryStream();
        var buffer = new byte[64 * 1024];
        long total = 0;
        while (true) {
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0) break;
            total = checked(total + read);
            if (total > maxBytes) {
                throw new InvalidDataException($"Attachment content exceeds the {maxBytes} byte materialization limit.");
            }
            if (expectedLength.HasValue && total > expectedLength.Value) {
                throw ContentLengthMismatch(expectedLength.Value, total);
            }
            memory.Write(buffer, 0, read);
        }
        if (expectedLength.HasValue && total != expectedLength.Value) {
            throw ContentLengthMismatch(expectedLength.Value, total);
        }
        return memory.ToArray();
    }

    internal long MeasureContentLength(long maxBytes) {
        if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        using var stream = OpenContentStream();
        var buffer = new byte[64 * 1024];
        long total = 0;
        while (true) {
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0) return total;
            total = checked(total + read);
            if (total > maxBytes) {
                throw new InvalidDataException($"Attachment content exceeds the {maxBytes} byte read limit.");
            }
        }
    }

    private static InvalidDataException ContentLengthMismatch(long declared, long observed) =>
        new InvalidDataException(
            $"Attachment content length changed while materializing content (declared {declared}, observed {observed}).");

    private static Stream ValidateReadableStream(Stream? stream) {
        if (stream != null && stream.CanRead) return stream;
        stream?.Dispose();
        throw new InvalidDataException("The attachment content source did not return a readable stream.");
    }
}

/// <summary>
/// Descriptor that sources attachment content from a file on disk.
/// </summary>
public sealed class FileAttachmentDescriptor : AttachmentDescriptor {
    /// <summary>
    /// Initializes a new instance of the <see cref="FileAttachmentDescriptor"/> class.
    /// </summary>
    /// <param name="filePath">Path to the file providing the attachment content.</param>
    public FileAttachmentDescriptor(string filePath) {
        if (string.IsNullOrWhiteSpace(filePath)) {
            throw new ArgumentException("File path must be provided.", nameof(filePath));
        }

        FilePath = filePath;
        FileName ??= Path.GetFileName(filePath);
    }

    /// <summary>
    /// Gets the file path supplying the attachment content.
    /// </summary>
    public string FilePath { get; }

    /// <inheritdoc />
    public override long? Length => new FileInfo(FilePath).Length;

    internal override string? SourcePath => FilePath;

    /// <inheritdoc />
    protected override Stream CreateContentStream() {
        return new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
            bufferSize: 64 * 1024, useAsync: false);
    }

    /// <inheritdoc />
    protected override Task<Stream> CreateContentStreamAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
            bufferSize: 64 * 1024, useAsync: true);
        return Task.FromResult(stream);
    }
}

/// <summary>
/// Descriptor that sources attachment content from a <see cref="Stream"/>.
/// </summary>
public sealed class StreamAttachmentDescriptor : AttachmentDescriptor, IDisposable {
    private readonly Stream _stream;
    private readonly bool _leaveStreamOpen;
    private readonly AttachmentStreamStagingOptions _stagingOptions;
    private readonly object _materializationLock = new();
    private byte[]? _buffer;
    private string? _stagedFilePath;
    private long? _materializedLength;
    private bool _sendAttempted;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamAttachmentDescriptor"/> class.
    /// </summary>
    /// <param name="stream">Readable stream that provides the attachment content.</param>
    /// <param name="fileName">File name to associate with the attachment.</param>
    /// <param name="leaveStreamOpen">Whether the provided stream should remain open after being read.</param>
    /// <param name="stagingOptions">Optional memory, temporary-file, and maximum-size staging limits.</param>
    public StreamAttachmentDescriptor(
        Stream stream,
        string fileName,
        bool leaveStreamOpen = true,
        AttachmentStreamStagingOptions? stagingOptions = null) {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        if (!stream.CanRead) {
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        }

        if (string.IsNullOrWhiteSpace(fileName)) {
            throw new ArgumentException("File name must be provided.", nameof(fileName));
        }

        FileName = fileName;
        _leaveStreamOpen = leaveStreamOpen;
        _stagingOptions = (stagingOptions ?? new AttachmentStreamStagingOptions()).CloneAndValidate();
    }

    /// <inheritdoc />
    public override long? Length {
        get {
            lock (_materializationLock) {
                if (_materializedLength.HasValue) return _materializedLength;
                if (!_stream.CanSeek) return null;
                try {
                    return _stream.Length;
                } catch (ObjectDisposedException) {
                    return null;
                } catch (NotSupportedException) {
                    return null;
                }
            }
        }
    }

    internal bool ReleaseAfterSend {
        get {
            lock (_materializationLock) {
                return _sendAttempted && !_stagingOptions.RetainStagedContentAfterSend;
            }
        }
    }

    internal void MarkSendAttempted() {
        lock (_materializationLock) {
            ThrowIfDisposed();
            _sendAttempted = true;
        }
    }

    /// <inheritdoc />
    protected override Stream CreateContentStream() {
        lock (_materializationLock) {
            ThrowIfDisposed();
            EnsureMaterialized();
            if (_buffer != null) return new MemoryStream(_buffer, writable: false);
            return new FileStream(_stagedFilePath!, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                bufferSize: 64 * 1024, useAsync: false);
        }
    }

    /// <summary>Deletes any temporary staging file and optionally closes the supplied source stream.</summary>
    public void Dispose() {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void EnsureMaterialized() {
        if (_buffer != null || _stagedFilePath != null) return;
        if (_stream.CanSeek) _stream.Position = 0;

        MemoryStream? memory = new MemoryStream((int)Math.Min(_stagingOptions.MemoryThresholdBytes, 64 * 1024));
        FileStream? staged = null;
        string? stagedPath = null;
        long total = 0;
        var copyBuffer = new byte[64 * 1024];
        try {
            while (true) {
                int read = _stream.Read(copyBuffer, 0, copyBuffer.Length);
                if (read == 0) break;
                total = checked(total + read);
                if (total > _stagingOptions.MaxBytes) {
                    throw new InvalidDataException(
                        $"Attachment stream exceeded the {_stagingOptions.MaxBytes} byte staging limit.");
                }
                if (staged == null && total > _stagingOptions.MemoryThresholdBytes) {
                    staged = CreateStagingFile(out stagedPath);
                    memory!.Position = 0;
                    memory.CopyTo(staged);
                    memory.Dispose();
                    memory = null;
                }
                (staged as Stream ?? memory!).Write(copyBuffer, 0, read);
            }

            if (staged != null) {
                staged.Flush();
                staged.Dispose();
                staged = null;
                _stagedFilePath = stagedPath;
            } else {
                _buffer = memory!.ToArray();
            }
            _materializedLength = total;
        } catch {
            staged?.Dispose();
            if (stagedPath != null) TryDeleteStagedFile(stagedPath);
            throw;
        } finally {
            memory?.Dispose();
            if (_stream.CanSeek) {
                try {
                    _stream.Position = 0;
                } catch (ObjectDisposedException) {
                    // Staged content remains independent of the original stream.
                }
            }
            if (!_leaveStreamOpen) _stream.Dispose();
        }
    }

    private FileStream CreateStagingFile(out string path) {
        string directory = string.IsNullOrWhiteSpace(_stagingOptions.TempDirectory)
            ? Path.Combine(Path.GetTempPath(), "Mailozaurr", "attachments")
            : Path.GetFullPath(_stagingOptions.TempDirectory!);
        Directory.CreateDirectory(directory);
        UnixFilePermissions.RestrictDirectory(directory);
        for (int attempt = 0; attempt < 32; attempt++) {
            path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            try {
                var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete,
                    bufferSize: 64 * 1024, useAsync: false);
                UnixFilePermissions.RestrictFile(path);
                return stream;
            } catch (IOException) when (attempt < 31) {
                // Extremely unlikely random-name collision; retry with a fresh name.
            }
        }
        throw new IOException("Unable to create a unique attachment staging file.");
    }

    private void Dispose(bool disposing) {
        lock (_materializationLock) {
            if (_disposed) return;
            _disposed = true;
            if (disposing && !_leaveStreamOpen) _stream.Dispose();
            if (_stagedFilePath != null) TryDeleteStagedFile(_stagedFilePath);
            _stagedFilePath = null;
            _buffer = null;
        }
    }

    private void ThrowIfDisposed() {
        if (_disposed) throw new ObjectDisposedException(nameof(StreamAttachmentDescriptor));
    }

    private static void TryDeleteStagedFile(string path) {
        try {
            File.Delete(path);
        } catch (IOException) {
            // Best effort during disposal/finalization; active reader streams may still own the file.
        } catch (UnauthorizedAccessException) {
            // Best effort during disposal/finalization.
        }
    }

    /// <summary>Finalizer removes abandoned temporary staging files.</summary>
    ~StreamAttachmentDescriptor() => Dispose(disposing: false);
}

internal static class AttachmentDescriptorLifetime {
    internal static void MarkSendAttempted(params IEnumerable<AttachmentDescriptor>?[] collections) {
        var marked = new HashSet<StreamAttachmentDescriptor>();
        foreach (IEnumerable<AttachmentDescriptor>? collection in collections) {
            if (collection == null) continue;
            foreach (StreamAttachmentDescriptor descriptor in collection.OfType<StreamAttachmentDescriptor>()) {
                if (marked.Add(descriptor)) descriptor.MarkSendAttempted();
            }
        }
    }

    internal static void ReleaseStaging(params IEnumerable<AttachmentDescriptor>?[] collections) {
        var released = new HashSet<StreamAttachmentDescriptor>();
        foreach (IEnumerable<AttachmentDescriptor>? collection in collections) {
            if (collection == null) continue;
            foreach (StreamAttachmentDescriptor descriptor in collection.OfType<StreamAttachmentDescriptor>()) {
                if (descriptor.ReleaseAfterSend && released.Add(descriptor)) descriptor.Dispose();
            }
        }
    }
}

/// <summary>
/// Descriptor that sources attachment content from a byte array.
/// </summary>
public sealed class ByteArrayAttachmentDescriptor : AttachmentDescriptor {
    private readonly byte[] _buffer;
    private readonly bool _cloneBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ByteArrayAttachmentDescriptor"/> class.
    /// </summary>
    /// <param name="buffer">Byte array containing the attachment data.</param>
    /// <param name="fileName">File name to associate with the attachment.</param>
    /// <param name="cloneBuffer">If <c>true</c>, the buffer will be cloned to prevent external mutations.</param>
    public ByteArrayAttachmentDescriptor(byte[] buffer, string fileName, bool cloneBuffer = true) {
        buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        if (string.IsNullOrWhiteSpace(fileName)) {
            throw new ArgumentException("File name must be provided.", nameof(fileName));
        }

        _buffer = buffer.Length == 0 ? Array.Empty<byte>() : buffer;
        _cloneBuffer = cloneBuffer;
        FileName = fileName;
    }

    /// <inheritdoc />
    public override long? Length => _buffer.LongLength;

    /// <inheritdoc />
    protected override Stream CreateContentStream() {
        var data = _cloneBuffer ? (byte[])_buffer.Clone() : _buffer;
        return new MemoryStream(data, writable: false);
    }
}

/// <summary>
/// Descriptor that wraps an existing <see cref="MimeEntity"/> instance.
/// </summary>
public sealed class MimeEntityAttachmentDescriptor : AttachmentDescriptor {
    private readonly MimeEntity _entity;

    /// <summary>
    /// Initializes a new instance of the <see cref="MimeEntityAttachmentDescriptor"/> class.
    /// </summary>
    /// <param name="entity">The MIME entity to attach.</param>
    public MimeEntityAttachmentDescriptor(MimeEntity entity) {
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }

    internal override MimeEntity CreateMimeEntity(bool inline) {
        if (_entity is MimePart part) {
            if (!string.IsNullOrWhiteSpace(FileName)) {
                part.FileName = FileName;
            }

            if (ContentDisposition != null) {
                part.ContentDisposition = ContentDisposition;
            } else if (inline && part.ContentDisposition == null) {
                part.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
            }

            if (!string.IsNullOrWhiteSpace(ContentDescription)) {
                part.ContentDescription = ContentDescription;
            }

            if (TransferEncoding.HasValue) {
                part.ContentTransferEncoding = TransferEncoding.Value;
            }

            if (Headers != null) {
                foreach (var header in Headers) {
                    if (!string.IsNullOrWhiteSpace(header.Key) && header.Value is not null) {
                        part.Headers[header.Key] = header.Value;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(part.ContentId)) {
                if (!string.IsNullOrWhiteSpace(ContentId)) {
                    part.ContentId = ContentId;
                } else if (inline) {
                    part.ContentId = MimeUtils.GenerateMessageId();
                }
            }
        }

        return _entity;
    }

    /// <summary>
    /// Throwing override — a <see cref="MimeEntityAttachmentDescriptor"/> does not expose a raw content stream.
    /// </summary>
    protected override Stream CreateContentStream() => throw new NotSupportedException("MimeEntity attachments do not expose a content stream.");

    internal override byte[] GetContentBytes() => throw new NotSupportedException("MimeEntity attachments cannot be converted to raw bytes.");
}
