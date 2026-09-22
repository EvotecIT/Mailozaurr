using System.Security.Cryptography;
using System.Text;

namespace Mailozaurr;

internal static class ArchiveAccountScope {
    internal static string Create(string provider, string host, int port, string userName) {
        var normalizedProvider = provider.Trim();
        var normalizedHost = host.Trim().ToLowerInvariant();
        // Authentication usernames are provider-defined and may be case-sensitive.
        var normalizedUserName = userName.Trim();
        var identity = normalizedProvider + "\n" + normalizedHost + "\n" +
            port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" + normalizedUserName;
        using var sha = SHA256.Create();
        var fingerprint = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(identity)));
        return normalizedProvider + ":account:" + fingerprint;
    }
}
