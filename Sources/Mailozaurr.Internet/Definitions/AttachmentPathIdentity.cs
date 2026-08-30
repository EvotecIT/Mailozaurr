namespace Mailozaurr.Definitions;

using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>
/// Provides platform-correct canonical identity for file-backed attachments.
/// </summary>
internal static class AttachmentPathIdentity {
    internal static StringComparer Comparer { get; } =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    internal static HashSet<string> CreateSet() => new(Comparer);

    internal static bool Add(ISet<string> paths, string path) {
        return paths.Add(Normalize(path));
    }

    internal static string Normalize(string path) {
        var fullPath = Path.GetFullPath(path);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            fullPath = ResolveExistingPathCasing(fullPath);
        }
        return fullPath;
    }

    private static string ResolveExistingPathCasing(string fullPath) {
        try {
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root)) {
                return fullPath;
            }

            var current = root;
            var remainder = fullPath.Substring(root.Length);
            var segments = remainder.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments) {
                string? caseInsensitiveMatch = null;
                foreach (var entry in Directory.EnumerateFileSystemEntries(current)) {
                    var entryName = Path.GetFileName(entry);
                    if (string.Equals(entryName, segment, System.StringComparison.Ordinal)) {
                        caseInsensitiveMatch = entry;
                        break;
                    }
                    if (caseInsensitiveMatch == null && string.Equals(entryName, segment, System.StringComparison.OrdinalIgnoreCase)) {
                        caseInsensitiveMatch = entry;
                    }
                }
                current = caseInsensitiveMatch ?? Path.Combine(current, segment);
            }
            return current;
        } catch (IOException) {
            return fullPath;
        } catch (UnauthorizedAccessException) {
            return fullPath;
        }
    }
}
