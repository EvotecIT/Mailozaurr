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

        string pattern = "<img[^>]+src=[\"']([^\"']+)[\"']";
        foreach (Match match in Regex.Matches(html, pattern, RegexOptions.IgnoreCase)) {
            var path = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("cid:", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }
            if (File.Exists(path)) {
                var fileName = Path.GetFileName(path);
                html = html.Replace(path, $"cid:{fileName}");
                paths.Add(path);
            }
        }
        return (html, paths);
    }

    /// <summary>
    /// Downloads externally referenced images and replaces their sources with cid links.
    /// </summary>
    /// <param name="html">HTML content to inspect.</param>
    /// <param name="maxParallelism">Maximum number of concurrent downloads.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Modified HTML and list of downloaded images.</returns>
    public static async Task<(string Html, List<RemoteImage> Images)> DownloadRemoteImagesAsync(string html, int maxParallelism = 4, CancellationToken cancellationToken = default) {
        var images = new List<RemoteImage>();
        if (string.IsNullOrWhiteSpace(html)) return (html, images);

        string pattern = "<img[^>]+src=['\"]([^'\"]+)['\"]";
        var urls = new List<string>();
        foreach (Match match in Regex.Matches(html, pattern, RegexOptions.IgnoreCase)) {
            var url = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(url)) continue;
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;
            urls.Add(url);
        }

        var semaphore = new SemaphoreSlim(maxParallelism <= 0 ? 1 : maxParallelism);
        var tasks = new List<Task<(string Url, RemoteImage? Image)>>();
        foreach (var url in urls) {
            tasks.Add(DownloadAsync(url));
        }

        async Task<(string Url, RemoteImage? Image)> DownloadAsync(string url) {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                using var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return (url, null);
#if NETFRAMEWORK || NETSTANDARD2_0
                var data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
#else
                var data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
#endif
                var mediaType = response.Content.Headers.ContentType?.MediaType ?? MimeTypes.GetMimeType(Path.GetFileName(url));
                var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
                if (string.IsNullOrEmpty(fileName)) fileName = Guid.NewGuid().ToString("N");
                return (url, new RemoteImage { ContentId = fileName, Data = data, MediaType = mediaType });
            } catch (Exception ex) {
                LoggingMessages.Logger.WriteWarning($"Failed to download image '{url}': {ex.Message}");
                return (url, null);
            } finally {
                semaphore.Release();
            }
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        foreach (var (url, image) in results) {
            if (image == null) continue;
            html = html.Replace(url, $"cid:{image.ContentId}");
            images.Add(image);
        }

        return (html, images);
    }
}
