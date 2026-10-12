using System.Management.Automation;

namespace Mailozaurr.PowerShell;

internal static class PowerShellFileSystemPath {
    // Resolve on the pipeline thread before awaiting; provider-internal paths are not necessarily files.
    public static string Resolve(SessionState sessionState, string path) {
        string resolved = sessionState.Path.GetUnresolvedProviderPathFromPSPath(path, out ProviderInfo provider, out _);
        if (provider.Name != "FileSystem") {
            throw new PSArgumentException("The destination must be a FileSystem path.");
        }
        return resolved;
    }
}
