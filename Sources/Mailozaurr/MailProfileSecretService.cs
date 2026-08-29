namespace Mailozaurr;

/// <summary>
/// Default implementation of profile secret lifecycle operations.
/// </summary>
public sealed class MailProfileSecretService : IMailProfileSecretService {
    private readonly IMailProfileStore _profileStore;
    private readonly IMailSecretStore _secretStore;

    /// <summary>
    /// Creates a new profile secret service.
    /// </summary>
    public MailProfileSecretService(IMailProfileStore profileStore, IMailSecretStore secretStore) {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    }

    /// <inheritdoc />
    public Task<OperationResult> SetSecretAsync(string profileId, string secretName, string secretValue, CancellationToken cancellationToken = default) =>
        SetSecretAsync(profileId, secretName, secretValue, null, false, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult> SetSecretAsync(
        string profileId,
        string secretName,
        string? secretValue,
        string? secretReference,
        CancellationToken cancellationToken = default) =>
        SetSecretAsync(profileId, secretName, secretValue, secretReference, false, cancellationToken);

    /// <inheritdoc />
    public async Task<OperationResult> SetSecretAsync(
        string profileId,
        string secretName,
        string? secretValue,
        string? secretReference,
        bool allowCrossProfileReference,
        CancellationToken cancellationToken = default) {
        var validationResult = await ValidateAsync(profileId, secretName, cancellationToken).ConfigureAwait(false);
        if (!validationResult.Succeeded) {
            return validationResult;
        }

        string? resolvedSecret;
        try {
            resolvedSecret = await MailSecretReferenceResolver.ResolveAsync(
                _secretStore,
                profileId,
                secretName,
                secretValue,
                secretReference,
                allowCrossProfileReference,
                cancellationToken).ConfigureAwait(false);
        } catch (InvalidOperationException ex) {
            return OperationResult.Failure("secret_reference_invalid", ex.Message);
        }

        if (string.IsNullOrWhiteSpace(resolvedSecret)) {
            return OperationResult.Failure("secret_value_required", "Secret value is required.");
        }

        await _secretStore.SetSecretAsync(profileId, secretName, resolvedSecret!, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success("Secret saved.");
    }

    /// <inheritdoc />
    public async Task<OperationResult> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
        var validationResult = await ValidateAsync(profileId, secretName, cancellationToken).ConfigureAwait(false);
        if (!validationResult.Succeeded) {
            return validationResult;
        }

        var removed = await _secretStore.RemoveSecretAsync(profileId, secretName, cancellationToken).ConfigureAwait(false);
        return removed
            ? OperationResult.Success("Secret removed.")
            : OperationResult.Failure("secret_not_found", "Secret was not found.");
    }

    private async Task<OperationResult> ValidateAsync(string profileId, string secretName, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            return OperationResult.Failure("profile_required", "Profile id is required.");
        }
        if (string.IsNullOrWhiteSpace(secretName)) {
            return OperationResult.Failure("secret_name_required", "Secret name is required.");
        }

        var profile = await _profileStore.GetByIdAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (profile == null) {
            return OperationResult.Failure("profile_not_found", "Profile was not found.");
        }

        return OperationResult.Success();
    }
}
