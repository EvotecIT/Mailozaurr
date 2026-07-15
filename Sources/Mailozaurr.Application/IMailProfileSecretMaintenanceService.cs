namespace Mailozaurr.Application;

/// <summary>
/// Inspects and cleans profile-scoped secrets whose owning profiles no longer exist.
/// </summary>
public interface IMailProfileSecretMaintenanceService {
    /// <summary>Reports profile-scoped secrets whose owning profiles no longer exist.</summary>
    Task<MailProfileSecretMaintenanceResult> InspectOrphanedSecretsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes structured orphan secret sets while retaining ambiguous legacy keys for explicit review.
    /// </summary>
    Task<MailProfileSecretMaintenanceResult> RemoveOrphanedSecretsAsync(
        CancellationToken cancellationToken = default);
}