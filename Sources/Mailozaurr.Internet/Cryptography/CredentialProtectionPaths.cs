using System;
using System.IO;

namespace Mailozaurr;

internal static class CredentialProtectionPaths {
    private const string RootDirectoryName = "Mailozaurr";
    private const string SubDirectoryName = "DataProtection";
    private const string OverrideDirectoryVariable = "MAILOZAURR_KEY_DIRECTORY";

    public static string ResolveKeyDirectory() {
        var overrideDirectory = Environment.GetEnvironmentVariable(OverrideDirectoryVariable);
        if (!string.IsNullOrWhiteSpace(overrideDirectory)) {
            return Path.GetFullPath(overrideDirectory);
        }

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