using Mailozaurr.Definitions;
using OfficeIMO.Email;

namespace Mailozaurr;

/// <summary>Adapts OfficeIMO email attachments to Mailozaurr transport descriptors.</summary>
public static class OfficeIMOAttachmentAdapter {
    /// <summary>
    /// Creates a transport descriptor that reopens the OfficeIMO attachment source without copying its content.
    /// </summary>
    /// <remarks>
    /// The owning <see cref="EmailReadResult"/> or <see cref="EmailDocument"/> must remain alive until all
    /// transport reads complete when the OfficeIMO attachment is backed by temporary storage.
    /// </remarks>
    public static AttachmentDescriptor ToMailozaurrAttachment(this EmailAttachment attachment) {
        if (attachment == null) throw new ArgumentNullException(nameof(attachment));
        string fileName = string.IsNullOrWhiteSpace(attachment.FileName) ? "attachment" : attachment.FileName!;
        var descriptor = new ContentSourceAttachmentDescriptor(
            new OfficeIMOEmailContentSource(attachment),
            fileName) {
            ContentType = attachment.ContentType,
            ContentId = attachment.ContentId
        };
        if (attachment.IsInline) {
            descriptor.ContentDisposition = new MimeKit.ContentDisposition(MimeKit.ContentDisposition.Inline);
        }
        return descriptor;
    }

    private sealed class OfficeIMOEmailContentSource : IAttachmentContentSource {
        private readonly EmailAttachment _attachment;

        internal OfficeIMOEmailContentSource(EmailAttachment attachment) => _attachment = attachment;

        public long? Length => _attachment.Content?.LongLength ??
            _attachment.ContentSource?.Length ??
            (_attachment.Length > 0 ? _attachment.Length : (long?)null);

        public Stream OpenRead() => _attachment.OpenContentStream();

        public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            _attachment.OpenContentStreamAsync(cancellationToken);
    }
}
