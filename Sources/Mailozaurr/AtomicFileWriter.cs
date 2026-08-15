namespace Mailozaurr;

/// <summary>
/// Writes file content through a sibling temporary file and atomically replaces existing targets.
/// </summary>
internal static class AtomicFileWriter {
    /// <summary>
    /// Writes <paramref name="targetPath"/> without deleting the existing file before replacement.
    /// </summary>
    public static async Task WriteAsync(
        string targetPath,
        string invalidPathMessage,
        Func<Stream, CancellationToken, Task> writeAsync,
        CancellationToken cancellationToken) {
        if (writeAsync == null) {
            throw new ArgumentNullException(nameof(writeAsync));
        }

        var directory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(directory)) {
            throw new InvalidOperationException(invalidPathMessage);
        }

        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, Path.GetRandomFileName());
        var backupPath = Path.Combine(directory, Path.GetRandomFileName());
        try {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                await writeAsync(stream, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(targetPath)) {
                File.Replace(tempPath, targetPath, backupPath);
                tempPath = string.Empty;
                DeleteIfExists(ref backupPath);
                return;
            }

            File.Move(tempPath, targetPath);
            tempPath = string.Empty;
        } finally {
            DeleteIfExists(ref tempPath);
            DeleteIfExists(ref backupPath);
        }
    }

    private static void DeleteIfExists(ref string path) {
        if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
            File.Delete(path);
        }

        path = string.Empty;
    }
}
