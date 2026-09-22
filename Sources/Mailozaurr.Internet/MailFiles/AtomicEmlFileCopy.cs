namespace Mailozaurr;

/// <summary>Commits one validated provider source without exposing partial EML files.</summary>
internal static class AtomicEmlFileCopy {
    internal static async Task WriteAsync(string destinationPath,
        Func<Stream, CancellationToken, Task> writeContent,
        bool overwrite, CancellationToken cancellationToken) {
        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("The EML destination has no parent directory.");
        if (!Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
            UnixFilePermissions.RestrictDirectory(directory);
        }
        // Keep the staging name short: the final EML name can already be near
        // the .NET Framework Windows path limit.
        var temporaryPath = Path.Combine(directory,
            ".eml-" + Guid.NewGuid().ToString("N") + ".tmp");
        try {
            using (var temporary = UnixFilePermissions.OpenRestrictedFile(temporaryPath,
                FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                await writeContent(temporary, cancellationToken).ConfigureAwait(false);
                await temporary.FlushAsync(cancellationToken).ConfigureAwait(false);
                temporary.Flush(true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (!overwrite) {
                File.Move(temporaryPath, fullPath);
                return;
            }
            while (true) {
                if (!File.Exists(fullPath)) {
                    try {
                        File.Move(temporaryPath, fullPath);
                        return;
                    } catch (IOException) when (File.Exists(fullPath)) {
                        // A concurrent writer created the target after the check.
                    }
                }
                RejectLinkOrDirectory(fullPath);
                try {
                    File.Replace(temporaryPath, fullPath, null);
                    return;
                } catch (IOException) when (!File.Exists(fullPath)) {
                    // A concurrent writer removed the target; retry the create path.
                }
            }
        } finally {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static void RejectLinkOrDirectory(string path) {
        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) {
            throw new IOException("The EML destination must be a regular file.");
        }
    }
}
