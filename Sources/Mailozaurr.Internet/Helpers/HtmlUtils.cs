using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;

namespace Mailozaurr;

/// <summary>
/// Helper utilities for working with HTML content.
/// </summary>
/// <remarks>
/// Methods on this class assist with embedding images and
/// performing minor HTML transformations.
/// </remarks>
public static class HtmlUtils {
    internal static HttpClient HttpClient { get; } = new HttpClient(new HttpClientHandler {
        AllowAutoRedirect = false
    });

    private static readonly Regex ImageSrcRegex = new("(?<=<img[^>]+src=[\"'])([^\"']+)(?=[\"'])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static HtmlUtils() {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => HttpClient.Dispose();
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

        html = ImageSrcRegex.Replace(html, match => {
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
        });
        return (html, paths);
    }

    /// <summary>
    /// Downloads externally referenced images and replaces their sources with cid links.
    /// </summary>
    /// <param name="html">HTML content to inspect.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Modified HTML and list of downloaded images.</returns>
    public static Task<(string Html, List<RemoteImage> Images)> DownloadRemoteImagesAsync(
        string html,
        CancellationToken cancellationToken = default) =>
        DownloadRemoteImagesAsync(html, new RemoteImageDownloadOptions(), cancellationToken);

    /// <summary>Downloads safe, bounded remote images and replaces their sources with cid links.</summary>
    /// <param name="html">HTML content to inspect.</param>
    /// <param name="options">Network and resource policy for remote image retrieval.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Modified HTML and list of downloaded images.</returns>
    public static async Task<(string Html, List<RemoteImage> Images)> DownloadRemoteImagesAsync(
        string html,
        RemoteImageDownloadOptions options,
        CancellationToken cancellationToken = default) {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.Validate();
        var images = new List<RemoteImage>();
        if (string.IsNullOrWhiteSpace(html)) return (html, images);

        var matches = ImageSrcRegex.Matches(html);
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var allocatedContentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;
        int attemptedImages = 0;

        foreach (Match match in matches) {
            var url = match.Value;
            if (string.IsNullOrWhiteSpace(url)) continue;
            if (replacements.ContainsKey(url)) continue;
            if (attemptedImages >= options.MaxImageCount) break;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? sourceUri)
                || !(string.Equals(sourceUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                    || options.AllowHttp && string.Equals(sourceUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))) {
                continue;
            }
            attemptedImages++;
            try {
                RemoteImageDownloader.DownloadResult? downloaded = await RemoteImageDownloader.DownloadAsync(
                    url,
                    options,
                    options.MaxTotalBytes - totalBytes,
                    HttpClient,
                    cancellationToken).ConfigureAwait(false);
                if (downloaded == null) continue;
                string contentId = RemoteImageDownloader.CreateContentId(downloaded.Source, allocatedContentIds);
                replacements[url] = $"cid:{contentId}";
                images.Add(new RemoteImage {
                    ContentId = contentId,
                    Data = downloaded.Data,
                    MediaType = downloaded.MediaType
                });
                totalBytes += downloaded.Data.LongLength;
                if (totalBytes >= options.MaxTotalBytes) break;
            } catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested) {
                throw new OperationCanceledException(ex.Message, ex, cancellationToken);
            } catch (Exception ex) {
                LoggingMessages.Logger.WriteWarning(
                    $"Failed to download a remote image from host '{sourceUri.Host}': {ex.Message}");
            }
        }

        html = ImageSrcRegex.Replace(html, m => replacements.TryGetValue(m.Value, out var value) ? value : m.Value);

        return (html, images);
    }
}
