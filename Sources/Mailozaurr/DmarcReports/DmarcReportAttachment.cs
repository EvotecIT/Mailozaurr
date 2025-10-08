namespace Mailozaurr.DmarcReports;

/// <summary>
/// Represents a zipped XML attachment belonging to a DMARC report.
/// </summary>
public sealed class DmarcReportAttachment : IDisposable {
    /// <summary>File name of the attachment.</summary>
    public string Name { get; }

    /// <summary>Stream containing the zipped attachment.</summary>
    public Stream Content { get; }

    /// <summary>Initializes a new instance of the <see cref="DmarcReportAttachment"/> class.</summary>
    /// <param name="name">Attachment file name.</param>
    /// <param name="content">Stream containing zipped report content.</param>
    public DmarcReportAttachment(string name, Stream content) {
        Name = name;
        Content = content;
    }

    /// <summary>Releases the underlying stream.</summary>
    public void Dispose() => Content.Dispose();
}
