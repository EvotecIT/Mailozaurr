namespace Mailozaurr;

internal static class MailProfileCredentialContextGuard {
    private static readonly string[] ServerCredentialContextKeys = {
        MailProfileSettingsKeys.Server,
        MailProfileSettingsKeys.Port,
        MailProfileSettingsKeys.SecureSocketOptions,
        MailProfileSettingsKeys.UseSsl,
        MailProfileSettingsKeys.AuthMode,
        MailProfileSettingsKeys.AuthenticationEnabled,
        MailProfileSettingsKeys.SkipCertificateRevocation,
        MailProfileSettingsKeys.SkipCertificateValidation
    };

    private static readonly string[] JmapCredentialContextKeys = {
        MailProfileSettingsKeys.JmapSessionUrl,
        MailProfileSettingsKeys.JmapAccountId,
        MailProfileSettingsKeys.JmapAllowCrossOriginApiUrl
    };

    private static readonly string[] GraphCredentialContextKeys = {
        MailProfileSettingsKeys.ClientId,
        MailProfileSettingsKeys.TenantId
    };

    private static readonly string[] GmailCredentialContextKeys = {
        MailProfileSettingsKeys.ClientId
    };

    private static readonly string[] SesCredentialContextKeys = {
        MailProfileSettingsKeys.Region
    };

    internal static string? GetChangedSetting(MailProfile existing, MailProfile candidate) {
        string[] keys = existing.Kind switch {
            MailProfileKind.Smtp or MailProfileKind.Imap or MailProfileKind.Pop3 => ServerCredentialContextKeys,
            MailProfileKind.Graph => GraphCredentialContextKeys,
            MailProfileKind.Gmail => GmailCredentialContextKeys,
            MailProfileKind.Jmap => JmapCredentialContextKeys,
            MailProfileKind.Ses => SesCredentialContextKeys,
            _ => Array.Empty<string>()
        };

        string? existingIdentity = ResolveCredentialIdentity(existing);
        string? candidateIdentity = ResolveCredentialIdentity(candidate);
        if (!string.Equals(existingIdentity, candidateIdentity, StringComparison.OrdinalIgnoreCase)) {
            return existing.Kind is MailProfileKind.Smtp or MailProfileKind.Imap or MailProfileKind.Pop3
                ? MailProfileSettingsKeys.UserName
                : MailProfileSettingsKeys.Mailbox;
        }

        foreach (string key in keys) {
            existing.Settings.TryGetValue(key, out string? existingValue);
            candidate.Settings.TryGetValue(key, out string? candidateValue);
            if (!AreEquivalent(key, existingValue, candidateValue)) {
                return key;
            }
        }

        return null;
    }

    private static string? ResolveCredentialIdentity(MailProfile profile) {
        if (profile.Kind is MailProfileKind.Smtp or MailProfileKind.Imap or MailProfileKind.Pop3) {
            if (profile.Settings.TryGetValue(MailProfileSettingsKeys.UserName, out string? userName) &&
                !string.IsNullOrWhiteSpace(userName)) return userName.Trim();
            if (profile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out string? mailbox) &&
                !string.IsNullOrWhiteSpace(mailbox)) return mailbox.Trim();
            if (!string.IsNullOrWhiteSpace(profile.DefaultMailbox)) return profile.DefaultMailbox!.Trim();
            if (profile.Kind == MailProfileKind.Smtp && !string.IsNullOrWhiteSpace(profile.DefaultSender)) {
                return profile.DefaultSender!.Trim();
            }
            return null;
        }

        if (profile.Kind is MailProfileKind.Graph or MailProfileKind.Gmail) {
            if (profile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out string? mailbox) &&
                !string.IsNullOrWhiteSpace(mailbox)) return mailbox.Trim();
            if (!string.IsNullOrWhiteSpace(profile.DefaultMailbox)) return profile.DefaultMailbox!.Trim();
            return "me";
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
        if (string.Equals(key, MailProfileSettingsKeys.Region, StringComparison.OrdinalIgnoreCase)) {
            normalizedFirst ??= "us-east-1";
            normalizedSecond ??= "us-east-1";
        }
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
