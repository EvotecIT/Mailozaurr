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
    /// Moves a legacy default queue file out of the shared temporary directory when possible.
    /// If another process is still using the legacy file, that path remains the compatibility
    /// fallback so queued messages do not become invisible during an upgrade.
    /// </summary>
    internal static string ResolveDefaultStorageFile(string targetPath, string legacyPath) {
        targetPath = Path.GetFullPath(targetPath);
        legacyPath = Path.GetFullPath(legacyPath);
        if (string.Equals(targetPath, legacyPath, StringComparison.OrdinalIgnoreCase)
            || File.Exists(targetPath)
            || !File.Exists(legacyPath)) {
            return targetPath;
        }

        if (!IsRegularFileWithoutReparsePoint(legacyPath)) return targetPath;

        string? directory = Path.GetDirectoryName(targetPath);
        bool createdTarget = false;
        try {
            if (!string.IsNullOrWhiteSpace(directory)) {
                bool existed = Directory.Exists(directory);
                Directory.CreateDirectory(directory);
                if (!existed) UnixFilePermissions.RestrictDirectory(directory);
            }

            using (var source = new FileStream(legacyPath, FileMode.Open, FileAccess.Read, FileShare.None))
            using (var destination = UnixFilePermissions.OpenRestrictedFile(
                targetPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None)) {
                createdTarget = true;
                source.CopyTo(destination);
                destination.Flush();
            }

            try {
                File.Delete(legacyPath);
            } catch (IOException) {
                // The migrated copy is complete. Leaving the legacy copy is safer than
                // deleting data another process may still expect.
            } catch (UnauthorizedAccessException) {
            }
            return targetPath;
        } catch (IOException) {
            if (createdTarget) DeleteIncompleteTarget(targetPath);
            if (File.Exists(targetPath)) return targetPath;
            return legacyPath;
        } catch (UnauthorizedAccessException) {
            if (createdTarget) DeleteIncompleteTarget(targetPath);
            if (File.Exists(targetPath)) return targetPath;
            return legacyPath;
        }
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

    private static void DeleteIncompleteTarget(string path) {
        try {
            if (File.Exists(path)) File.Delete(path);
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }
}
