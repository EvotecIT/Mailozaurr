namespace Mailozaurr;

/// <summary>
/// Provides reusable dry-run previews for mailbox message actions.
/// </summary>
public interface IMailMessageActionPreviewService {
    /// <summary>Previews a read/unread state change without executing it.</summary>
    Task<MessageStateChangePreview> PreviewReadStateAsync(SetReadStateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Previews a flagged/unflagged state change without executing it.</summary>
    Task<MessageStateChangePreview> PreviewFlaggedStateAsync(SetFlaggedStateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Previews a common bundle of state-change and mailbox actions side by side without executing them.</summary>
    Task<CommonMessageActionsPreview> PreviewCommonActionsAsync(CommonMessageActionsPreviewRequest request, CancellationToken cancellationToken = default);

    /// <summary>Previews a message move without executing it.</summary>
    Task<MoveMessagesPreview> PreviewMoveAsync(MoveMessagesPreviewRequest request, CancellationToken cancellationToken = default);

    /// <summary>Previews a message delete without executing it.</summary>
    Task<DeleteMessagesPreview> PreviewDeleteAsync(DeleteMessagesPreviewRequest request, CancellationToken cancellationToken = default);

    /// <summary>Previews the standard set of mailbox actions side by side without executing them.</summary>
    Task<StandardMessageActionsPreview> PreviewStandardActionsAsync(StandardMessageActionsPreviewRequest request, CancellationToken cancellationToken = default);
}