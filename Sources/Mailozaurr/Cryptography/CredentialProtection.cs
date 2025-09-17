using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Mailozaurr;

/// <summary>
/// Provides access to credential protection services and legacy compatibility helpers.
/// </summary>
public static class CredentialProtection {
    private static readonly object SyncRoot = new();
    private static ICredentialProtector? instance;

    /// <summary>
    /// Gets the default credential protector for the current platform.
    /// </summary>
    public static ICredentialProtector Default {
        get {
            if (instance != null) {
                return instance;
            }

            lock (SyncRoot) {
                instance ??= CreateDefaultProtector();
            }

            return instance;
        }
    }

    /// <summary>
    /// Allows tests to replace the default protector.
    /// </summary>
    /// <param name="protector">Protector instance used for subsequent operations.</param>
    internal static void SetDefault(ICredentialProtector protector) {
        if (protector == null) {
            throw new ArgumentNullException(nameof(protector));
        }

        lock (SyncRoot) {
            instance = protector;
        }
    }

    /// <summary>
    /// Resets the cached protector so the next access recreates it.
    /// </summary>
    internal static void Reset() {
        lock (SyncRoot) {
            instance = null;
        }
    }

    /// <summary>
    /// Attempts to decrypt <paramref name="protectedData"/> and gracefully falls back to
    /// plain Base64 decoding for legacy records created prior to introducing
    /// <see cref="ICredentialProtector"/> on non-Windows platforms.
    /// </summary>
    /// <param name="protectedData">Base64 encoded protected payload.</param>
    /// <returns>The decrypted secret or an empty string when decoding fails.</returns>
    internal static string UnprotectWithFallback(string? protectedData) => UnprotectWithFallback(Default, protectedData);

    internal static string UnprotectWithFallback(ICredentialProtector protector, string? protectedData) {
        if (protector == null) {
            throw new ArgumentNullException(nameof(protector));
        }

        if (string.IsNullOrEmpty(protectedData)) {
            return string.Empty;
        }

        try {
            return protector.Unprotect(protectedData);
        } catch (FormatException) {
            // Fall back to legacy behaviour below.
        } catch (CryptographicException) {
            // Fall back to legacy behaviour below.
        } catch (ArgumentException) {
            // Fall back to legacy behaviour below.
        }

        try {
            var raw = Convert.FromBase64String(protectedData);
            return Encoding.UTF8.GetString(raw);
        } catch {
            return string.Empty;
        }
    }

    private static ICredentialProtector CreateDefaultProtector() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            return new WindowsCredentialProtector();
        }

        return new AesCredentialProtector();
    }
}
