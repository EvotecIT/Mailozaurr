namespace Mailozaurr;

internal static class ProviderMailboxProfileResolver {
    internal static async Task<MailProfile> GetAsync(
        IMailProfileStore profileStore,
        string profileId,
        MailProfileKind expectedKind,
        string? mailboxId,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("Profile id is required.", nameof(profileId));
        var profile = await profileStore.GetByIdAsync(profileId.Trim(), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");
        if (profile.Kind != expectedKind) {
            throw new NotSupportedException($"Profile '{profile.Id}' is '{profile.Kind}', not '{expectedKind}'.");
        }
        return WithMailbox(profile, mailboxId);
    }

    internal static MailProfile WithMailbox(MailProfile profile, string? mailboxId) {
        if (string.IsNullOrWhiteSpace(mailboxId)) return profile;
        var mailbox = mailboxId!.Trim();
        return new MailProfile {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            Description = profile.Description,
            Kind = profile.Kind,
            DefaultSender = profile.DefaultSender,
            DefaultMailbox = mailbox,
            IsDefault = profile.IsDefault,
            Settings = new Dictionary<string, string>(profile.Settings, StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Mailbox] = mailbox
            },
            Capabilities = profile.Capabilities
        };
    }
}
