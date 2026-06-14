namespace Mailozaurr.Application;

/// <summary>
/// Configures where persisted drafts are stored.
/// </summary>
public sealed class MailDraftStoreOptions {
    /// <summary>Directory containing the draft store file.</summary>
    public string? DirectoryPath { get; set; }

    /// <summary>File name used to persist drafts.</summary>
    public string FileName { get; set; } = "drafts.json";

    /// <summary>
    /// Resolves the file path that should be used by the draft store.
    /// </summary>
    public string GetFilePath() {
        var directory = string.IsNullOrWhiteSpace(DirectoryPath)
            ? MailApplicationPaths.ResolveDraftsDirectory()
            : Path.GetFullPath(DirectoryPath);
        return Path.Combine(directory, FileName);
    }
}