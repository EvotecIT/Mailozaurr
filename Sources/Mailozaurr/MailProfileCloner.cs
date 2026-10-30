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
        SmtpDelivery = CloneSmtpDelivery(profile.SmtpDelivery),
        Capabilities = profile.Capabilities == null
            ? null
            : new ProfileCapabilities(profile.Capabilities.Kind, profile.Capabilities.Capabilities)
    };

    private static SmtpDeliveryOptions? CloneSmtpDelivery(SmtpDeliveryOptions? options) => options == null ? null : new() {
        Strategy = options.Strategy,
        MaxAttempts = options.MaxAttempts,
        CircuitBreakMinutes = options.CircuitBreakMinutes,
        Servers = options.Servers.Select(server => new SmtpEndpoint {
            Server = server.Server,
            Port = server.Port,
            SecureSocketOptions = server.SecureSocketOptions
        }).ToList()
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
