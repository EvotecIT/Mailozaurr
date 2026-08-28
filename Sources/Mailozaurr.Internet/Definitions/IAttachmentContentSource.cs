namespace Mailozaurr.Definitions;

/// <summary>Supplies a fresh readable stream for attachment content.</summary>
public interface IAttachmentContentSource {
    /// <summary>Known content length, or null when it cannot be determined cheaply.</summary>
    long? Length { get; }

    /// <summary>Opens a new readable stream. The caller owns the returned stream.</summary>
    Stream OpenRead();

    /// <summary>Asynchronously opens a new readable stream. The caller owns the returned stream.</summary>
    Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default);
}
