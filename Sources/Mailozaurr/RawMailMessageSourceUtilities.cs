namespace Mailozaurr;

internal static class RawMailMessageSourceUtilities {
    internal static async Task<byte[]> ReadBoundedAsync(
        Stream stream,
        long maxBytes,
        CancellationToken cancellationToken) {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (maxBytes <= 0 || maxBytes > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(maxBytes));

        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true) {
            var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (output.Length + read > maxBytes) {
                throw new InvalidOperationException($"Provider message exceeds the configured {maxBytes} byte export limit.");
            }
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    internal static byte[] DecodeBase64Url(string value, long maxBytes) {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("Provider response did not contain raw RFC 822 content.");
        if (maxBytes <= 0 || maxBytes > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(maxBytes));

        long maximumEncodedCharacters = checked(((maxBytes + 2L) / 3L) * 4L);
        if (value.Length > maximumEncodedCharacters) {
            throw new InvalidOperationException($"Provider message exceeds the configured {maxBytes} byte export limit.");
        }

        var normalized = value.Trim().Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
        byte[] content;
        try {
            content = Convert.FromBase64String(normalized);
        } catch (FormatException ex) {
            throw new InvalidDataException("Provider returned invalid base64url RFC 822 content.", ex);
        }
        if (content.LongLength > maxBytes) {
            throw new InvalidOperationException($"Provider message exceeds the configured {maxBytes} byte export limit.");
        }
        return content;
    }
}
