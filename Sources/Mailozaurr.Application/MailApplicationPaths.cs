namespace Mailozaurr.Application;

/// <summary>
/// Resolves default storage paths used by application-layer services.
/// </summary>
public static class MailApplicationPaths {
    private const string RootDirectoryName = "Mailozaurr";
    private const string ProfilesSubDirectoryName = "Profiles";
    private const string SecretsSubDirectoryName = "Secrets";
    private const string DraftsSubDirectoryName = "Drafts";
    private const string ProfilesOverrideDirectoryVariable = "MAILOZAURR_PROFILE_DIRECTORY";
    private const string SecretsOverrideDirectoryVariable = "MAILOZAURR_SECRET_DIRECTORY";
    private const string DraftsOverrideDirectoryVariable = "MAILOZAURR_DRAFT_DIRECTORY";

    /// <summary>
    /// Resolves the default directory used to store profile configuration.
    /// </summary>
    public static string ResolveProfilesDirectory() => ResolveDirectory(
        ProfilesOverrideDirectoryVariable,
        ProfilesSubDirectoryName);

    /// <summary>
    /// Resolves the default directory used to store protected secrets.
    /// </summary>
    public static string ResolveSecretsDirectory() => ResolveDirectory(
        SecretsOverrideDirectoryVariable,
        SecretsSubDirectoryName);

    /// <summary>
    /// Resolves the default directory used to store reusable drafts.
    /// </summary>
    public static string ResolveDraftsDirectory() => ResolveDirectory(
        DraftsOverrideDirectoryVariable,
        DraftsSubDirectoryName);

    private static string ResolveDirectory(string overrideVariableName, string subDirectoryName) {
        var overrideDirectory = Environment.GetEnvironmentVariable(overrideVariableName);
        if (!string.IsNullOrWhiteSpace(overrideDirectory)) {
            return Path.GetFullPath(overrideDirectory);
        }

        var baseDirectory = GetBaseDirectory();
        return Path.Combine(baseDirectory, RootDirectoryName, subDirectoryName);
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
