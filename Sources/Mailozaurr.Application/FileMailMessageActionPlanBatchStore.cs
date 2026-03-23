using System.Text.Json;

namespace Mailozaurr.Application;

/// <summary>
/// Stores reusable message action plan batches in a JSON document on disk.
/// </summary>
public sealed class FileMailMessageActionPlanBatchStore : IMailMessageActionPlanBatchStore {
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions SerializerOptions = new() {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// Creates a new store using the provided options.
    /// </summary>
    public FileMailMessageActionPlanBatchStore(MailMessageActionPlanBatchStoreOptions? options = null)
        : this((options ?? new MailMessageActionPlanBatchStoreOptions()).GetFilePath()) {
    }

    /// <summary>
    /// Creates a new store using the specified file path.
    /// </summary>
    public FileMailMessageActionPlanBatchStore(string filePath) {
        _filePath = Path.GetFullPath(filePath ?? throw new ArgumentNullException(nameof(filePath)));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MailMessageActionPlanBatch>> GetAllAsync(CancellationToken cancellationToken = default) {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            return document.Batches
                .Select(CloneBatch)
                .OrderBy(batch => batch.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(batch => batch.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<MailMessageActionPlanBatch?> GetByIdAsync(string batchId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(batchId)) {
            throw new ArgumentException("Batch id is required.", nameof(batchId));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var batch = document.Batches.FirstOrDefault(existing => string.Equals(existing.Id, batchId, StringComparison.OrdinalIgnoreCase));
            return batch == null ? null : CloneBatch(batch);
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(MailMessageActionPlanBatch batch, CancellationToken cancellationToken = default) {
        ValidateBatch(batch);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var index = document.Batches.FindIndex(existing => string.Equals(existing.Id, batch.Id, StringComparison.OrdinalIgnoreCase));
            var batchToStore = CloneBatch(batch);
            if (batchToStore.CreatedAt == default) {
                batchToStore.CreatedAt = DateTimeOffset.UtcNow;
            }
            batchToStore.UpdatedAt = DateTimeOffset.UtcNow;

            if (index >= 0) {
                batchToStore.CreatedAt = document.Batches[index].CreatedAt == default
                    ? batchToStore.CreatedAt
                    : document.Batches[index].CreatedAt;
                document.Batches[index] = batchToStore;
            } else {
                document.Batches.Add(batchToStore);
            }

            await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        } finally {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(string batchId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(batchId)) {
            throw new ArgumentException("Batch id is required.", nameof(batchId));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var removed = document.Batches.RemoveAll(existing => string.Equals(existing.Id, batchId, StringComparison.OrdinalIgnoreCase)) > 0;
            if (!removed) {
                return false;
            }

            await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            return true;
        } finally {
            _gate.Release();
        }
    }

    private async Task<MailMessageActionPlanBatchStoreDocument> LoadDocumentAsync(CancellationToken cancellationToken) {
        if (!File.Exists(_filePath)) {
            return new MailMessageActionPlanBatchStoreDocument();
        }

        using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            var document = await JsonSerializer.DeserializeAsync<MailMessageActionPlanBatchStoreDocument>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
            return document ?? new MailMessageActionPlanBatchStoreDocument();
        }
    }

    private async Task SaveDocumentAsync(MailMessageActionPlanBatchStoreDocument document, CancellationToken cancellationToken) {
        var directory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrWhiteSpace(directory)) {
            throw new InvalidOperationException("Action plan batch store path is invalid.");
        }

        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, Path.GetRandomFileName());
        try {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken).ConfigureAwait(false);
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

    private static void ValidateBatch(MailMessageActionPlanBatch? batch) {
        if (batch == null) {
            throw new ArgumentNullException(nameof(batch));
        }
        if (string.IsNullOrWhiteSpace(batch.Id)) {
            throw new InvalidOperationException("Action plan batch id is required.");
        }
        if (string.IsNullOrWhiteSpace(batch.Name)) {
            throw new InvalidOperationException("Action plan batch name is required.");
        }
        if (batch.Plans == null) {
            throw new InvalidOperationException("Action plan batch plans are required.");
        }
    }

    private static MailMessageActionPlanBatch CloneBatch(MailMessageActionPlanBatch batch) => new() {
        Id = batch.Id,
        Name = batch.Name,
        Description = batch.Description,
        CreatedAt = batch.CreatedAt,
        UpdatedAt = batch.UpdatedAt,
        Plans = batch.Plans.Select(ClonePlan).ToList()
    };

    private static MessageActionExecutionPlan ClonePlan(MessageActionExecutionPlan plan) => new() {
        Succeeded = plan.Succeeded,
        Code = plan.Code,
        Message = plan.Message,
        Name = plan.Name,
        Summary = plan.Summary,
        Action = plan.Action,
        ExecutionKind = plan.ExecutionKind,
        ProfileId = plan.ProfileId,
        MailboxId = plan.MailboxId,
        FolderId = plan.FolderId,
        RequestedCount = plan.RequestedCount,
        UniqueMessageCount = plan.UniqueMessageCount,
        MessageIds = plan.MessageIds.ToList(),
        RequestedDestinationFolderId = plan.RequestedDestinationFolderId,
        Destination = plan.Destination == null
            ? null
            : new MailFolderTargetResolution {
                ProfileId = plan.Destination.ProfileId,
                MailboxId = plan.Destination.MailboxId,
                RequestedValue = plan.Destination.RequestedValue,
                IsAlias = plan.Destination.IsAlias,
                Alias = plan.Destination.Alias,
                IsSupported = plan.Destination.IsSupported,
                IsResolved = plan.Destination.IsResolved,
                EffectiveFolderId = plan.Destination.EffectiveFolderId,
                FolderDisplayName = plan.Destination.FolderDisplayName,
                FolderPath = plan.Destination.FolderPath,
                Summary = plan.Destination.Summary
            },
        DesiredState = plan.DesiredState,
        ConfirmationToken = plan.ConfirmationToken,
        ConfirmationProvided = plan.ConfirmationProvided,
        ConfirmationValidated = plan.ConfirmationValidated,
        Warnings = plan.Warnings.ToList()
    };

    private sealed class MailMessageActionPlanBatchStoreDocument {
        public int Version { get; set; } = 1;

        public List<MailMessageActionPlanBatch> Batches { get; set; } = new();
    }
}
