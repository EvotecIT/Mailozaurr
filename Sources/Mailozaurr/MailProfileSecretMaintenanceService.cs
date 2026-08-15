namespace Mailozaurr;

/// <summary>
/// Coordinates orphan-secret inspection and cleanup against the current profile inventory.
/// </summary>
public sealed class MailProfileSecretMaintenanceService : IMailProfileSecretMaintenanceService {
    private readonly IMailProfileStore _profileStore;
    private readonly IMailSecretStore _secretStore;

    /// <summary>Creates a maintenance service over the provided profile and secret stores.</summary>
    public MailProfileSecretMaintenanceService(
        IMailProfileStore profileStore,
        IMailSecretStore secretStore) {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    }

    /// <inheritdoc />
    public Task<MailProfileSecretMaintenanceResult> InspectOrphanedSecretsAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(remove: false, cancellationToken);

    /// <inheritdoc />
    public Task<MailProfileSecretMaintenanceResult> RemoveOrphanedSecretsAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(remove: true, cancellationToken);

    private async Task<MailProfileSecretMaintenanceResult> ExecuteAsync(
        bool remove,
        CancellationToken cancellationToken) {
        if (_secretStore is not IMailProfileSecretMaintenanceStore maintenanceStore) {
            return new MailProfileSecretMaintenanceResult {
                Succeeded = false,
                Code = "secret_maintenance_unavailable",
                Message = "The configured secret store does not support orphan-secret maintenance."
            };
        }

        MailProfileSecretMaintenanceResult result;
        if (remove) {
            if (_profileStore is not IMailProfileMaintenanceCoordinator profileCoordinator) {
                return new MailProfileSecretMaintenanceResult {
                    Succeeded = false,
                    Code = "secret_cleanup_coordination_unavailable",
                    Message = "The configured profile store cannot hold a stable inventory during orphan-secret cleanup. " +
                              "Inject a custom profile-secret maintenance service for this store."
                };
            }
            result = await profileCoordinator.ExecuteWithStableProfileIdsAsync(
                (knownProfileIds, operationCancellationToken) => maintenanceStore.RemoveOrphanedSecretsAsync(
                    knownProfileIds,
                    operationCancellationToken),
                cancellationToken).ConfigureAwait(false);
        } else {
            string[] knownProfileIds = await GetKnownProfileIdsAsync(cancellationToken).ConfigureAwait(false);
            result = await maintenanceStore.InspectOrphanedSecretsAsync(knownProfileIds, cancellationToken)
                .ConfigureAwait(false);
        }

        if (result.Succeeded) {
            result.Message = remove
                ? CreateRemovalMessage(result)
                : CreateInspectionMessage(result);
        }
        return result;
    }

    private async Task<string[]> GetKnownProfileIdsAsync(CancellationToken cancellationToken) {
        IReadOnlyList<MailProfile> profiles = await _profileStore.GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return profiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Id))
            .Select(profile => profile.Id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string CreateInspectionMessage(MailProfileSecretMaintenanceResult result) {
        if (!result.HasOrphanedSecrets && !result.HasUnresolvedLegacySecrets) {
            return "No orphaned profile secrets were found.";
        }
        return $"Found {result.OrphanedProfileIds.Count} structured orphan secret set(s) and " +
               $"{result.UnresolvedLegacyKeys.Count} ambiguous legacy key(s).";
    }

    private static string CreateRemovalMessage(MailProfileSecretMaintenanceResult result) =>
        $"Removed {result.RemovedProfileIds.Count} structured orphan secret set(s); " +
        $"retained {result.UnresolvedLegacyKeys.Count} ambiguous legacy key(s).";
}