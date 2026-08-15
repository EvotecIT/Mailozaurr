namespace Mailozaurr.Hosting;

/// <summary>
/// Resolves a profile and routes send operations to the matching provider handler.
/// </summary>
public sealed class RoutedMailSendService : IMailSendService {
    private readonly IMailProfileStore _profileStore;
    private readonly IReadOnlyDictionary<MailProfileKind, IMailSendHandler> _handlers;

    /// <summary>
    /// Creates a new routed send service.
    /// </summary>
    public RoutedMailSendService(IMailProfileStore profileStore, IEnumerable<IMailSendHandler> handlers) {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        if (handlers == null) {
            throw new ArgumentNullException(nameof(handlers));
        }

        _handlers = handlers.ToDictionary(handler => handler.Kind);
    }

    /// <inheritdoc />
    public async Task<SendResult> SendAsync(SendMessageRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var profile = await _profileStore.GetByIdAsync(request.ProfileId, cancellationToken).ConfigureAwait(false);
        if (profile == null) {
            throw new InvalidOperationException($"Profile '{request.ProfileId}' was not found.");
        }

        if (!_handlers.TryGetValue(profile.Kind, out var handler)) {
            throw new NotSupportedException($"No send handler is registered for profile kind '{profile.Kind}'.");
        }
        if (!profile.GetCapabilities(MailCapability.SendMessages).Supports(MailCapability.SendMessages)) {
            throw new NotSupportedException($"Profile '{profile.Id}' does not support '{MailCapability.SendMessages}'.");
        }

        return await handler.SendAsync(profile, request, cancellationToken).ConfigureAwait(false);
    }
}
