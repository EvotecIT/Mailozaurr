namespace Mailozaurr.Application;

/// <summary>Captures and restores secrets for multi-store rollback paths.</summary>
internal sealed class MailSecretRollbackSnapshot {
    private readonly IMailProfileSecretSnapshot? _storeSnapshot;
    private readonly IReadOnlyDictionary<string, string?>? _plainTextSecrets;

    private MailSecretRollbackSnapshot(
        IMailProfileSecretSnapshot? storeSnapshot,
        IReadOnlyDictionary<string, string?>? plainTextSecrets) {
        _storeSnapshot = storeSnapshot;
        _plainTextSecrets = plainTextSecrets;
    }

    internal static async Task<MailSecretRollbackSnapshot> CaptureAsync(
        IMailSecretStore secretStore,
        string profileId,
        IReadOnlyCollection<string> fallbackSecretNames,
        IReadOnlyCollection<string>? requiredSecretNames,
        CancellationToken cancellationToken) {
        if (secretStore is IMailProfileSecretSnapshotStore snapshotStore) {
            IMailProfileSecretSnapshot snapshot = await snapshotStore.CaptureProfileSecretsAsync(
                profileId,
                cancellationToken).ConfigureAwait(false);
            return new MailSecretRollbackSnapshot(snapshot, null);
        }

        var secrets = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (secretStore is IMailProfileSecretCleanup cleanup) {
            IReadOnlyDictionary<string, string> existingSecrets = await cleanup.GetProfileSecretsAsync(
                profileId,
                cancellationToken).ConfigureAwait(false);
            foreach (KeyValuePair<string, string> secret in existingSecrets) {
                secrets[secret.Key] = secret.Value;
            }
        } else {
            foreach (string secretName in fallbackSecretNames) {
                string? value = await secretStore.GetSecretAsync(
                    profileId,
                    secretName,
                    cancellationToken).ConfigureAwait(false);
                if (value != null) {
                    secrets[secretName] = value;
                }
            }
        }

        if (requiredSecretNames != null) {
            foreach (string secretName in requiredSecretNames) {
                if (!secrets.ContainsKey(secretName)) {
                    secrets[secretName] = null;
                }
            }
        }

        return new MailSecretRollbackSnapshot(null, secrets);
    }

    internal async Task RestoreAsync(
        IMailSecretStore secretStore,
        string profileId,
        CancellationToken cancellationToken) {
        if (_storeSnapshot != null) {
            if (secretStore is not IMailProfileSecretSnapshotStore snapshotStore) {
                throw new InvalidOperationException("The secret store no longer supports its captured snapshot.");
            }
            await snapshotStore.RestoreProfileSecretsAsync(
                profileId,
                _storeSnapshot,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (KeyValuePair<string, string?> secret in _plainTextSecrets!) {
            if (secret.Value == null) {
                await secretStore.RemoveSecretAsync(
                    profileId,
                    secret.Key,
                    cancellationToken).ConfigureAwait(false);
            } else {
                await secretStore.SetSecretAsync(
                    profileId,
                    secret.Key,
                    secret.Value,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
