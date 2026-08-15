namespace Mailozaurr.Hosting;

/// <summary>
/// Implements reusable draft lifecycle operations.
/// </summary>
public sealed class MailDraftService : IMailDraftService {
    private readonly IMailDraftStore _store;
    private readonly IMailProfileStore _profileStore;

    /// <summary>
    /// Creates a new draft service.
    /// </summary>
    public MailDraftService(IMailDraftStore store, IMailProfileStore profileStore) {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MailDraft>> GetDraftsAsync(CancellationToken cancellationToken = default) =>
        _store.GetAllAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<MailDraftCompact>> GetDraftsCompactAsync(CancellationToken cancellationToken = default) =>
        (await _store.GetAllAsync(cancellationToken).ConfigureAwait(false))
        .Select(ToCompact)
        .ToArray();

    /// <inheritdoc />
    public Task<MailDraft?> GetDraftAsync(string draftId, CancellationToken cancellationToken = default) =>
        _store.GetByIdAsync(draftId, cancellationToken);

    /// <inheritdoc />
    public async Task<MailDraftCompact?> GetDraftCompactAsync(string draftId, CancellationToken cancellationToken = default) {
        var draft = await _store.GetByIdAsync(draftId, cancellationToken).ConfigureAwait(false);
        return draft == null ? null : ToCompact(draft);
    }

    /// <inheritdoc />
    public async Task<OperationResult> SaveAsync(MailDraft draft, CancellationToken cancellationToken = default) {
        var validation = await ValidateAsync(draft, cancellationToken).ConfigureAwait(false);
        if (!validation.Succeeded) {
            return validation;
        }

        await _store.SaveAsync(draft, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success($"Draft '{draft.Id}' saved.");
    }

    /// <inheritdoc />
    public async Task<OperationResult> DeleteAsync(string draftId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(draftId)) {
            throw new ArgumentException("Draft id is required.", nameof(draftId));
        }

        var removed = await _store.RemoveAsync(draftId.Trim(), cancellationToken).ConfigureAwait(false);
        return removed
            ? OperationResult.Success($"Draft '{draftId.Trim()}' deleted.")
            : OperationResult.Failure("draft_not_found", $"Draft '{draftId.Trim()}' was not found.");
    }

    private async Task<OperationResult> ValidateAsync(MailDraft? draft, CancellationToken cancellationToken) {
        if (draft == null) {
            return OperationResult.Failure("draft_invalid", "Draft is required.");
        }
        if (string.IsNullOrWhiteSpace(draft.Id)) {
            return OperationResult.Failure("draft_invalid", "Draft id is required.");
        }
        if (string.IsNullOrWhiteSpace(draft.Name)) {
            return OperationResult.Failure("draft_invalid", "Draft name is required.");
        }
        if (draft.Message == null) {
            return OperationResult.Failure("draft_invalid", "Draft message is required.");
        }
        if (string.IsNullOrWhiteSpace(draft.Message.ProfileId)) {
            return OperationResult.Failure("draft_invalid", "Draft profile id is required.");
        }

        var profile = await _profileStore.GetByIdAsync(draft.Message.ProfileId, cancellationToken).ConfigureAwait(false);
        if (profile == null) {
            return OperationResult.Failure("draft_profile_not_found", $"Profile '{draft.Message.ProfileId}' was not found.");
        }

        return OperationResult.Success();
    }

    private static MailDraftCompact ToCompact(MailDraft draft) => new() {
        Id = draft.Id,
        Name = draft.Name,
        ProfileId = draft.Message.ProfileId,
        Subject = draft.Message.Subject,
        ToCount = draft.Message.To.Count,
        AttachmentCount = draft.Message.Attachments.Count,
        UpdatedAt = draft.UpdatedAt,
        Summary = $"{draft.Id} [{draft.Message.ProfileId}] {draft.Name}"
    };
}