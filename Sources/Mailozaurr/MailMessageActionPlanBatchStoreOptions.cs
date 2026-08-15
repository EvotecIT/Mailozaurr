namespace Mailozaurr;

/// <summary>
/// Configures where persisted reusable message action plan batches are stored.
/// </summary>
public sealed class MailMessageActionPlanBatchStoreOptions {
    /// <summary>Directory containing the batch store file.</summary>
    public string? DirectoryPath { get; set; }

    /// <summary>File name used to persist batches.</summary>
    public string FileName { get; set; } = "action-plan-batches.json";

    /// <summary>
    /// Resolves the file path that should be used by the batch store.
    /// </summary>
    public string GetFilePath() {
        var directory = string.IsNullOrWhiteSpace(DirectoryPath)
            ? MailApplicationPaths.ResolveActionPlanBatchesDirectory()
            : Path.GetFullPath(DirectoryPath);
        return Path.Combine(directory, FileName);
    }
}