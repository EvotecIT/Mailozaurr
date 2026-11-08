namespace Mailozaurr;

/// <summary>Resolves a profile credential each time a transport attempt starts.</summary>
public interface IMailCredentialResolver {
    /// <summary>Returns the current credential, or null if the reference is unavailable.</summary>
    Task<string?> ResolveAsync(MailCredentialReference reference, CancellationToken cancellationToken = default);
}

/// <summary>Resolves process environment variables and rereads mounted secret files for each attempt.</summary>
public sealed class MailCredentialResolver : IMailCredentialResolver {
    /// <inheritdoc />
    public async Task<string?> ResolveAsync(MailCredentialReference reference, CancellationToken cancellationToken = default) {
        Validate(reference);
        cancellationToken.ThrowIfCancellationRequested();
        if (reference.Source.Equals("environment", StringComparison.OrdinalIgnoreCase)) {
            return Environment.GetEnvironmentVariable(reference.Key);
        }

        using var stream = new FileStream(reference.Key, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, useAsync: true);
        using var reader = new StreamReader(stream);
#if NET8_0_OR_GREATER
        string value = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
#else
        string value = await reader.ReadToEndAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
#endif
        return value.TrimEnd('\r', '\n');
    }

    internal static void Validate(MailCredentialReference reference) {
        if (reference == null) throw new ArgumentNullException(nameof(reference));
        if (string.IsNullOrWhiteSpace(reference.Source) || string.IsNullOrWhiteSpace(reference.Key))
            throw new ArgumentException("A credential reference requires a source and key.", nameof(reference));
        if (reference.Source.Equals("environment", StringComparison.OrdinalIgnoreCase)) {
            if (reference.Key.IndexOfAny(new[] { '=', '\0' }) >= 0)
                throw new ArgumentException("A credential environment reference requires a valid variable name.", nameof(reference));
            return;
        }
        if (!reference.Source.Equals("file", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("The credential reference needs an application-supplied resolver.");
#if NET8_0_OR_GREATER
        if (!Path.IsPathFullyQualified(reference.Key))
#else
        if (!Path.IsPathRooted(reference.Key) || Path.GetFullPath(reference.Key) != reference.Key)
#endif
            throw new ArgumentException("A credential file reference requires an absolute path.", nameof(reference));
    }
}
