namespace Mailozaurr.Application;

/// <summary>
/// Stores reusable drafts in a JSON document on disk.
/// </summary>
public sealed class FileMailDraftStore : IMailDraftStore {
    private readonly JsonFileDocumentStore<MailDraftStoreDocument> _store;
    /// <summary>
    /// Creates a new store using the provided options.
    /// </summary>
    public FileMailDraftStore(MailDraftStoreOptions? options = null)
        : this((options ?? new MailDraftStoreOptions()).GetFilePath()) {
    }

    /// <summary>
    /// Creates a new store using the specified file path.
    /// </summary>
    public FileMailDraftStore(string filePath) {
        _store = new JsonFileDocumentStore<MailDraftStoreDocument>(
            filePath,
            "Draft store path is invalid.",
            ApplicationJsonContext.Default.MailDraftStoreDocument,
            static () => new MailDraftStoreDocument());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MailDraft>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _store.ReadAsync<IReadOnlyList<MailDraft>>(document => document.Drafts
                .Select(CloneDraft)
                .OrderBy(draft => draft.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(draft => draft.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray(), cancellationToken);

    /// <inheritdoc />
    public Task<MailDraft?> GetByIdAsync(string draftId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(draftId)) {
            throw new ArgumentException("Draft id is required.", nameof(draftId));
        }

        return _store.ReadAsync(document => {
            MailDraft? draft = document.Drafts.FirstOrDefault(existing =>
                string.Equals(existing.Id, draftId, StringComparison.OrdinalIgnoreCase));
            return draft == null ? null : CloneDraft(draft);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveAsync(MailDraft draft, CancellationToken cancellationToken = default) {
        ValidateDraft(draft);

        await _store.UpdateAsync(document => {
            var index = document.Drafts.FindIndex(existing => string.Equals(existing.Id, draft.Id, StringComparison.OrdinalIgnoreCase));
            var draftToStore = CloneDraft(draft);
            if (draftToStore.CreatedAt == default) {
                draftToStore.CreatedAt = DateTimeOffset.UtcNow;
            }
            draftToStore.UpdatedAt = DateTimeOffset.UtcNow;

            if (index >= 0) {
                draftToStore.CreatedAt = document.Drafts[index].CreatedAt == default
                    ? draftToStore.CreatedAt
                    : document.Drafts[index].CreatedAt;
                document.Drafts[index] = draftToStore;
            } else {
                document.Drafts.Add(draftToStore);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(string draftId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(draftId)) {
            throw new ArgumentException("Draft id is required.", nameof(draftId));
        }

        return _store.RemoveAsync(document => document.Drafts.RemoveAll(existing =>
            string.Equals(existing.Id, draftId, StringComparison.OrdinalIgnoreCase)) > 0, cancellationToken);
    }

    private static void ValidateDraft(MailDraft? draft) {
        if (draft == null) {
            throw new ArgumentNullException(nameof(draft));
        }
        if (string.IsNullOrWhiteSpace(draft.Id)) {
            throw new InvalidOperationException("Draft id is required.");
        }
        if (string.IsNullOrWhiteSpace(draft.Name)) {
            throw new InvalidOperationException("Draft name is required.");
        }
        if (draft.Message == null) {
            throw new InvalidOperationException("Draft message is required.");
        }
        if (string.IsNullOrWhiteSpace(draft.Message.ProfileId)) {
            throw new InvalidOperationException("Draft message profile id is required.");
        }
    }

    private static MailDraft CloneDraft(MailDraft draft) => new() {
        Id = draft.Id,
        Name = draft.Name,
        CreatedAt = draft.CreatedAt,
        UpdatedAt = draft.UpdatedAt,
        Message = CloneMessage(draft.Message)
    };

    private static DraftMessage CloneMessage(DraftMessage message) => new() {
        ProfileId = message.ProfileId,
        From = message.From == null ? null : CloneRecipient(message.From),
        To = message.To.Select(CloneRecipient).ToList(),
        Cc = message.Cc.Select(CloneRecipient).ToList(),
        Bcc = message.Bcc.Select(CloneRecipient).ToList(),
        ReplyTo = message.ReplyTo.Select(CloneRecipient).ToList(),
        Subject = message.Subject,
        TextBody = message.TextBody,
        HtmlBody = message.HtmlBody,
        Headers = new Dictionary<string, string>(message.Headers, StringComparer.OrdinalIgnoreCase),
        Attachments = message.Attachments.Select(CloneAttachment).ToList()
    };

    private static MessageRecipient CloneRecipient(MessageRecipient recipient) => new() {
        Name = recipient.Name,
        Address = recipient.Address
    };

    private static DraftAttachment CloneAttachment(DraftAttachment attachment) => new() {
        Path = attachment.Path,
        FileName = attachment.FileName,
        ContentType = attachment.ContentType,
        IsInline = attachment.IsInline,
        ContentId = attachment.ContentId
    };

}
