namespace Mailozaurr.DmarcReports;

/// <summary>
/// Represents a DMARC aggregate report extracted from a message.
/// </summary>
public sealed class DmarcReport {
    /// <summary>Sender of the DMARC report.</summary>
    public string? From { get; set; }

    /// <summary>Subject of the DMARC report message.</summary>
    public string? Subject { get; set; }

    /// <summary>Date the report message was received.</summary>
    public DateTimeOffset Date { get; set; }

    /// <summary>Collection of zipped XML attachments.</summary>
    public IList<DmarcReportAttachment> Attachments { get; } = new List<DmarcReportAttachment>();
}

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
