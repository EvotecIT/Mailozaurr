namespace Mailozaurr;

/// <summary>Default provider-neutral EML export orchestration.</summary>
public sealed class MailEmlExportService : IMailEmlExportService {
    /// <summary>Maximum number of distinct messages accepted by one export request.</summary>
    public const int MaximumBatchMessageCount = 1000;

    private readonly IMailProfileStore _profileStore;
    private readonly IReadOnlyDictionary<MailProfileKind, IRawMailMessageSource> _sources;
    private readonly ProviderEmlArtifactWriter _writer;

    /// <summary>Creates an export service from provider-native raw-message sources.</summary>
    public MailEmlExportService(
        IMailProfileStore profileStore,
        IEnumerable<IRawMailMessageSource> sources,
        ProviderEmlArtifactWriter? writer = null) {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        if (sources == null) throw new ArgumentNullException(nameof(sources));
        _sources = sources.ToDictionary(source => source.Kind);
        _writer = writer ?? new ProviderEmlArtifactWriter();
    }

    /// <inheritdoc />
    public async Task<MailEmlExportResult> ExportAsync(
        MailEmlExportRequest request,
        CancellationToken cancellationToken = default) {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.ProfileId)) throw new ArgumentException("Profile id is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.DestinationDirectory)) throw new ArgumentException("Destination directory is required.", nameof(request));
        if (request.MaxMessageBytes <= 0 || request.MaxMessageBytes > int.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(request.MaxMessageBytes));
        }

        if (request.MessageIds == null) throw new ArgumentException("At least one message id is required.", nameof(request));
        var messageIds = request.MessageIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(MaximumBatchMessageCount + 1)
            .ToArray();
        if (messageIds.Length == 0) throw new ArgumentException("At least one message id is required.", nameof(request));
        if (messageIds.Length > MaximumBatchMessageCount) {
            throw new ArgumentOutOfRangeException(
                nameof(request.MessageIds),
                $"A single EML export may contain at most {MaximumBatchMessageCount} distinct message ids.");
        }

        var profile = await _profileStore.GetByIdAsync(request.ProfileId.Trim(), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Profile '{request.ProfileId}' was not found.");
        if (!_sources.TryGetValue(profile.Kind, out var source)) {
            throw new NotSupportedException($"Raw EML export is not configured for profile kind '{profile.Kind}'.");
        }

        var destinationDirectory = Path.GetFullPath(request.DestinationDirectory);
        Directory.CreateDirectory(destinationDirectory);
        var result = new MailEmlExportResult {
            ProfileId = profile.Id,
            DestinationDirectory = destinationDirectory,
            RequestedCount = messageIds.Length
        };

        foreach (var messageId in messageIds) {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new MailEmlExportItemResult {
                MessageId = messageId
            };
            result.Results.Add(item);

            try {
                var raw = await source.GetRawMessageAsync(profile, new RawMailMessageRequest {
                    MailboxId = request.MailboxId,
                    FolderId = request.FolderId,
                    MessageId = messageId,
                    MaxBytes = request.MaxMessageBytes
                }, cancellationToken).ConfigureAwait(false);
                if (raw == null) {
                    item.Code = "message_not_found";
                    item.Message = $"Message '{messageId}' was not found.";
                    result.FailedCount++;
                    continue;
                }

                var storageMessageId = CanonicalizeMessageIdForStorage(profile, messageId);
                var destinationPath = MimeAttachmentStorage.ResolveDestinationPath(
                    destinationDirectory,
                    storageMessageId + ".eml",
                    CreateStorageIdentity(profile, request, storageMessageId, raw.StorageIdentityComponent));
                item.DestinationPath = destinationPath;
                if (File.Exists(destinationPath) && !request.Overwrite) {
                    item.Code = "destination_exists";
                    item.Message = $"Destination '{destinationPath}' already exists.";
                    result.FailedCount++;
                    continue;
                }

                ProviderEmlArtifactWriteResult write;
                try {
                    write = await _writer.WriteAsync(
                        raw.Content,
                        destinationPath,
                        request.MaxMessageBytes,
                        request.Overwrite,
                        cancellationToken).ConfigureAwait(false);
                } catch (IOException) when (!request.Overwrite && File.Exists(destinationPath)) {
                    item.Code = "destination_exists";
                    item.Message = $"Destination '{destinationPath}' already exists.";
                    result.FailedCount++;
                    continue;
                }
                item.Succeeded = true;
                item.Message = $"Exported message '{messageId}'.";
                item.BytesWritten = write.BytesWritten;
                item.Sha256 = write.Sha256;
                item.UsedPreservedSource = write.UsedPreservedSource;
                item.DiagnosticCodes = write.DiagnosticCodes;
                result.ExportedCount++;
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                item.Code = "eml_export_failed";
                item.Message = ex.Message;
                result.FailedCount++;
            }
        }

        result.Succeeded = result.FailedCount == 0 && result.ExportedCount > 0;
        result.Code = result.Succeeded ? null : "eml_export_failed";
        result.Message = result.Succeeded
            ? $"Exported {result.ExportedCount} EML message(s)."
            : $"Exported {result.ExportedCount} EML message(s); {result.FailedCount} failed.";
        return result;
    }

    private static string CreateStorageIdentity(
        MailProfile profile,
        MailEmlExportRequest request,
        string messageId,
        string? providerIdentityComponent) {
        string? mailbox = null;
        string? folder = null;
        switch (profile.Kind) {
            case MailProfileKind.Pop3:
                folder = Pop3MailReadHandler.NormalizeFolderId(request.FolderId);
                break;
            case MailProfileKind.Imap:
                folder = ImapMailReadHandler.CanonicalizeFolderForStorage(
                    ImapMailReadHandler.ResolveFolder(request.FolderId, profile));
                break;
            case MailProfileKind.Graph:
                mailbox = GraphMailReadHandler.ResolveUserId(profile, request.MailboxId);
                break;
            case MailProfileKind.Gmail:
                mailbox = GmailMailReadHandler.ResolveUserId(profile, request.MailboxId);
                break;
            default:
                mailbox = request.MailboxId?.Trim();
                folder = request.FolderId?.Trim();
                break;
        }
        return MimeAttachmentStorage.CreateStorageIdentity(
            profile.Id,
            profile.Kind.ToString(),
            mailbox,
            folder,
            messageId,
            providerIdentityComponent);
    }

    private static string CanonicalizeMessageIdForStorage(MailProfile profile, string messageId) =>
        profile.Kind == MailProfileKind.Imap
            ? ImapMailReadHandler.CanonicalizeUidForStorage(messageId)
            : messageId;
}
