#if WINDOWS
using System;
using System.Security.Cryptography;
using System.Text;

namespace Mailozaurr;

internal sealed class WindowsCredentialProtector : ICredentialProtector {
    public string Protect(string plainText) {
        if (plainText == null) {
            throw new ArgumentNullException(nameof(plainText));
        }

        var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(plaintextBytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedData) {
        if (protectedData == null) {
            throw new ArgumentNullException(nameof(protectedData));
        }

        var protectedBytes = Convert.FromBase64String(protectedData);
        var plaintextBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
#endif
