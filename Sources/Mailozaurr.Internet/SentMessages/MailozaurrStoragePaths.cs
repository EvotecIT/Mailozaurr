namespace Mailozaurr;

/// <summary>Resolves per-user storage locations owned by the reusable Mailozaurr runtime.</summary>
public static class MailozaurrStoragePaths {
    private const string PendingDirectoryVariable = "MAILOZAURR_PENDING_DIRECTORY";

    /// <summary>Resolves the default per-user pending-message queue directory.</summary>
    public static string ResolvePendingMessagesDirectory() {
        string? configured = Environment.GetEnvironmentVariable(PendingDirectoryVariable);
        if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);

        string baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory)) {
            baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        if (string.IsNullOrWhiteSpace(baseDirectory)) {
            baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        if (string.IsNullOrWhiteSpace(baseDirectory)) baseDirectory = AppContext.BaseDirectory;
        return Path.Combine(baseDirectory, "Mailozaurr", "PendingMessages");
    }
}
