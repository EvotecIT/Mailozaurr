namespace Mailozaurr.Application;

/// <summary>
/// Configures where mail profiles are stored.
/// </summary>
public sealed class MailProfileStoreOptions {
    /// <summary>Directory containing the profile store file.</summary>
    public string? DirectoryPath { get; set; }

    /// <summary>File name used to persist profiles.</summary>
    public string FileName { get; set; } = "profiles.json";

    /// <summary>
    /// Resolves the file path that should be used by the profile store.
    /// </summary>
    public string GetFilePath() {
        var directory = string.IsNullOrWhiteSpace(DirectoryPath)
            ? MailApplicationPaths.ResolveProfilesDirectory()
            : Path.GetFullPath(DirectoryPath);
        return Path.Combine(directory, FileName);
    }
}