using MimeKit;
using MimeKit.Utils;

namespace Mailozaurr;

/// <summary>
/// Describes an attachment to be added to an SMTP message.
/// </summary>
public abstract class SmtpAttachmentDescriptor
{

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpAttachmentDescriptor"/> class.
    /// </summary>
    protected SmtpAttachmentDescriptor()
    {
    }

    /// <summary>
    /// Gets or sets the file name used for the attachment.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Gets or sets the MIME type to apply to the attachment.
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
    /// Gets or sets additional headers to include on the attachment part.
    /// </summary>
    public IDictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Creates a <see cref="MimeEntity"/> instance for the descriptor.
    /// </summary>
    /// <param name="inline">Whether the attachment is used as an inline resource.</param>
    internal MimeEntity CreateMimeEntity(bool inline)
    {
        var stream = CreateContentStream();
        var mediaType = !string.IsNullOrWhiteSpace(ContentType)
            ? ContentType
            : !string.IsNullOrWhiteSpace(FileName)
                ? MimeTypes.GetMimeType(FileName)
                : "application/octet-stream";

        var part = new MimePart(mediaType)
        {
            Content = new MimeContent(stream),
            FileName = FileName,
        };

        var disposition = ContentDisposition ?? new ContentDisposition(inline ? ContentDisposition.Inline : ContentDisposition.Attachment);
        part.ContentDisposition = disposition;

        var contentId = ContentId;
        if (inline && string.IsNullOrWhiteSpace(contentId))
        {
            contentId = !string.IsNullOrWhiteSpace(FileName)
                ? FileName
                : MimeUtils.GenerateMessageId();
        }

        if (!string.IsNullOrWhiteSpace(contentId))
        {
            part.ContentId = contentId;
        }

        if (!string.IsNullOrWhiteSpace(ContentDescription))
        {
            part.ContentDescription = ContentDescription;
        }

        if (TransferEncoding.HasValue)
        {
            part.ContentTransferEncoding = TransferEncoding.Value;
        }

        if (Headers != null)
        {
            foreach (var header in Headers)
            {
                if (!string.IsNullOrWhiteSpace(header.Key) && header.Value is not null)
                {
                    part.Headers[header.Key] = header.Value;
                }
            }
        }

        return part;
    }

    /// <summary>
    /// Creates the content stream used by the attachment.
    /// </summary>
    /// <remarks>
    /// The returned stream must be positioned at the beginning and remain readable for the lifetime
    /// of the message being sent.
    /// </remarks>
    /// <returns>A readable <see cref="Stream"/> containing the attachment data.</returns>
    protected abstract Stream CreateContentStream();
}

/// <summary>
/// Attachment descriptor that sources its content from a <see cref="Stream"/>.
/// </summary>
public class SmtpAttachmentStreamDescriptor : SmtpAttachmentDescriptor
{
    private readonly Stream _stream;
    private readonly bool _leaveStreamOpen;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpAttachmentStreamDescriptor"/> class.
    /// </summary>
    /// <param name="stream">Stream providing the attachment content.</param>
    /// <param name="fileName">File name assigned to the attachment.</param>
    /// <param name="leaveStreamOpen">Whether the provided stream should remain open after the descriptor copies its content.</param>
    public SmtpAttachmentStreamDescriptor(Stream stream, string fileName, bool leaveStreamOpen = true)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        }

        FileName = string.IsNullOrWhiteSpace(fileName)
            ? throw new ArgumentException("File name must be provided.", nameof(fileName))
            : fileName;
        _leaveStreamOpen = leaveStreamOpen;
    }

    /// <inheritdoc />
    protected override Stream CreateContentStream()
    {
        if (_stream.CanSeek)
        {
            _stream.Position = 0;
        }

        var memory = new MemoryStream();
        _stream.CopyTo(memory);
        memory.Position = 0;

        if (_stream.CanSeek)
        {
            try
            {
                _stream.Position = 0;
            }
            catch (ObjectDisposedException)
            {
                // Ignore; stream may have been disposed externally.
            }
        }

        if (!_leaveStreamOpen)
        {
            _stream.Dispose();
        }

        return memory;
    }
}

/// <summary>
/// Attachment descriptor that sources its content from a byte array.
/// </summary>
public class SmtpAttachmentByteArrayDescriptor : SmtpAttachmentDescriptor
{
    private readonly byte[] _buffer;
    private readonly bool _cloneBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpAttachmentByteArrayDescriptor"/> class.
    /// </summary>
    /// <param name="buffer">Byte array containing the attachment data.</param>
    /// <param name="fileName">File name assigned to the attachment.</param>
    /// <param name="cloneBuffer">If <c>true</c>, the byte array is cloned to prevent external modifications.</param>
    public SmtpAttachmentByteArrayDescriptor(byte[] buffer, string fileName, bool cloneBuffer = true)
    {
        buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _buffer = buffer.Length == 0 ? Array.Empty<byte>() : buffer;

        FileName = string.IsNullOrWhiteSpace(fileName)
            ? throw new ArgumentException("File name must be provided.", nameof(fileName))
            : fileName;
        _cloneBuffer = cloneBuffer;
    }

    /// <inheritdoc />
    protected override Stream CreateContentStream()
    {
        var data = _cloneBuffer ? (byte[])_buffer.Clone() : _buffer;
        return new MemoryStream(data, writable: false);
    }
}
