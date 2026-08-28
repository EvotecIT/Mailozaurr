namespace Mailozaurr.PowerShell;

/// <summary>Converts PowerShell dictionaries into non-secret profile settings.</summary>
internal static class MailProfileCmdletSettings {
    internal static void Merge(MailProfile profile, IDictionary? settings) {
        if (settings == null) return;
        foreach (DictionaryEntry entry in settings) {
            string key = entry.Key?.ToString()?.Trim() ?? string.Empty;
            if (key.Length == 0) throw new PSArgumentException("Profile setting names cannot be empty.", nameof(settings));
            string value = entry.Value?.ToString() ?? string.Empty;
            profile.Settings[key] = value;
        }
    }
}
