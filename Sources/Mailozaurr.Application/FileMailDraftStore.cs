using System.Text.Json;

namespace Mailozaurr.Application;

/// <summary>
/// Stores reusable drafts in a JSON document on disk.
/// </summary>
public sealed class FileMailDraftStore : IMailDraftStore {
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
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
        _filePath = Path.GetFullPath(filePath ?? throw new ArgumentNullException(nameof(filePath)));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MailDraft>> GetAllAsync(CancellationToken cancellationToken = default) {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            return document.Drafts
                .Select(CloneDraft)
                .OrderBy(draft => draft.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(draft => draft.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<MailDraft?> GetByIdAsync(string draftId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(draftId)) {
            throw new ArgumentException("Draft id is required.", nameof(draftId));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var draft = document.Drafts.FirstOrDefault(existing => string.Equals(existing.Id, draftId, StringComparison.OrdinalIgnoreCase));
            return draft == null ? null : CloneDraft(draft);
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(MailDraft draft, CancellationToken cancellationToken = default) {
        ValidateDraft(draft);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
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

            await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(string draftId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(draftId)) {
            throw new ArgumentException("Draft id is required.", nameof(draftId));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var removed = document.Drafts.RemoveAll(existing => string.Equals(existing.Id, draftId, StringComparison.OrdinalIgnoreCase)) > 0;
            if (!removed) {
                return false;
            }

            await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            return true;
        } finally {
            _gate.Release();
        }
    }

    private async Task<MailDraftStoreDocument> LoadDocumentAsync(CancellationToken cancellationToken) {
        if (!File.Exists(_filePath)) {
            return new MailDraftStoreDocument();
        }

        using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            var document = await JsonSerializer.DeserializeAsync(stream, ApplicationJsonContext.Default.MailDraftStoreDocument, cancellationToken).ConfigureAwait(false);
            return document ?? new MailDraftStoreDocument();
        }
    }

    private async Task SaveDocumentAsync(MailDraftStoreDocument document, CancellationToken cancellationToken) {
        var directory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrWhiteSpace(directory)) {
            throw new InvalidOperationException("Draft store path is invalid.");
        }

        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, Path.GetRandomFileName());
        try {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                await JsonSerializer.SerializeAsync(stream, document, ApplicationJsonContext.Default.MailDraftStoreDocument, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(_filePath)) {
                File.Delete(_filePath);
            }

            File.Move(tempPath, _filePath);
            tempPath = string.Empty;
        } finally {
            if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath)) {
                File.Delete(tempPath);
            }
        }
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
