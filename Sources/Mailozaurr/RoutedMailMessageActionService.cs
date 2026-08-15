namespace Mailozaurr;

/// <summary>
/// Resolves a profile and routes message actions to the matching provider handler.
/// </summary>
public sealed class RoutedMailMessageActionService : IMailMessageActionService {
    private const MailCapability HandlerCapabilities = MailCapability.MarkMessages
        | MailCapability.MoveMessages
        | MailCapability.DeleteMessages;
    private readonly IMailProfileStore _profileStore;
    private readonly IMailFolderAliasService? _folderAliases;
    private readonly IReadOnlyDictionary<MailProfileKind, IMailMessageActionHandler> _handlers;

    /// <summary>
    /// Creates a new routed message-action service.
    /// </summary>
    public RoutedMailMessageActionService(
        IMailProfileStore profileStore,
        IEnumerable<IMailMessageActionHandler> handlers,
        IMailFolderAliasService? folderAliases = null) {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        _folderAliases = folderAliases;
        if (handlers == null) {
            throw new ArgumentNullException(nameof(handlers));
        }

        _handlers = handlers.ToDictionary(handler => handler.Kind);
    }

    /// <inheritdoc />
    public async Task<MessageActionResult> SetReadStateAsync(SetReadStateRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var profile = await GetProfileAsync(request.ProfileId, cancellationToken).ConfigureAwait(false);
        EnsureCapability(profile, MailCapability.MarkMessages);
        var confirmationFailure = ValidateReadStateConfirmation(request);
        if (confirmationFailure != null) {
            confirmationFailure.ProfileId = profile.Id;
            confirmationFailure.RequestedCount = request.MessageIds.Count;
            confirmationFailure.FailedCount = request.MessageIds.Count;
            return confirmationFailure;
        }
        return await GetHandler(profile.Kind).SetReadStateAsync(profile, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MessageActionResult> SetFlaggedStateAsync(SetFlaggedStateRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var profile = await GetProfileAsync(request.ProfileId, cancellationToken).ConfigureAwait(false);
        EnsureCapability(profile, MailCapability.MarkMessages);
        var confirmationFailure = ValidateFlaggedStateConfirmation(request);
        if (confirmationFailure != null) {
            confirmationFailure.ProfileId = profile.Id;
            confirmationFailure.RequestedCount = request.MessageIds.Count;
            confirmationFailure.FailedCount = request.MessageIds.Count;
            return confirmationFailure;
        }
        return await GetHandler(profile.Kind).SetFlaggedStateAsync(profile, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MessageActionResult> MoveAsync(MoveMessagesRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var profile = await GetProfileAsync(request.ProfileId, cancellationToken).ConfigureAwait(false);
        EnsureCapability(profile, MailCapability.MoveMessages);
        var normalizedRequest = await NormalizeMoveRequestAsync(profile, request, cancellationToken).ConfigureAwait(false);
        var confirmationFailure = ValidateMoveConfirmation(normalizedRequest);
        if (confirmationFailure != null) {
            confirmationFailure.ProfileId = profile.Id;
            confirmationFailure.RequestedCount = request.MessageIds.Count;
            confirmationFailure.FailedCount = request.MessageIds.Count;
            return confirmationFailure;
        }
        return await GetHandler(profile.Kind).MoveAsync(profile, normalizedRequest, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MessageActionResult> DeleteAsync(DeleteMessagesRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var profile = await GetProfileAsync(request.ProfileId, cancellationToken).ConfigureAwait(false);
        EnsureCapability(profile, MailCapability.DeleteMessages);
        var confirmationFailure = ValidateDeleteConfirmation(request);
        if (confirmationFailure != null) {
            confirmationFailure.ProfileId = profile.Id;
            confirmationFailure.RequestedCount = request.MessageIds.Count;
            confirmationFailure.FailedCount = request.MessageIds.Count;
            return confirmationFailure;
        }
        return await GetHandler(profile.Kind).DeleteAsync(profile, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MailProfile> GetProfileAsync(string profileId, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        var profile = await _profileStore.GetByIdAsync(profileId, cancellationToken).ConfigureAwait(false);
        return profile ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");
    }

    private IMailMessageActionHandler GetHandler(MailProfileKind kind) =>
        _handlers.TryGetValue(kind, out var handler)
            ? handler
            : throw new NotSupportedException($"No message-action handler is registered for profile kind '{kind}'.");

    private async Task<MoveMessagesRequest> NormalizeMoveRequestAsync(
        MailProfile profile,
        MoveMessagesRequest request,
        CancellationToken cancellationToken) {
        var canonicalAlias = MailFolderAliases.Canonicalize(request.DestinationFolderId);
        if (canonicalAlias == null) {
            return request;
        }

        if (_folderAliases == null) {
            if (string.Equals(request.DestinationFolderId, canonicalAlias, StringComparison.Ordinal)) {
                return request;
            }

            return CloneMoveRequest(request, canonicalAlias);
        }

        var resolution = await _folderAliases.ResolveAsync(profile.Id, request.DestinationFolderId, request.MailboxId, cancellationToken).ConfigureAwait(false);
        if (!resolution.IsSupported) {
            throw new NotSupportedException($"Profile '{profile.Id}' does not support folder alias '{resolution.Alias ?? request.DestinationFolderId}'.");
        }

        var destinationFolderId = resolution.EffectiveFolderId;
        if (string.Equals(request.DestinationFolderId, destinationFolderId, StringComparison.Ordinal)) {
            return request;
        }

        return CloneMoveRequest(request, destinationFolderId);
    }

    private static MoveMessagesRequest CloneMoveRequest(MoveMessagesRequest request, string destinationFolderId) =>
        new() {
            ProfileId = request.ProfileId,
            MailboxId = request.MailboxId,
            FolderId = request.FolderId,
            MessageIds = request.MessageIds.ToList(),
            DestinationFolderId = destinationFolderId,
            ConfirmationToken = request.ConfirmationToken
        };

    private static MessageActionResult? ValidateMoveConfirmation(MoveMessagesRequest request) {
        if (string.IsNullOrWhiteSpace(request.ConfirmationToken)) {
            return MissingConfirmation("move");
        }

        var expectedToken = MessageActionConfirmationTokens.CreateMoveToken(
            request.ProfileId,
            request.MailboxId,
            request.FolderId,
            request.MessageIds,
            request.DestinationFolderId);
        var providedToken = request.ConfirmationToken!;
        providedToken = providedToken.Trim();
        if (string.Equals(expectedToken, providedToken, StringComparison.Ordinal)) {
            return null;
        }

        return new MessageActionResult {
            Succeeded = false,
            Code = "confirmation_token_mismatch",
            Message = "The supplied confirmation token does not match this move action."
        };
    }

    private static MessageActionResult? ValidateDeleteConfirmation(DeleteMessagesRequest request) {
        if (string.IsNullOrWhiteSpace(request.ConfirmationToken)) {
            return MissingConfirmation("delete");
        }

        var expectedToken = MessageActionConfirmationTokens.CreateDeleteToken(
            request.ProfileId,
            request.MailboxId,
            request.FolderId,
            request.MessageIds);
        var providedToken = request.ConfirmationToken!;
        providedToken = providedToken.Trim();
        if (string.Equals(expectedToken, providedToken, StringComparison.Ordinal)) {
            return null;
        }

        return new MessageActionResult {
            Succeeded = false,
            Code = "confirmation_token_mismatch",
            Message = "The supplied confirmation token does not match this delete action."
        };
    }

    private static MessageActionResult? ValidateReadStateConfirmation(SetReadStateRequest request) {
        if (string.IsNullOrWhiteSpace(request.ConfirmationToken)) {
            return MissingConfirmation("read-state");
        }

        var expectedToken = MessageActionConfirmationTokens.CreateReadStateToken(
            request.ProfileId,
            request.MailboxId,
            request.FolderId,
            request.MessageIds,
            request.IsRead);
        var providedToken = request.ConfirmationToken!;
        providedToken = providedToken.Trim();
        if (string.Equals(expectedToken, providedToken, StringComparison.Ordinal)) {
            return null;
        }

        return new MessageActionResult {
            Succeeded = false,
            Code = "confirmation_token_mismatch",
            Message = "The supplied confirmation token does not match this read-state action."
        };
    }

    private static MessageActionResult? ValidateFlaggedStateConfirmation(SetFlaggedStateRequest request) {
        if (string.IsNullOrWhiteSpace(request.ConfirmationToken)) {
            return MissingConfirmation("flagged-state");
        }

        var expectedToken = MessageActionConfirmationTokens.CreateFlaggedStateToken(
            request.ProfileId,
            request.MailboxId,
            request.FolderId,
            request.MessageIds,
            request.IsFlagged);
        var providedToken = request.ConfirmationToken!;
        providedToken = providedToken.Trim();
        if (string.Equals(expectedToken, providedToken, StringComparison.Ordinal)) {
            return null;
        }

        return new MessageActionResult {
            Succeeded = false,
            Code = "confirmation_token_mismatch",
            Message = "The supplied confirmation token does not match this flagged-state action."
        };
    }

    private static void EnsureCapability(MailProfile profile, MailCapability capability) {
        if (!profile.GetCapabilities(HandlerCapabilities).Supports(capability)) {
            throw new NotSupportedException($"Profile '{profile.Id}' does not support '{capability}'.");
        }
    }

    private static MessageActionResult MissingConfirmation(string action) => new() {
        Succeeded = false,
        Code = "confirmation_token_required",
        Message = $"A confirmation token is required for this {action} action. Preview the action first and pass the generated token."
    };
}
