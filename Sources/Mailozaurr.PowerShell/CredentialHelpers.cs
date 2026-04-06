namespace Mailozaurr.PowerShell;

using System.Security;
using System.Net;
using System.Management.Automation;
using System.Collections.ObjectModel;

/// <summary>
/// Helper class for converting strings to SecureString.
/// </summary>
public static class CredentialHelpers {
    /// <summary>
    /// Converts a string to a SecureString.
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    public static SecureString ToSecureString(string? s) {
        if (string.IsNullOrWhiteSpace(s))
            return new SecureString();
        // Try to convert from encrypted string, fallback to plain text
        try {
            return new NetworkCredential("", s).SecurePassword;
        } catch (ArgumentException ex) {
            LoggingMessages.Logger?.WriteWarning($"Failed to convert to SecureString: {ex.Message}");
            var ss = new SecureString();
            foreach (char c in s!) ss.AppendChar(c);
            ss.MakeReadOnly();
            return ss;
        }
    }

    /// <summary>
    /// Converts a SecureString to plain text.
    /// </summary>
    /// <param name="secureString"></param>
    /// <returns></returns>
    public static string ToPlainText(SecureString? secureString) {
        if (secureString == null || secureString.Length == 0) {
            return string.Empty;
        }

        return new NetworkCredential(string.Empty, secureString).Password;
    }

    /// <summary>
    /// Resolves a secret from the current PowerShell runspace using Get-Secret.
    /// </summary>
    /// <param name="secretName"></param>
    /// <param name="vaultName"></param>
    /// <returns></returns>
    public static SecureString ResolveSecretFromVault(string secretName, string? vaultName = null) {
        if (string.IsNullOrWhiteSpace(secretName)) {
            throw new PSArgumentException("Secret name is required.", nameof(secretName));
        }

        using var powerShell = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        powerShell.AddCommand("Get-Secret").AddParameter("Name", secretName);
        if (!string.IsNullOrWhiteSpace(vaultName)) {
            powerShell.AddParameter("Vault", vaultName);
        }

        Collection<PSObject> results;
        try {
            results = powerShell.Invoke();
        } catch (CommandNotFoundException ex) {
            throw new PSInvalidOperationException(
                "Get-Secret is not available in the current PowerShell session. Install/import Microsoft.PowerShell.SecretManagement or provide the secret directly.", ex);
        }

        if (powerShell.HadErrors) {
            var firstError = powerShell.Streams.Error.FirstOrDefault();
            throw new PSInvalidOperationException(
                firstError?.Exception?.Message ?? $"Secret '{secretName}' could not be resolved from the current PowerShell session.",
                firstError?.Exception);
        }

        var value = results.FirstOrDefault()?.BaseObject;
        if (value == null) {
            throw new PSInvalidOperationException($"Secret '{secretName}' was not found.");
        }

        return value switch {
            SecureString secureString => secureString,
            string text => ToSecureString(text),
            PSCredential credential => credential.Password,
            byte[] bytes => ToSecureString(System.Text.Encoding.UTF8.GetString(bytes)),
            _ => ToSecureString(value.ToString())
        };
    }
}
