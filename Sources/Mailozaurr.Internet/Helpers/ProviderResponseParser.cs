using System.Xml;
using System.Xml.Linq;

namespace Mailozaurr;

/// <summary>
/// Extracts native message identifiers from successful provider responses.
/// </summary>
internal static class ProviderResponseParser {
    internal static async Task<string> ReadContentAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken) {

        if (response.Content == null) {
            return string.Empty;
        }

#if NET5_0_OR_GREATER
        return await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
#else
        return await response.Content
            .ReadAsStringAsync()
            .ConfigureAwait(false);
#endif
    }

    internal static string? GetSendGridMessageId(
        HttpResponseMessage response) {

        return response.Headers.TryGetValues(
            "X-Message-Id",
            out IEnumerable<string>? values)
            ? Normalize(values.FirstOrDefault())
            : null;
    }

    internal static string? GetJsonMessageId(string? content) {
        if (content == null || content.Trim().Length == 0) {
            return null;
        }

        try {
            using JsonDocument document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty(
                "id",
                out JsonElement id)) {
                return null;
            }

            return Normalize(
                id.ValueKind == JsonValueKind.String
                    ? id.GetString()
                    : id.ToString());
        } catch (JsonException) {
            return null;
        }
    }

    internal static string? GetSesMessageId(string? content) {
        if (content == null || content.Trim().Length == 0) {
            return null;
        }

        try {
            using var textReader = new StringReader(content);
            using XmlReader xmlReader = XmlReader.Create(
                textReader,
                new XmlReaderSettings {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                });
            XDocument document = XDocument.Load(
                xmlReader,
                LoadOptions.None);
            return Normalize(document
                .Descendants()
                .FirstOrDefault(element =>
                    string.Equals(
                        element.Name.LocalName,
                        "MessageId",
                        StringComparison.Ordinal))
                ?.Value);
        } catch (System.Xml.XmlException) {
            return null;
        }
    }

    private static string? Normalize(string? value) {
        if (value == null) {
            return null;
        }

        string normalized = value.Trim();
        return normalized.Length == 0 ? null : normalized;
    }
}
