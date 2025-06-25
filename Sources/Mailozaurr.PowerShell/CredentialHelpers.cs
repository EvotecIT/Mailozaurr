namespace Mailozaurr.PowerShell;

using System.Security;
using System.Net;

/// <summary>
/// Helper class for converting strings to SecureString.
/// </summary>
public static class CredentialHelpers {
    /// <summary>
    /// Converts a string to a SecureString.
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    public static SecureString ToSecureString(string s) {
        if (string.IsNullOrEmpty(s))
            return new SecureString();
        // Try to convert from encrypted string, fallback to plain text
        try {
            return new NetworkCredential("", s).SecurePassword;
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Failed to convert to SecureString: {ex.Message}");
            var ss = new SecureString();
            foreach (char c in s) ss.AppendChar(c);
            ss.MakeReadOnly();
            return ss;
        }
    }
}