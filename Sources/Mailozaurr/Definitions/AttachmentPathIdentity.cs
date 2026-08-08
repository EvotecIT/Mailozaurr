namespace Mailozaurr.Definitions;

using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>
/// Provides platform-correct canonical identity for file-backed attachments.
/// </summary>
internal static class AttachmentPathIdentity {
    internal static StringComparer Comparer { get; } =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    internal static HashSet<string> CreateSet() => new(Comparer);

    internal static bool Add(ISet<string> paths, string path) => paths.Add(Path.GetFullPath(path));
}
