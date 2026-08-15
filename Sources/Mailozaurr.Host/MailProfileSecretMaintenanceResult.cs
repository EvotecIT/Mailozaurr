namespace Mailozaurr.Hosting;

/// <summary>
/// Reports profile-scoped secret sets that no longer have a profile owner and legacy keys that cannot be classified safely.
/// </summary>
public sealed class MailProfileSecretMaintenanceResult : OperationResult {
    /// <summary>Profile identifiers whose structured secret sets had no matching saved profile.</summary>
    public List<string> OrphanedProfileIds { get; } = new();

    /// <summary>Orphaned profile identifiers whose structured secret sets were removed.</summary>
    public List<string> RemovedProfileIds { get; } = new();

    /// <summary>
    /// Ambiguous legacy flat keys that were retained because their profile and secret-name boundary cannot be proven.
    /// </summary>
    public List<string> UnresolvedLegacyKeys { get; } = new();

    /// <summary>Whether structured orphan secret sets were found.</summary>
    public bool HasOrphanedSecrets => OrphanedProfileIds.Count > 0;

    /// <summary>Whether ambiguous legacy keys still require explicit operator review.</summary>
    public bool HasUnresolvedLegacySecrets => UnresolvedLegacyKeys.Count > 0;
}