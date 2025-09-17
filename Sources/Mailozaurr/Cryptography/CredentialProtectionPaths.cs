using System;
using System.IO;

namespace Mailozaurr;

internal static class CredentialProtectionPaths {
    private const string RootDirectoryName = "Mailozaurr";
    private const string SubDirectoryName = "DataProtection";

    public static string ResolveKeyDirectory() {
        var baseDirectory = GetBaseDirectory();
        return Path.Combine(baseDirectory, RootDirectoryName, SubDirectoryName);
    }

    private static string GetBaseDirectory() {
        var candidates = new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        foreach (var candidate in candidates) {
            if (!string.IsNullOrWhiteSpace(candidate)) {
                return candidate;
            }
        }

        return AppContext.BaseDirectory;
    }
}
