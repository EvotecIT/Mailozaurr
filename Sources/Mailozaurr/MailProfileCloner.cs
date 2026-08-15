namespace Mailozaurr;

internal static class MailProfileCloner {
    public static IReadOnlyList<MailProfile> CloneAll(IEnumerable<MailProfile> profiles) =>
        profiles.Select(Clone).ToArray();

    public static MailProfile Clone(MailProfile profile) => new() {
        Id = profile.Id,
        DisplayName = profile.DisplayName,
        Description = profile.Description,
        Kind = profile.Kind,
        DefaultSender = profile.DefaultSender,
        DefaultMailbox = profile.DefaultMailbox,
        IsDefault = profile.IsDefault,
        Settings = new Dictionary<string, string>(profile.Settings, StringComparer.OrdinalIgnoreCase),
        Capabilities = profile.Capabilities == null
            ? null
            : new ProfileCapabilities(profile.Capabilities.Kind, profile.Capabilities.Capabilities)
    };

    public static void Validate(MailProfile? profile) {
        if (profile == null) {
            throw new ArgumentNullException(nameof(profile));
        }

        if (string.IsNullOrWhiteSpace(profile.Id)) {
            throw new InvalidOperationException("Profile id is required.");
        }

        if (string.IsNullOrWhiteSpace(profile.DisplayName)) {
            throw new InvalidOperationException("Profile display name is required.");
        }
    }
}
