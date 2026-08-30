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

    /// <summary>
    /// Keeps using a legacy default queue while that file exists. This lets old and new
    /// processes append to one queue during a rolling upgrade; an operator can move the file
    /// to the per-user target after all legacy processes have stopped. Repositories retain
    /// the alternate path and fail closed if it appears after construction.
    /// </summary>
    internal static string ResolveDefaultStorageFile(string targetPath, string legacyPath) {
        return ResolveDefaultStorageFiles(targetPath, legacyPath).Path;
    }

    internal static (string Path, string ConflictPath) ResolveDefaultStorageFiles(
        string targetPath,
        string legacyPath) {
        targetPath = Path.GetFullPath(targetPath);
        legacyPath = Path.GetFullPath(legacyPath);
        if (string.Equals(targetPath, legacyPath, StringComparison.OrdinalIgnoreCase)) {
            return (targetPath, string.Empty);
        }

        bool targetExists = File.Exists(targetPath);
        bool legacyExists = File.Exists(legacyPath);
        if (targetExists && legacyExists && IsRegularFileWithoutReparsePoint(legacyPath)) {
            throw new InvalidOperationException(
                "Both the per-user and legacy Mailozaurr queue files exist. Stop legacy processes and reconcile the files before continuing so queued messages are not hidden.");
        }
        if (!legacyExists) return (targetPath, legacyPath);
        return IsRegularFileWithoutReparsePoint(legacyPath)
            ? (legacyPath, targetPath)
            : (targetPath, legacyPath);
    }

    internal static void ThrowIfConflictingQueueAppeared(string path, string conflictPath) {
        if (string.IsNullOrWhiteSpace(conflictPath) || !File.Exists(conflictPath)) return;
        throw new InvalidOperationException(
            $"Mailozaurr queue '{conflictPath}' appeared after '{path}' was selected. Stop legacy processes and reconcile the files before continuing so queued messages are not hidden.");
    }

    private static bool IsRegularFileWithoutReparsePoint(string path) {
        try {
            FileAttributes attributes = File.GetAttributes(path);
            return (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0;
        } catch (IOException) {
            return false;
        } catch (UnauthorizedAccessException) {
            return false;
        }
    }

}
