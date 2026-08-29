namespace Mailozaurr;

internal static class MailProfileCredentialContextGuard {
    private static readonly string[] ServerCredentialContextKeys = {
        MailProfileSettingsKeys.Server,
        MailProfileSettingsKeys.Port,
        MailProfileSettingsKeys.SecureSocketOptions,
        MailProfileSettingsKeys.UseSsl,
        MailProfileSettingsKeys.SkipCertificateRevocation,
        MailProfileSettingsKeys.SkipCertificateValidation
    };

    private static readonly string[] JmapCredentialContextKeys = {
        MailProfileSettingsKeys.JmapSessionUrl,
        MailProfileSettingsKeys.JmapAllowCrossOriginApiUrl
    };

    internal static string? GetChangedSetting(MailProfile existing, MailProfile candidate) {
        string[] keys = existing.Kind switch {
            MailProfileKind.Smtp or MailProfileKind.Imap or MailProfileKind.Pop3 => ServerCredentialContextKeys,
            MailProfileKind.Jmap => JmapCredentialContextKeys,
            _ => Array.Empty<string>()
        };

        foreach (string key in keys) {
            existing.Settings.TryGetValue(key, out string? existingValue);
            candidate.Settings.TryGetValue(key, out string? candidateValue);
            if (!AreEquivalent(key, existingValue, candidateValue)) {
                return key;
            }
        }

        return null;
    }

    internal static void EnsureUnchanged(MailProfile existing, MailProfile candidate) {
        string? changedSetting = GetChangedSetting(existing, candidate);
        if (changedSetting != null) {
            throw new MailProfileCredentialContextChangeException(existing.Kind, changedSetting);
        }
    }

    private static bool AreEquivalent(string key, string? first, string? second) {
        string? normalizedFirst = Normalize(first);
        string? normalizedSecond = Normalize(second);
        if (normalizedFirst == null || normalizedSecond == null) {
            return normalizedFirst == normalizedSecond;
        }

        if (string.Equals(key, MailProfileSettingsKeys.Server, StringComparison.OrdinalIgnoreCase)) {
            return string.Equals(normalizedFirst, normalizedSecond, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(key, MailProfileSettingsKeys.Port, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(normalizedFirst, out int firstPort) &&
            int.TryParse(normalizedSecond, out int secondPort)) {
            return firstPort == secondPort;
        }

        if ((string.Equals(key, MailProfileSettingsKeys.UseSsl, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(key, MailProfileSettingsKeys.SkipCertificateRevocation, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(key, MailProfileSettingsKeys.SkipCertificateValidation, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(key, MailProfileSettingsKeys.JmapAllowCrossOriginApiUrl, StringComparison.OrdinalIgnoreCase)) &&
            bool.TryParse(normalizedFirst, out bool firstBoolean) &&
            bool.TryParse(normalizedSecond, out bool secondBoolean)) {
            return firstBoolean == secondBoolean;
        }

        if (string.Equals(key, MailProfileSettingsKeys.JmapSessionUrl, StringComparison.OrdinalIgnoreCase) &&
            Uri.TryCreate(normalizedFirst, UriKind.Absolute, out Uri? firstUri) &&
            Uri.TryCreate(normalizedSecond, UriKind.Absolute, out Uri? secondUri)) {
            return firstUri.Equals(secondUri);
        }

        return string.Equals(normalizedFirst, normalizedSecond, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value!.Trim();
}

internal sealed class MailProfileCredentialContextChangeException : InvalidOperationException {
    internal MailProfileCredentialContextChangeException(MailProfileKind profileKind, string changedSetting)
        : base($"Changing credential-context setting '{changedSetting}' on an existing {profileKind} profile is not allowed. Delete and recreate the profile so stored credentials cannot be redirected.") {
        ProfileKind = profileKind;
        ChangedSetting = changedSetting;
    }

    internal MailProfileKind ProfileKind { get; }
    internal string ChangedSetting { get; }
}
