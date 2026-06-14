namespace Mailozaurr.Definitions;

using MimeKit;
using MimeKit.Utils;

/// <summary>
/// Base descriptor describing an attachment that can be added to outbound messages.
/// </summary>
public abstract class AttachmentDescriptor {
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
        var stream = CreateContentStream();
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

    /// <summary>
    /// Returns the attachment content as a byte array.
    /// </summary>
    /// <returns>Attachment content represented as a byte array.</returns>
    internal virtual byte[] GetContentBytes() {
        using var stream = CreateContentStream();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
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

    internal override string? SourcePath => FilePath;

    /// <inheritdoc />
    protected override Stream CreateContentStream() {
        var data = File.ReadAllBytes(FilePath);
        return new MemoryStream(data, writable: false);
    }
}

/// <summary>
/// Descriptor that sources attachment content from a <see cref="Stream"/>.
/// </summary>
public sealed class StreamAttachmentDescriptor : AttachmentDescriptor {
    private readonly Stream _stream;
    private readonly bool _leaveStreamOpen;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamAttachmentDescriptor"/> class.
    /// </summary>
    /// <param name="stream">Readable stream that provides the attachment content.</param>
    /// <param name="fileName">File name to associate with the attachment.</param>
    /// <param name="leaveStreamOpen">Whether the provided stream should remain open after being read.</param>
    public StreamAttachmentDescriptor(Stream stream, string fileName, bool leaveStreamOpen = true) {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        if (!stream.CanRead) {
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        }

        if (string.IsNullOrWhiteSpace(fileName)) {
            throw new ArgumentException("File name must be provided.", nameof(fileName));
        }

        FileName = fileName;
        _leaveStreamOpen = leaveStreamOpen;
    }

    /// <inheritdoc />
    protected override Stream CreateContentStream() {
        if (_stream.CanSeek) {
            _stream.Position = 0;
        }

        var memory = new MemoryStream();
        _stream.CopyTo(memory);
        memory.Position = 0;

        if (_stream.CanSeek) {
            try {
                _stream.Position = 0;
            } catch (ObjectDisposedException) {
                // Ignore - stream may have been disposed externally.
            }
        }

        if (!_leaveStreamOpen) {
            _stream.Dispose();
        }

        return memory;
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