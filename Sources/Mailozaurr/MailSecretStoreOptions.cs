namespace Mailozaurr;

/// <summary>
/// Configures where protected secrets are stored.
/// </summary>
public sealed class MailSecretStoreOptions {
    /// <summary>Directory containing the secret store file.</summary>
    public string? DirectoryPath { get; set; }

    /// <summary>File name used to persist secrets.</summary>
    public string FileName { get; set; } = "profile-secrets.json";

    /// <summary>
    /// Resolves the file path that should be used by the secret store.
    /// </summary>
    public string GetFilePath() {
        var directory = string.IsNullOrWhiteSpace(DirectoryPath)
            ? MailApplicationPaths.ResolveSecretsDirectory()
            : Path.GetFullPath(DirectoryPath);
        return Path.Combine(directory, FileName);
    }
}