namespace Mailozaurr.DmarcReports;

/// <summary>
/// Represents a zipped XML attachment belonging to a DMARC report.
/// </summary>
public sealed class DmarcReportAttachment : IDisposable {
    /// <summary>File name of the attachment.</summary>
    public string Name { get; }

    /// <summary>Stream containing the zipped attachment.</summary>
    public Stream Content { get; }

    public DmarcReportAttachment(string name, Stream content) {
        Name = name;
        Content = content;
    }

    public void Dispose() => Content.Dispose();
}