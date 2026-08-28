using System.Net.Http;
using System.Threading;
using OfficeIMO.Email;

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

    /// <summary>Local image selected for inline attachment embedding.</summary>
    public sealed class LocalImage {
        internal LocalImage(string path, string contentId) {
            Path = path;
            ContentId = contentId;
        }

        /// <summary>Authorized local file path.</summary>
        public string Path { get; }

        /// <summary>Unique content identifier written into the HTML body.</summary>
        public string ContentId { get; }
    }

    /// <summary>
    /// Replaces local image <c>src</c> references with <c>cid:</c> links and returns the
    /// updated HTML and a collection of the embedded file paths.
    /// </summary>
    /// <param name="html">HTML content that may contain local image paths.</param>
    /// <returns>The updated HTML and list of file paths that were replaced.</returns>
    public static (string Html, List<string> Paths) ExtractLocalImagePaths(string html) {
        (string rendered, List<LocalImage> images) = ExtractLocalImagesCore(html, uniqueContentIds: false);
        return (rendered, images.Select(image => image.Path).ToList());
    }

    /// <summary>
    /// Rewrites authorized local image sources with unique CID references and returns their file mappings.
    /// </summary>
    public static (string Html, List<LocalImage> Images) ExtractLocalImages(string html) =>
        ExtractLocalImagesCore(html, uniqueContentIds: true);

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

        EmailHtmlImageDocument document = EmailHtmlImageDocument.Parse(html);
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var allocatedContentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;
        int attemptedImages = 0;

        foreach (EmailHtmlImageReference reference in document.Images) {
            var url = reference.Source;
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

        foreach (EmailHtmlImageReference reference in document.Images) {
            if (replacements.TryGetValue(reference.Source, out string? value)) {
                document.SetImageSource(reference.Index, value);
            }
        }

        return (document.ToHtml(), images);
    }

    private static (string Html, List<LocalImage> Images) ExtractLocalImagesCore(
        string html,
        bool uniqueContentIds) {
        var images = new List<LocalImage>();
        if (string.IsNullOrWhiteSpace(html)) return (html, images);

        EmailHtmlImageDocument document = EmailHtmlImageDocument.Parse(html);
        var allocatedContentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (EmailHtmlImageReference reference in document.Images) {
            string path = reference.Source;
            if (string.IsNullOrWhiteSpace(path) || IsNonLocalSource(path) || !File.Exists(path)) continue;

            string fileName = Path.GetFileName(path);
            string contentId = uniqueContentIds
                ? RemoteImageDownloader.CreateContentId(fileName, Path.GetFullPath(path), "local-image", allocatedContentIds)
                : fileName;
            document.SetImageSource(reference.Index, "cid:" + contentId);
            images.Add(new LocalImage(path, contentId));
        }
        return (document.ToHtml(), images);
    }

    private static bool IsNonLocalSource(string source) {
        if (source.StartsWith("cid:", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return true;
        return Uri.TryCreate(source, UriKind.Absolute, out Uri? uri) &&
            (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }
}
