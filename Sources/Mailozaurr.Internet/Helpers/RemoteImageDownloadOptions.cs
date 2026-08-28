namespace Mailozaurr;

/// <summary>Security and resource limits for embedding remotely referenced email images.</summary>
public sealed class RemoteImageDownloadOptions {
    /// <summary>Maximum number of distinct remote image URLs attempted per message.</summary>
    public int MaxImageCount { get; set; } = 32;

    /// <summary>Maximum downloaded bytes for one image.</summary>
    public long MaxImageBytes { get; set; } = 8L * 1024 * 1024;

    /// <summary>Maximum combined downloaded bytes embedded into one message.</summary>
    public long MaxTotalBytes { get; set; } = 32L * 1024 * 1024;

    /// <summary>Maximum number of validated HTTP redirects followed for one image.</summary>
    public int MaxRedirects { get; set; } = 3;

    /// <summary>Allows unencrypted HTTP URLs. HTTPS is required by default.</summary>
    public bool AllowHttp { get; set; }

    /// <summary>
    /// Allows loopback, link-local, private, and other non-public network destinations.
    /// Enable only for explicitly trusted HTML and infrastructure.
    /// </summary>
    public bool AllowPrivateNetworkAddresses { get; set; }

    /// <summary>
    /// Allows hostname downloads on runtimes that cannot bind a validated DNS answer to the socket.
    /// This weakens SSRF protection and should be enabled only for trusted HTML and DNS.
    /// </summary>
    public bool AllowUnpinnedDnsResolution { get; set; }

    /// <summary>Allowed image media types. SVG is excluded because it may contain active content.</summary>
    public ISet<string> AllowedMediaTypes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/webp",
        "image/bmp",
        "image/tiff",
        "image/x-icon",
        "image/vnd.microsoft.icon"
    };

    internal void Validate() {
        if (MaxImageCount < 0) throw new ArgumentOutOfRangeException(nameof(MaxImageCount));
        if (MaxImageBytes <= 0 || MaxImageBytes > int.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(MaxImageBytes));
        }
        if (MaxTotalBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaxTotalBytes));
        if (MaxRedirects < 0 || MaxRedirects > 20) throw new ArgumentOutOfRangeException(nameof(MaxRedirects));
        if (AllowedMediaTypes.Count == 0) throw new InvalidOperationException("At least one remote image media type must be allowed.");
    }
}
