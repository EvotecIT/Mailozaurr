using System;
using System.Collections.Generic;
using System.IO;
using MimeKit;

namespace Mailozaurr;

/// <summary>
/// Status of POP3 attachment payload build operation.
/// </summary>
public enum Pop3AttachmentBuildStatus {
    /// <summary>Attachment payload was built.</summary>
    Success,
    /// <summary>Attachment index was not found.</summary>
    NotFound,
    /// <summary>Attachment exceeded maximum allowed payload size.</summary>
    SizeLimitExceeded
}

/// <summary>
/// Materialized POP3 attachment payload.
/// </summary>
public sealed record Pop3AttachmentPayload(
    string FileName,
    string ContentType,
    byte[] Bytes);

/// <summary>
/// Result of POP3 attachment payload build operation.
/// </summary>
public sealed record Pop3AttachmentBuildResult(
    Pop3AttachmentBuildStatus Status,
    Pop3AttachmentPayload? Payload,
    string? Error) {
    /// <summary>True when payload was produced.</summary>
    public bool Success => Payload != null;
}

/// <summary>
/// Builds binary attachment payload from MIME messages retrieved over POP3.
/// </summary>
public static class Pop3AttachmentPayloadBuilder {
    /// <summary>
    /// Builds attachment payload by index.
    /// </summary>
    public static Pop3AttachmentBuildResult Build(MimeMessage message, int attachmentIndex, long maxBytes) {
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }
        if (maxBytes < 0) {
            throw new ArgumentOutOfRangeException(nameof(maxBytes));
        }

        var attachments = new List<MimeEntity>();
        foreach (var attachment in message.Attachments) {
            attachments.Add(attachment);
        }

        if (attachmentIndex < 0 || attachmentIndex >= attachments.Count) {
            return new Pop3AttachmentBuildResult(
                Pop3AttachmentBuildStatus.NotFound,
                null,
                "Attachment not found.");
        }

        var entity = attachments[attachmentIndex];
        string fileName;
        string contentType;
        if (entity is MimePart part) {
            fileName = part.FileName ?? part.ContentDisposition?.FileName ?? part.ContentType?.Name ?? string.Empty;
            contentType = part.ContentType?.MimeType ?? "application/octet-stream";
        } else if (entity is MessagePart messagePart) {
            fileName = messagePart.ContentDisposition?.FileName ?? messagePart.ContentType?.Name ?? "message.eml";
            contentType = "message/rfc822";
        } else {
            fileName = "attachment";
            contentType = "application/octet-stream";
        }

        using var ms = new MemoryStream();
        try {
            using var limited = new SizeLimitedWriteStream(ms, maxBytes);
            if (entity is MimePart mimePart) {
                mimePart.Content.DecodeTo(limited);
            } else if (entity is MessagePart nestedMessagePart) {
                nestedMessagePart.Message.WriteTo(limited);
            } else {
                entity.WriteTo(limited);
            }
        } catch (InvalidDataException ex) {
            return new Pop3AttachmentBuildResult(
                Pop3AttachmentBuildStatus.SizeLimitExceeded,
                null,
                ex.Message);
        }

        fileName = string.IsNullOrWhiteSpace(fileName) ? $"attachment-{attachmentIndex}" : fileName.Trim();
        return new Pop3AttachmentBuildResult(
            Pop3AttachmentBuildStatus.Success,
            new Pop3AttachmentPayload(fileName, contentType, ms.ToArray()),
            null);
    }

    private sealed class SizeLimitedWriteStream : Stream {
        private readonly Stream _inner;
        private readonly long _maxBytes;
        private long _writtenBytes;

        internal SizeLimitedWriteStream(Stream inner, long maxBytes) {
            _inner = inner;
            _maxBytes = maxBytes;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) {
            if (count < 0) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (_maxBytes >= 0 && _writtenBytes + count > _maxBytes) {
                throw new InvalidDataException($"Attachment exceeds max allowed size of {_maxBytes} bytes.");
            }

            _inner.Write(buffer, offset, count);
            _writtenBytes += count;
        }
    }
}
