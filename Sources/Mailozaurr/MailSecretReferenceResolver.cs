namespace Mailozaurr;

internal static class MailSecretReferenceResolver {
    public static async Task<string?> ResolveAsync(
        IMailSecretStore secretStore,
        string targetProfileId,
        string targetSecretName,
        string? inlineValue,
        string? secretReference,
        bool allowCrossProfileReference = false,
        CancellationToken cancellationToken = default) {
        if (secretStore == null) {
            throw new ArgumentNullException(nameof(secretStore));
        }

        var hasInline = !string.IsNullOrWhiteSpace(inlineValue);
        var hasReference = !string.IsNullOrWhiteSpace(secretReference);
        if (hasInline && hasReference) {
            throw new InvalidOperationException(
                $"Provide either an inline secret or a secret reference for '{targetSecretName}', not both.");
        }

        if (!hasReference) {
            return hasInline ? inlineValue : null;
        }

        var (sourceProfileId, sourceSecretName) = Parse(secretReference!, targetProfileId);
        if (!string.Equals(sourceSecretName, targetSecretName, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException(
                $"Secret reference '{secretReference}' is not compatible with target secret '{targetSecretName}'.");
        }
        if (!string.Equals(sourceProfileId, targetProfileId, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException(
                $"Secret reference '{secretReference}' crosses profile boundaries. " +
                "Cross-profile secret references are not supported without owner-enforced audience metadata.");
        }
        var resolved = await secretStore.GetSecretAsync(sourceProfileId, sourceSecretName, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(resolved)) {
            throw new InvalidOperationException(
                $"Secret reference '{secretReference}' for '{targetSecretName}' was not found.");
        }

        return resolved;
    }

    public static (string ProfileId, string SecretName) Parse(string secretReference, string defaultProfileId) {
        var normalized = secretReference == null ? null : secretReference.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) {
            throw new InvalidOperationException("Secret reference cannot be empty.");
        }

        var value = normalized!;
        var colonIndex = value.IndexOf(':');
        var slashIndex = value.IndexOf('/');
        var separatorIndex = colonIndex >= 0 && slashIndex >= 0
            ? Math.Min(colonIndex, slashIndex)
            : Math.Max(colonIndex, slashIndex);

        if (separatorIndex < 0) {
            return (defaultProfileId, value);
        }

        var profileId = value.Substring(0, separatorIndex).Trim();
        var secretName = value.Substring(separatorIndex + 1).Trim();
        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(secretName)) {
            throw new InvalidOperationException(
                $"Secret reference '{secretReference}' must use '<profile-id>:<secret-name>' or '<secret-name>'.");
        }

        return (profileId, secretName);
    }
}
