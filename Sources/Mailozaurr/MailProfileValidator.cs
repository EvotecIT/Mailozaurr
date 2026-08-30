namespace Mailozaurr;

/// <summary>
/// Validates profile definitions before they are saved or used.
/// </summary>
public static class MailProfileValidator {
    /// <summary>
    /// Validates a profile and returns errors and warnings.
    /// </summary>
    public static MailProfileValidationResult Validate(MailProfile? profile) {
        var result = new MailProfileValidationResult();
        if (profile == null) {
            result.Succeeded = false;
            result.Code = "profile_missing";
            result.Errors.Add("Profile is required.");
            result.Message = result.Errors[0];
            return result;
        }

        if (string.IsNullOrWhiteSpace(profile.Id)) {
            result.Errors.Add("Profile id is required.");
        } else if (profile.Id.Any(char.IsWhiteSpace)) {
            result.Warnings.Add("Profile id contains whitespace. Hyphenated identifiers are recommended.");
        }

        if (string.IsNullOrWhiteSpace(profile.DisplayName)) {
            result.Errors.Add("Profile display name is required.");
        }

        if (profile.Kind == MailProfileKind.Unknown) {
            result.Errors.Add("Profile kind must be specified.");
        }

        switch (profile.Kind) {
            case MailProfileKind.Imap:
            case MailProfileKind.Pop3:
            case MailProfileKind.Smtp:
                RequireSetting(profile, MailProfileSettingsKeys.Server, result);
                ValidateOptionalPort(profile, result);
                break;
            case MailProfileKind.Graph:
                RequireOneOf(profile, result, MailProfileSettingsKeys.Mailbox, "defaultMailbox");
                break;
            case MailProfileKind.Gmail:
                RequireOneOf(profile, result, MailProfileSettingsKeys.Mailbox, "defaultMailbox");
                break;
            case MailProfileKind.Jmap:
                RequireSetting(profile, MailProfileSettingsKeys.JmapSessionUrl, result);
                if (profile.Settings.TryGetValue(MailProfileSettingsKeys.JmapSessionUrl, out var jmapSessionUrl) &&
                    !IsValidJmapSessionUrl(jmapSessionUrl)) {
                    result.Errors.Add($"Profile setting '{MailProfileSettingsKeys.JmapSessionUrl}' must be an absolute HTTPS URL without user information or a fragment.");
                }
                if (profile.Settings.TryGetValue(MailProfileSettingsKeys.JmapAllowCrossOriginApiUrl, out var allowCrossOrigin) &&
                    !bool.TryParse(allowCrossOrigin, out _)) {
                    result.Errors.Add($"Profile setting '{MailProfileSettingsKeys.JmapAllowCrossOriginApiUrl}' must be true or false.");
                }
                break;
            case MailProfileKind.Ses:
                if (profile.Settings.TryGetValue(MailProfileSettingsKeys.Region, out var region) &&
                    !SesRegionName.IsValid(region)) {
                    result.Errors.Add($"Profile setting '{MailProfileSettingsKeys.Region}' must be a valid Amazon SES region name.");
                }
                break;
        }

        if (profile.GetCapabilities().Supports(MailCapability.SendMessages) &&
            string.IsNullOrWhiteSpace(profile.DefaultSender)) {
            result.Warnings.Add("Send-capable profiles should define DefaultSender.");
        }

        if (profile.GetCapabilities().Supports(MailCapability.ReadMessages) &&
            profile.Kind != MailProfileKind.Jmap &&
            string.IsNullOrWhiteSpace(profile.DefaultMailbox) &&
            !profile.Settings.ContainsKey(MailProfileSettingsKeys.Mailbox)) {
            result.Warnings.Add("Read-capable profiles should define DefaultMailbox or a mailbox setting.");
        }

        result.Succeeded = result.Errors.Count == 0;
        result.Code = result.Succeeded ? null : "profile_invalid";
        result.Message = result.Succeeded
            ? (result.Warnings.Count == 0 ? "Profile is valid." : "Profile is valid with warnings.")
            : result.Errors[0];
        return result;
    }

    private static bool IsValidJmapSessionUrl(string value) {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        try {
            _ = JmapApiClient.ValidateSessionUrl(uri);
            return true;
        } catch (ArgumentException) {
            return false;
        }
    }

    private static void RequireSetting(MailProfile profile, string key, MailProfileValidationResult result) {
        if (!profile.Settings.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) {
            result.Errors.Add($"Profile setting '{key}' is required for {profile.Kind} profiles.");
        }
    }

    private static void RequireOneOf(MailProfile profile, MailProfileValidationResult result, params string[] keys) {
        foreach (var key in keys) {
            if (string.Equals(key, "defaultMailbox", StringComparison.OrdinalIgnoreCase)) {
                if (!string.IsNullOrWhiteSpace(profile.DefaultMailbox)) {
                    return;
                }
            } else if (profile.Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) {
                return;
            }
        }

        result.Warnings.Add($"Profile should define one of: {string.Join(", ", keys)}.");
    }

    private static void ValidateOptionalPort(MailProfile profile, MailProfileValidationResult result) {
        if (!profile.Settings.TryGetValue(MailProfileSettingsKeys.Port, out var value) || string.IsNullOrWhiteSpace(value)) {
            return;
        }

        if (!int.TryParse(value, out var port) || port <= 0 || port > 65535) {
            result.Errors.Add($"Profile setting '{MailProfileSettingsKeys.Port}' must be a valid TCP port.");
        }
    }

}
