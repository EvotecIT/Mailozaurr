namespace Mailozaurr.Definitions;

/// <summary>Attachment descriptor backed by a reopenable content source.</summary>
public sealed class ContentSourceAttachmentDescriptor : AttachmentDescriptor {
    private readonly IAttachmentContentSource _source;

    /// <summary>Creates a descriptor over a reopenable source.</summary>
    /// <param name="source">Source that returns an independent readable stream for every call.</param>
    /// <param name="fileName">File name presented to recipients.</param>
    public ContentSourceAttachmentDescriptor(IAttachmentContentSource source, string fileName) {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("File name must be provided.", nameof(fileName));
        FileName = fileName;
    }

    /// <summary>The underlying reopenable source.</summary>
    public IAttachmentContentSource Source => _source;

    /// <inheritdoc />
    public override long? Length => _source.Length;

    /// <inheritdoc />
    protected override Stream CreateContentStream() => _source.OpenRead();

    /// <inheritdoc />
    protected override Task<Stream> CreateContentStreamAsync(CancellationToken cancellationToken) =>
        _source.OpenReadAsync(cancellationToken);
}
