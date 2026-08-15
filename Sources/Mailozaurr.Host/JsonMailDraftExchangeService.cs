using System.Text.Json;

namespace Mailozaurr.Hosting;

/// <summary>
/// Imports and exports drafts as JSON documents.
/// </summary>
public sealed class JsonMailDraftExchangeService : IMailDraftExchangeService {
    /// <inheritdoc />
    public async Task<MailDraft> LoadAsync(string path, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Draft path is required.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var draft = await JsonSerializer.DeserializeAsync(stream, ApplicationJsonContext.Default.MailDraft, cancellationToken).ConfigureAwait(false);
        if (draft == null) {
            throw new InvalidDataException($"Draft file '{fullPath}' did not contain a valid draft document.");
        }

        return draft;
    }

    /// <inheritdoc />
    public async Task SaveAsync(string path, MailDraft draft, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Draft path is required.", nameof(path));
        }
        if (draft == null) {
            throw new ArgumentNullException(nameof(draft));
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, draft, ApplicationJsonContext.Default.MailDraft, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}