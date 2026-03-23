namespace Mailozaurr.Application;

/// <summary>
/// Default implementation of reusable dry-run mailbox action previews.
/// </summary>
public sealed class MailMessageActionPreviewService : IMailMessageActionPreviewService {
    private readonly IMailProfileStore _profileStore;
    private readonly IMailFolderAliasService _folderAliases;

    /// <summary>
    /// Creates a new message action preview service.
    /// </summary>
    public MailMessageActionPreviewService(IMailProfileStore profileStore, IMailFolderAliasService folderAliases) {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        _folderAliases = folderAliases ?? throw new ArgumentNullException(nameof(folderAliases));
    }

    /// <inheritdoc />
    public async Task<MessageStateChangePreview> PreviewReadStateAsync(SetReadStateRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var context = await CreateContextAsync(request.ProfileId, request.MessageIds, cancellationToken).ConfigureAwait(false);
        return CreateReadStatePreview(request, context);
    }

    /// <inheritdoc />
    public async Task<MessageStateChangePreview> PreviewFlaggedStateAsync(SetFlaggedStateRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var context = await CreateContextAsync(request.ProfileId, request.MessageIds, cancellationToken).ConfigureAwait(false);
        return CreateFlaggedStatePreview(request, context);
    }

    /// <inheritdoc />
    public async Task<CommonMessageActionsPreview> PreviewCommonActionsAsync(CommonMessageActionsPreviewRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var context = await CreateContextAsync(request.ProfileId, request.MessageIds, cancellationToken).ConfigureAwait(false);
        var preview = CreateCommonPreview(request, context);

        preview.Actions.Add(ToActionItem(
            "mark-read",
            "Mark as read",
            CreateReadStatePreview(new SetReadStateRequest {
                ProfileId = request.ProfileId,
                MailboxId = request.MailboxId,
                FolderId = request.FolderId,
                MessageIds = request.MessageIds,
                IsRead = true
            }, context)));

        preview.Actions.Add(ToActionItem(
            "mark-unread",
            "Mark as unread",
            CreateReadStatePreview(new SetReadStateRequest {
                ProfileId = request.ProfileId,
                MailboxId = request.MailboxId,
                FolderId = request.FolderId,
                MessageIds = request.MessageIds,
                IsRead = false
            }, context)));

        preview.Actions.Add(ToActionItem(
            "flag",
            "Flag",
            CreateFlaggedStatePreview(new SetFlaggedStateRequest {
                ProfileId = request.ProfileId,
                MailboxId = request.MailboxId,
                FolderId = request.FolderId,
                MessageIds = request.MessageIds,
                IsFlagged = true
            }, context)));

        preview.Actions.Add(ToActionItem(
            "unflag",
            "Unflag",
            CreateFlaggedStatePreview(new SetFlaggedStateRequest {
                ProfileId = request.ProfileId,
                MailboxId = request.MailboxId,
                FolderId = request.FolderId,
                MessageIds = request.MessageIds,
                IsFlagged = false
            }, context)));

        var standardPreview = await PreviewStandardActionsAsync(new StandardMessageActionsPreviewRequest {
            ProfileId = request.ProfileId,
            MailboxId = request.MailboxId,
            FolderId = request.FolderId,
            MessageIds = request.MessageIds,
            DestinationFolderId = request.DestinationFolderId
        }, cancellationToken).ConfigureAwait(false);

        preview.Actions.AddRange(standardPreview.Actions);
        preview.IncludedActionCount = preview.Actions.Count;
        preview.SucceededActionCount = preview.Actions.Count(action => action.Succeeded);
        preview.FailedActionCount = preview.IncludedActionCount - preview.SucceededActionCount;
        preview.Succeeded = preview.SucceededActionCount > 0;
        preview.Code = preview.Succeeded ? null : "no_supported_actions";
        preview.Message = preview.Succeeded
            ? $"Prepared {preview.IncludedActionCount} action preview(s) for {preview.UniqueMessageCount} message(s); {preview.SucceededActionCount} supported."
            : $"No previewed actions are currently supported for profile '{preview.ProfileId}'.";
        return preview;
    }

    /// <inheritdoc />
    public async Task<MoveMessagesPreview> PreviewMoveAsync(MoveMessagesPreviewRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var context = await CreateContextAsync(request.ProfileId, request.MessageIds, cancellationToken).ConfigureAwait(false);
        return await CreateMovePreviewAsync(request, context, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DeleteMessagesPreview> PreviewDeleteAsync(DeleteMessagesPreviewRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var context = await CreateContextAsync(request.ProfileId, request.MessageIds, cancellationToken).ConfigureAwait(false);
        return CreateDeletePreview(request, context);
    }

    /// <inheritdoc />
    public async Task<StandardMessageActionsPreview> PreviewStandardActionsAsync(StandardMessageActionsPreviewRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var context = await CreateContextAsync(request.ProfileId, request.MessageIds, cancellationToken).ConfigureAwait(false);
        var preview = CreateStandardPreview(request, context);

        preview.Actions.Add(ToActionItem(
            "archive",
            "Archive",
            await CreateMovePreviewAsync(new MoveMessagesPreviewRequest {
                ProfileId = request.ProfileId,
                MailboxId = request.MailboxId,
                FolderId = request.FolderId,
                MessageIds = request.MessageIds,
                DestinationFolderId = MailFolderAliases.Archive
            }, context, cancellationToken).ConfigureAwait(false)));

        preview.Actions.Add(ToActionItem(
            "trash",
            "Trash",
            await CreateMovePreviewAsync(new MoveMessagesPreviewRequest {
                ProfileId = request.ProfileId,
                MailboxId = request.MailboxId,
                FolderId = request.FolderId,
                MessageIds = request.MessageIds,
                DestinationFolderId = MailFolderAliases.Trash
            }, context, cancellationToken).ConfigureAwait(false)));

        if (!string.IsNullOrWhiteSpace(request.DestinationFolderId)) {
            var trimmedDestination = request.DestinationFolderId!;
            trimmedDestination = trimmedDestination.Trim();
            preview.Actions.Add(ToActionItem(
                "move",
                $"Move to '{trimmedDestination}'",
                await CreateMovePreviewAsync(new MoveMessagesPreviewRequest {
                    ProfileId = request.ProfileId,
                    MailboxId = request.MailboxId,
                    FolderId = request.FolderId,
                    MessageIds = request.MessageIds,
                    DestinationFolderId = trimmedDestination
                }, context, cancellationToken).ConfigureAwait(false)));
        }

        preview.Actions.Add(ToActionItem(
            "delete",
            "Delete",
            CreateDeletePreview(new DeleteMessagesPreviewRequest {
                ProfileId = request.ProfileId,
                MailboxId = request.MailboxId,
                FolderId = request.FolderId,
                MessageIds = request.MessageIds
            }, context)));

        preview.IncludedActionCount = preview.Actions.Count;
        preview.SucceededActionCount = preview.Actions.Count(action => action.Succeeded);
        preview.FailedActionCount = preview.IncludedActionCount - preview.SucceededActionCount;
        preview.Succeeded = preview.SucceededActionCount > 0;
        preview.Code = preview.Succeeded ? null : "no_supported_actions";
        preview.Message = preview.Succeeded
            ? $"Prepared {preview.IncludedActionCount} action preview(s) for {preview.UniqueMessageCount} message(s); {preview.SucceededActionCount} supported."
            : $"No previewed actions are currently supported for profile '{preview.ProfileId}'.";
        return preview;
    }

    private async Task<PreviewContext> CreateContextAsync(string profileId, IReadOnlyList<string> messageIds, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        var profile = await _profileStore.GetByIdAsync(profileId.Trim(), cancellationToken).ConfigureAwait(false);
        if (profile == null) {
            throw new InvalidOperationException($"Profile '{profileId}' was not found.");
        }

        var normalizedMessageIds = NormalizeMessageIds(messageIds);
        return new PreviewContext(profile, messageIds.Count, normalizedMessageIds);
    }

    private async Task<MoveMessagesPreview> CreateMovePreviewAsync(
        MoveMessagesPreviewRequest request,
        PreviewContext context,
        CancellationToken cancellationToken) {
        var preview = CreateMovePreview(request, context.Profile.Id, context.NormalizedMessageIds, context.RequestedCount);

        if (!context.Profile.GetCapabilities().Supports(MailCapability.MoveMessages)) {
            preview.Succeeded = false;
            preview.Code = "move_not_supported";
            preview.Message = $"Profile '{context.Profile.Id}' does not support '{MailCapability.MoveMessages}'.";
            return preview;
        }

        if (context.NormalizedMessageIds.Count == 0) {
            preview.Succeeded = false;
            preview.Code = "messages_required";
            preview.Message = "At least one non-empty message id is required.";
            return preview;
        }

        var resolution = await _folderAliases.ResolveAsync(context.Profile.Id, request.DestinationFolderId, request.MailboxId, cancellationToken).ConfigureAwait(false);
        preview.Destination = resolution;
        if (!resolution.IsSupported) {
            preview.Succeeded = false;
            preview.Code = "destination_not_supported";
            preview.Message = resolution.Summary;
            return preview;
        }

        if (!resolution.IsResolved && resolution.IsAlias) {
            preview.Warnings.Add($"Destination alias '{resolution.Alias}' could not be resolved to a concrete folder and will be used as-is.");
        }

        preview.ConfirmationToken = MessageActionConfirmationTokens.CreateMoveToken(
            context.Profile.Id,
            request.MailboxId,
            request.FolderId,
            context.NormalizedMessageIds,
            resolution.EffectiveFolderId);
        preview.Succeeded = true;
        preview.Code = null;
        preview.Message = $"Move preview ready for {preview.UniqueMessageCount} message(s) to '{resolution.EffectiveFolderId}'.";
        return preview;
    }

    private DeleteMessagesPreview CreateDeletePreview(DeleteMessagesPreviewRequest request, PreviewContext context) {
        var preview = CreateDeletePreview(request, context.Profile.Id, context.NormalizedMessageIds, context.RequestedCount);

        if (!context.Profile.GetCapabilities().Supports(MailCapability.DeleteMessages)) {
            preview.Succeeded = false;
            preview.Code = "delete_not_supported";
            preview.Message = $"Profile '{context.Profile.Id}' does not support '{MailCapability.DeleteMessages}'.";
            return preview;
        }

        if (context.NormalizedMessageIds.Count == 0) {
            preview.Succeeded = false;
            preview.Code = "messages_required";
            preview.Message = "At least one non-empty message id is required.";
            return preview;
        }

        preview.ConfirmationToken = MessageActionConfirmationTokens.CreateDeleteToken(
            context.Profile.Id,
            request.MailboxId,
            request.FolderId,
            context.NormalizedMessageIds);
        preview.Succeeded = true;
        preview.Code = null;
        preview.Message = $"Delete preview ready for {preview.UniqueMessageCount} message(s).";
        return preview;
    }

    private MessageStateChangePreview CreateReadStatePreview(SetReadStateRequest request, PreviewContext context) {
        var preview = CreateStatePreview(
            context,
            request.MailboxId,
            request.FolderId,
            "read-state",
            request.IsRead);
        if (!context.Profile.GetCapabilities().Supports(MailCapability.MarkMessages)) {
            preview.Succeeded = false;
            preview.Code = "mark_not_supported";
            preview.Message = $"Profile '{context.Profile.Id}' does not support '{MailCapability.MarkMessages}'.";
            return preview;
        }

        if (context.NormalizedMessageIds.Count == 0) {
            preview.Succeeded = false;
            preview.Code = "messages_required";
            preview.Message = "At least one non-empty message id is required.";
            return preview;
        }

        preview.ConfirmationToken = MessageActionConfirmationTokens.CreateReadStateToken(
            context.Profile.Id,
            request.MailboxId,
            request.FolderId,
            context.NormalizedMessageIds,
            request.IsRead);
        preview.Succeeded = true;
        preview.Message = request.IsRead
            ? $"Read-state preview ready for {preview.UniqueMessageCount} message(s)."
            : $"Unread-state preview ready for {preview.UniqueMessageCount} message(s).";
        return preview;
    }

    private MessageStateChangePreview CreateFlaggedStatePreview(SetFlaggedStateRequest request, PreviewContext context) {
        var preview = CreateStatePreview(
            context,
            request.MailboxId,
            request.FolderId,
            "flagged-state",
            request.IsFlagged);
        if (!context.Profile.GetCapabilities().Supports(MailCapability.MarkMessages)) {
            preview.Succeeded = false;
            preview.Code = "mark_not_supported";
            preview.Message = $"Profile '{context.Profile.Id}' does not support '{MailCapability.MarkMessages}'.";
            return preview;
        }

        if (context.NormalizedMessageIds.Count == 0) {
            preview.Succeeded = false;
            preview.Code = "messages_required";
            preview.Message = "At least one non-empty message id is required.";
            return preview;
        }

        preview.ConfirmationToken = MessageActionConfirmationTokens.CreateFlaggedStateToken(
            context.Profile.Id,
            request.MailboxId,
            request.FolderId,
            context.NormalizedMessageIds,
            request.IsFlagged);
        preview.Succeeded = true;
        preview.Message = request.IsFlagged
            ? $"Flag preview ready for {preview.UniqueMessageCount} message(s)."
            : $"Unflag preview ready for {preview.UniqueMessageCount} message(s).";
        return preview;
    }

    private static List<string> NormalizeMessageIds(IReadOnlyList<string> messageIds) =>
        messageIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static MoveMessagesPreview CreateMovePreview(
        MoveMessagesPreviewRequest request,
        string profileId,
        List<string> normalizedMessageIds,
        int requestedCount) {
        var preview = new MoveMessagesPreview {
            ProfileId = profileId,
            MailboxId = request.MailboxId,
            FolderId = request.FolderId,
            RequestedDestinationFolderId = request.DestinationFolderId,
            RequestedCount = requestedCount,
            MessageIds = normalizedMessageIds,
            UniqueMessageCount = normalizedMessageIds.Count,
            DuplicateOrEmptyCount = requestedCount - normalizedMessageIds.Count
        };

        if (preview.DuplicateOrEmptyCount > 0) {
            preview.Warnings.Add($"Ignored {preview.DuplicateOrEmptyCount} duplicate or empty message id value(s).");
        }

        return preview;
    }

    private static DeleteMessagesPreview CreateDeletePreview(
        DeleteMessagesPreviewRequest request,
        string profileId,
        List<string> normalizedMessageIds,
        int requestedCount) {
        var preview = new DeleteMessagesPreview {
            ProfileId = profileId,
            MailboxId = request.MailboxId,
            FolderId = request.FolderId,
            RequestedCount = requestedCount,
            MessageIds = normalizedMessageIds,
            UniqueMessageCount = normalizedMessageIds.Count,
            DuplicateOrEmptyCount = requestedCount - normalizedMessageIds.Count
        };

        if (preview.DuplicateOrEmptyCount > 0) {
            preview.Warnings.Add($"Ignored {preview.DuplicateOrEmptyCount} duplicate or empty message id value(s).");
        }

        return preview;
    }

    private static StandardMessageActionsPreview CreateStandardPreview(StandardMessageActionsPreviewRequest request, PreviewContext context) {
        var requestedDestinationFolderId = string.IsNullOrWhiteSpace(request.DestinationFolderId)
            ? null
            : request.DestinationFolderId!.Trim();
        var preview = new StandardMessageActionsPreview {
            ProfileId = context.Profile.Id,
            MailboxId = request.MailboxId,
            FolderId = request.FolderId,
            RequestedDestinationFolderId = requestedDestinationFolderId,
            RequestedCount = context.RequestedCount,
            MessageIds = context.NormalizedMessageIds,
            UniqueMessageCount = context.NormalizedMessageIds.Count,
            DuplicateOrEmptyCount = context.RequestedCount - context.NormalizedMessageIds.Count
        };

        if (preview.DuplicateOrEmptyCount > 0) {
            preview.Warnings.Add($"Ignored {preview.DuplicateOrEmptyCount} duplicate or empty message id value(s).");
        }

        return preview;
    }

    private static MessageStateChangePreview CreateStatePreview(
        PreviewContext context,
        string? mailboxId,
        string? folderId,
        string action,
        bool desiredState) {
        var preview = new MessageStateChangePreview {
            ProfileId = context.Profile.Id,
            MailboxId = mailboxId,
            FolderId = folderId,
            Action = action,
            DesiredState = desiredState,
            RequestedCount = context.RequestedCount,
            MessageIds = context.NormalizedMessageIds,
            UniqueMessageCount = context.NormalizedMessageIds.Count,
            DuplicateOrEmptyCount = context.RequestedCount - context.NormalizedMessageIds.Count
        };

        if (preview.DuplicateOrEmptyCount > 0) {
            preview.Warnings.Add($"Ignored {preview.DuplicateOrEmptyCount} duplicate or empty message id value(s).");
        }

        return preview;
    }

    private static MessageActionPreviewItem ToActionItem(string action, string displayName, MoveMessagesPreview preview) =>
        new() {
            Action = action,
            DisplayName = displayName,
            Succeeded = preview.Succeeded,
            Code = preview.Code,
            Message = preview.Message,
            RequestedDestinationFolderId = preview.RequestedDestinationFolderId,
            Destination = preview.Destination,
            ConfirmationToken = preview.ConfirmationToken,
            Warnings = new List<string>(preview.Warnings)
        };

    private static MessageActionPreviewItem ToActionItem(string action, string displayName, DeleteMessagesPreview preview) =>
        new() {
            Action = action,
            DisplayName = displayName,
            Succeeded = preview.Succeeded,
            Code = preview.Code,
            Message = preview.Message,
            ConfirmationToken = preview.ConfirmationToken,
            Warnings = new List<string>(preview.Warnings)
        };

    private static MessageActionPreviewItem ToActionItem(string action, string displayName, MessageStateChangePreview preview) =>
        new() {
            Action = action,
            DisplayName = displayName,
            Succeeded = preview.Succeeded,
            Code = preview.Code,
            Message = preview.Message,
            DesiredState = preview.DesiredState,
            ConfirmationToken = preview.ConfirmationToken,
            Warnings = new List<string>(preview.Warnings)
        };

    private static CommonMessageActionsPreview CreateCommonPreview(CommonMessageActionsPreviewRequest request, PreviewContext context) {
        var requestedDestinationFolderId = string.IsNullOrWhiteSpace(request.DestinationFolderId)
            ? null
            : request.DestinationFolderId!.Trim();
        var preview = new CommonMessageActionsPreview {
            ProfileId = context.Profile.Id,
            MailboxId = request.MailboxId,
            FolderId = request.FolderId,
            RequestedDestinationFolderId = requestedDestinationFolderId,
            RequestedCount = context.RequestedCount,
            MessageIds = context.NormalizedMessageIds,
            UniqueMessageCount = context.NormalizedMessageIds.Count,
            DuplicateOrEmptyCount = context.RequestedCount - context.NormalizedMessageIds.Count
        };

        if (preview.DuplicateOrEmptyCount > 0) {
            preview.Warnings.Add($"Ignored {preview.DuplicateOrEmptyCount} duplicate or empty message id value(s).");
        }

        return preview;
    }

    private sealed class PreviewContext {
        public PreviewContext(MailProfile profile, int requestedCount, List<string> normalizedMessageIds) {
            Profile = profile;
            RequestedCount = requestedCount;
            NormalizedMessageIds = normalizedMessageIds;
        }

        public MailProfile Profile { get; }

        public int RequestedCount { get; }

        public List<string> NormalizedMessageIds { get; }
    }
}
