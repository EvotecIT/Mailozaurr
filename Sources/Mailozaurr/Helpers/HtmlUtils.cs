using System.Net.Http;
using System.Threading;
using System.Text.RegularExpressions;

namespace Mailozaurr;

/// <summary>
/// Helper utilities for working with HTML content.
/// </summary>
/// <remarks>
/// Methods on this class assist with embedding images and
/// performing minor HTML transformations.
/// </remarks>
public static class HtmlUtils {
    internal static HttpClient HttpClient { get; set; }

    static HtmlUtils() {
        HttpClient = new HttpClient();
    }

    /// <summary>
    /// Represents an image downloaded from a remote location for
    /// embedding into an HTML message.
    /// </summary>
    public class RemoteImage {
        /// <summary>Content identifier used when embedding.</summary>
        public string ContentId { get; set; } = string.Empty;
        /// <summary>Binary data of the image.</summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();
        /// <summary>MIME type of the image data.</summary>
        public string MediaType { get; set; } = string.Empty;
    }
    /// <summary>
    /// Replaces local image <c>src</c> references with <c>cid:</c> links and returns the
    /// updated HTML and a collection of the embedded file paths.
    /// </summary>
    /// <param name="html">HTML content that may contain local image paths.</param>
    /// <returns>The updated HTML and list of file paths that were replaced.</returns>
    public static (string Html, List<string> Paths) ExtractLocalImagePaths(string html) {
        var paths = new List<string>();
        if (string.IsNullOrWhiteSpace(html)) return (html, paths);

        string pattern = "(?<=<img[^>]+src=[\"'])([^\"']+)(?=[\"'])";
        html = Regex.Replace(html, pattern, match => {
            var path = match.Value;
            if (string.IsNullOrWhiteSpace(path)) return path;
            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("cid:", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
                return path;
            }
            if (File.Exists(path)) {
                var fileName = Path.GetFileName(path);
                paths.Add(path);
                return $"cid:{fileName}";
            }
            return path;
        }, RegexOptions.IgnoreCase);
        return (html, paths);
    }

    /// <summary>
    /// Downloads externally referenced images and replaces their sources with cid links.
    /// </summary>
    /// <param name="html">HTML content to inspect.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Modified HTML and list of downloaded images.</returns>
    public static async Task<(string Html, List<RemoteImage> Images)> DownloadRemoteImagesAsync(string html, CancellationToken cancellationToken = default) {
        var images = new List<RemoteImage>();
        if (string.IsNullOrWhiteSpace(html)) return (html, images);

        string pattern = "(?<=<img[^>]+src=['\"])([^'\"]+)(?=['\"])";
        var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in matches) {
            var url = match.Value;
            if (string.IsNullOrWhiteSpace(url)) continue;
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;
            if (replacements.ContainsKey(url)) continue;
            try {
                using var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) continue;
#if NETFRAMEWORK || NETSTANDARD2_0
                var data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
#else
                var data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
#endif
                var mediaType = response.Content.Headers.ContentType?.MediaType ?? MimeTypes.GetMimeType(Path.GetFileName(url));
                var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
                if (string.IsNullOrEmpty(fileName)) fileName = Guid.NewGuid().ToString("N");
                replacements[url] = $"cid:{fileName}";
                images.Add(new RemoteImage { ContentId = fileName, Data = data, MediaType = mediaType });
            } catch (Exception ex) {
                LoggingMessages.Logger.WriteWarning($"Failed to download image '{url}': {ex.Message}");
            }
        }

        html = Regex.Replace(html, pattern, m => replacements.TryGetValue(m.Value, out var value) ? value : m.Value, RegexOptions.IgnoreCase);

        return (html, images);
    }
}
