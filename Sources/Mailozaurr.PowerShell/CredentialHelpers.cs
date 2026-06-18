namespace Mailozaurr.PowerShell;

using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Net;
using System.Security;

/// <summary>
/// Helper class for converting strings to SecureString.
/// </summary>
public static class CredentialHelpers {
    /// <summary>
    /// Converts a plain text string to a SecureString.
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    public static SecureString ToSecureString(string? s) {
        if (string.IsNullOrWhiteSpace(s))
            return new SecureString();

        return new NetworkCredential("", s).SecurePassword;
    }

    /// <summary>
    /// Converts a string produced by ConvertFrom-SecureString back to a SecureString.
    /// </summary>
    /// <param name="encryptedString"></param>
    /// <returns></returns>
    public static SecureString ToSecureStringFromEncryptedString(string? encryptedString) {
        if (string.IsNullOrWhiteSpace(encryptedString)) {
            return new SecureString();
        }

        using var powerShell = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        powerShell
            .AddCommand("ConvertTo-SecureString")
            .AddParameter("String", encryptedString!)
            .AddParameter("ErrorAction", ActionPreference.Stop);

        Collection<PSObject> results;
        try {
            results = powerShell.Invoke();
        } catch (RuntimeException ex) {
            throw new PSInvalidOperationException(
                "ClientSecretEncrypted must be a string produced by ConvertFrom-SecureString for the current user and machine.",
                ex);
        }

        if (powerShell.HadErrors) {
            var firstError = powerShell.Streams.Error.FirstOrDefault();
            throw new PSInvalidOperationException(
                firstError?.Exception?.Message ?? "ClientSecretEncrypted could not be converted from its encrypted form.",
                firstError?.Exception);
        }

        var value = results.FirstOrDefault()?.BaseObject;
        if (value is SecureString secureString) {
            return secureString;
        }

        throw new PSInvalidOperationException("ClientSecretEncrypted could not be converted from its encrypted form.");
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
