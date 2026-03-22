namespace Mailozaurr.Application;

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
        }

        if (profile.GetCapabilities().Supports(MailCapability.SendMessages) &&
            string.IsNullOrWhiteSpace(profile.DefaultSender)) {
            result.Warnings.Add("Send-capable profiles should define DefaultSender.");
        }

        if (profile.GetCapabilities().Supports(MailCapability.ReadMessages) &&
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
