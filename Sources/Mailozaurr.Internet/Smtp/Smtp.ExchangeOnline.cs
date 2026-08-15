using System.Globalization;

namespace Mailozaurr;

public partial class Smtp {
    /// <summary>
    /// Builds the direct Exchange Online Protection host name commonly used for an accepted domain.
    /// </summary>
    /// <param name="domain">Accepted email domain, for example <c>example.com</c>.</param>
    /// <returns>The inferred Exchange Online Protection host name.</returns>
    public static string GetExchangeOnlineProtectionHost(string domain) {
        if (string.IsNullOrWhiteSpace(domain)) {
            throw new ArgumentException("Domain is required.", nameof(domain));
        }

        var normalized = domain.Trim().TrimEnd('.').ToLowerInvariant();
        if (normalized.Length == 0) {
            throw new ArgumentException("Domain is required.", nameof(domain));
        }

        var idn = new IdnMapping();
        var asciiDomain = idn.GetAscii(normalized);
        return asciiDomain.Replace('.', '-') + ".mail.protection.outlook.com";
    }
}
