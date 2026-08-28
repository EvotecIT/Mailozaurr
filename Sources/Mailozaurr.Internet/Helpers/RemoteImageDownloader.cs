using System.Net.Sockets;
using System.Security.Cryptography;

namespace Mailozaurr;

internal static class RemoteImageDownloader {
    internal sealed class DownloadResult {
        public DownloadResult(Uri source, byte[] data, string mediaType) {
            Source = source;
            Data = data;
            MediaType = mediaType;
        }

        public Uri Source { get; }
        public byte[] Data { get; }
        public string MediaType { get; }
    }

    public static async Task<DownloadResult?> DownloadAsync(
        string source,
        RemoteImageDownloadOptions options,
        long remainingTotalBytes,
        HttpClient client,
        CancellationToken cancellationToken) {
        if (!Uri.TryCreate(source, UriKind.Absolute, out Uri? current)) return null;
        long responseLimit = Math.Min(options.MaxImageBytes, remainingTotalBytes);
        if (responseLimit <= 0) return null;

        for (int redirects = 0; ; redirects++) {
            await ValidateDestinationAsync(current, options, cancellationToken).ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            using HttpResponseMessage response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (IsRedirect(response.StatusCode)) {
                if (redirects >= options.MaxRedirects || response.Headers.Location == null) return null;
                current = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(current, response.Headers.Location);
                continue;
            }
            if (!response.IsSuccessStatusCode) return null;

            string? mediaType = response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrWhiteSpace(mediaType)) {
                mediaType = MimeTypes.GetMimeType(Path.GetFileName(current.AbsolutePath));
            }
            string normalizedMediaType = mediaType ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedMediaType)
                || !options.AllowedMediaTypes.Contains(normalizedMediaType)) return null;

            long? declaredLength = response.Content.Headers.ContentLength;
            if (declaredLength.HasValue && declaredLength.Value > responseLimit) {
                throw new InvalidDataException($"Remote image exceeded the {responseLimit} byte limit.");
            }

#if NETFRAMEWORK || NETSTANDARD2_0
            using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#else
            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#endif
            byte[] data = await ReadBoundedAsync(stream, responseLimit, cancellationToken).ConfigureAwait(false);
            return new DownloadResult(current, data, normalizedMediaType);
        }
    }

    private static async Task ValidateDestinationAsync(
        Uri uri,
        RemoteImageDownloadOptions options,
        CancellationToken cancellationToken) {
        bool validScheme = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || options.AllowHttp && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        if (!validScheme || string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo)) {
            throw new InvalidOperationException("Remote image URL is not an allowed HTTP destination.");
        }
        if (options.AllowPrivateNetworkAddresses) return;

        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.DnsSafeHost, out IPAddress? literalAddress)) {
            addresses = new[] { literalAddress };
        } else {
#if NET8_0_OR_GREATER
            addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken).ConfigureAwait(false);
#else
            cancellationToken.ThrowIfCancellationRequested();
            addresses = await Task.Run(() => Dns.GetHostAddresses(uri.DnsSafeHost), cancellationToken).ConfigureAwait(false);
#endif
        }
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address))) {
            throw new InvalidOperationException("Remote image URL resolved to a non-public network address.");
        }
    }

    internal static bool IsPublicAddress(IPAddress address) {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return false;

        byte[] bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork) {
            byte first = bytes[0];
            byte second = bytes[1];
            if (first == 0 || first == 10 || first == 127 || first >= 224) return false;
            if (first == 100 && second >= 64 && second <= 127) return false;
            if (first == 169 && second == 254) return false;
            if (first == 172 && second >= 16 && second <= 31) return false;
            if (first == 192 && second == 0) return false;
            if (first == 192 && second == 168) return false;
            if (first == 198 && (second == 18 || second == 19)) return false;
            if (first == 198 && second == 51 && bytes[2] == 100) return false;
            if (first == 203 && second == 0 && bytes[2] == 113) return false;
            return true;
        }
        if (address.AddressFamily != AddressFamily.InterNetworkV6) return false;
        if (address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal) return false;
        if ((bytes[0] & 0xfe) == 0xfc) return false;
        if (bytes[0] == 0x01 && bytes.Skip(1).Take(7).All(value => value == 0)) return false;
        if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8) return false;
        return true;
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.MovedPermanently or
        HttpStatusCode.Redirect or
        HttpStatusCode.RedirectMethod or
        HttpStatusCode.TemporaryRedirect || (int)statusCode == 308;

    private static async Task<byte[]> ReadBoundedAsync(
        Stream source,
        long maxBytes,
        CancellationToken cancellationToken) {
        using var destination = new MemoryStream((int)Math.Min(maxBytes, 64 * 1024));
        var buffer = new byte[64 * 1024];
        long total = 0;
        while (true) {
            int read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            if (read == 0) return destination.ToArray();
            total += read;
            if (total > maxBytes) throw new InvalidDataException($"Remote image exceeded the {maxBytes} byte limit.");
            destination.Write(buffer, 0, read);
        }
    }

    internal static string CreateContentId(Uri source, ISet<string> allocatedContentIds) {
        string candidate;
        try {
            candidate = Path.GetFileName(Uri.UnescapeDataString(source.AbsolutePath));
        } catch (UriFormatException) {
            candidate = Path.GetFileName(source.AbsolutePath);
        }
        candidate = SanitizeContentId(candidate);
        if (string.IsNullOrWhiteSpace(candidate)) candidate = "remote-image";
        if (allocatedContentIds.Add(candidate)) return candidate;

        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(source.AbsoluteUri));
        string suffix = BitConverter.ToString(hash, 0, 6).Replace("-", string.Empty).ToLowerInvariant();
        string extension = Path.GetExtension(candidate);
        string stem = Path.GetFileNameWithoutExtension(candidate);
        string unique = stem + "-" + suffix + extension;
        for (int collision = 2; !allocatedContentIds.Add(unique); collision++) {
            unique = stem + "-" + suffix + "-" + collision.ToString(
                System.Globalization.CultureInfo.InvariantCulture) + extension;
        }
        return unique;
    }

    private static string SanitizeContentId(string value) {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var builder = new StringBuilder(Math.Min(value.Length, 96));
        foreach (char character in value) {
            if (builder.Length >= 96) break;
            builder.Append(char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '_');
        }
        return builder.ToString().Trim('.', ' ', '_');
    }
}
