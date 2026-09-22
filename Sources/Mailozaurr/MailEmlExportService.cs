namespace Mailozaurr;

/// <summary>Default provider-neutral EML export orchestration.</summary>
public sealed class MailEmlExportService : IMailEmlExportService, IMailEmlArchiveScopeProvider {
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
    public async Task<string> GetArchiveScopeAsync(MailProfile profile, string? mailboxId, string? folderId,
        CancellationToken cancellationToken = default) {
        if (!_sources.TryGetValue(profile.Kind, out var source))
            throw new NotSupportedException($"Raw EML export is not configured for profile kind '{profile.Kind}'.");
        using var session = await OpenScopedSessionAsync(source, profile, cancellationToken).ConfigureAwait(false);
        if (session is not IRawMailMessageScopeSession scopeSession)
            throw new NotSupportedException("Archive requires a source that exposes a verified mailbox scope.");
        return await scopeSession.GetScopeAsync(mailboxId, folderId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MailEmlExportResult> ExportAsync(
        MailEmlExportRequest request,
        CancellationToken cancellationToken = default) {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.ProfileId)) throw new ArgumentException("Profile id is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.DestinationDirectory)) throw new ArgumentException("Destination directory is required.", nameof(request));
        if (request.MaxMessageBytes <= 0 || request.MaxMessageBytes > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(request.MaxMessageBytes));
        if (request.MessageIds == null) throw new ArgumentException("At least one message id is required.", nameof(request));
        var profile = await _profileStore.GetByIdAsync(request.ProfileId.Trim(), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Profile '{request.ProfileId}' was not found.");
        if (profile.Kind == MailProfileKind.Imap && request.MaxMessageBytes == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(request.MaxMessageBytes));
        _ = CanonicalizeMessageIds(profile, request.MessageIds);
        using var sourceSession = await GetSource(profile).OpenSessionAsync(profile, cancellationToken).ConfigureAwait(false);
        return await ExportWithSessionAsync(request, profile, sourceSession, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<IMailEmlArchiveBatchSession> OpenArchiveSessionAsync(
        MailProfile profile, CancellationToken cancellationToken) {
        var source = GetSource(profile);
        var sourceSession = await OpenScopedSessionAsync(source, profile, cancellationToken).ConfigureAwait(false);
        return new ArchiveBatchSession(this, profile, sourceSession);
    }

    private static Task<IRawMailMessageSession> OpenScopedSessionAsync(IRawMailMessageSource source,
        MailProfile profile, CancellationToken cancellationToken) =>
        source is IArchiveRawMailMessageSource archiveSource
            ? archiveSource.OpenArchiveSessionAsync(profile, cancellationToken)
            : source.OpenSessionAsync(profile, cancellationToken);

    private IRawMailMessageSource GetSource(MailProfile profile) =>
        _sources.TryGetValue(profile.Kind, out var source)
            ? source
            : throw new NotSupportedException($"Raw EML export is not configured for profile kind '{profile.Kind}'.");

    private async Task<MailEmlExportResult> ExportWithSessionAsync(
        MailEmlExportRequest request, MailProfile profile, IRawMailMessageSession sourceSession,
        CancellationToken cancellationToken, bool validateInitialProviderScope = true) {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (!string.Equals(request.ProfileId.Trim(), profile.Id, StringComparison.Ordinal))
            throw new InvalidOperationException("An archive export session cannot switch profiles.");
        if (string.IsNullOrWhiteSpace(request.DestinationDirectory)) throw new ArgumentException("Destination directory is required.", nameof(request));
        if (request.MaxMessageBytes <= 0 || request.MaxMessageBytes > int.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(request.MaxMessageBytes));
        }

        if (profile.Kind == MailProfileKind.Imap && request.MaxMessageBytes == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(request.MaxMessageBytes),
                "IMAP export limit must be below Int32.MaxValue so an extra byte can detect oversized content.");
        if (request.MessageIds == null) throw new ArgumentException("At least one message id is required.", nameof(request));
        var messageIds = CanonicalizeMessageIds(profile, request.MessageIds);
        if (messageIds.Count == 0) throw new ArgumentException("At least one message id is required.", nameof(request));
        var destinationDirectory = Path.GetFullPath(request.DestinationDirectory);
        Directory.CreateDirectory(destinationDirectory);
        if (request.ExpectedProviderScope != null && validateInitialProviderScope) {
            if (sourceSession is not IRawMailMessageScopeSession scopedSession)
                throw new NotSupportedException("Archive export requires a provider scope aware source.");
            var actualScope = await scopedSession.GetScopeAsync(request.MailboxId, request.FolderId, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(actualScope, request.ExpectedProviderScope, StringComparison.Ordinal))
                throw new InvalidOperationException("The provider mailbox identity changed during this archive run.");
        }
        var result = new MailEmlExportResult {
            ProfileId = profile.Id,
            DestinationDirectory = destinationDirectory,
            RequestedCount = messageIds.Count
        };

        foreach (var messageId in messageIds) {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new MailEmlExportItemResult {
                MessageId = messageId
            };
            result.Results.Add(item);

            try {
                if (request.ExpectedProviderScope != null && profile.Kind == MailProfileKind.Imap) {
                    var currentScope = await ((IRawMailMessageScopeSession)sourceSession)
                        .GetScopeAsync(request.MailboxId, request.FolderId, cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(currentScope, request.ExpectedProviderScope, StringComparison.Ordinal))
                        throw new InvalidOperationException("The provider mailbox identity changed during this archive run.");
                }
                var rawRequest = new RawMailMessageRequest {
                    MailboxId = request.MailboxId,
                    FolderId = request.FolderId,
                    MessageId = messageId,
                    MaxBytes = request.MaxMessageBytes
                };
                using var raw = sourceSession is IStreamingRawMailMessageSession streamingSession
                    ? await streamingSession.GetRawMessageStreamAsync(rawRequest, cancellationToken).ConfigureAwait(false)
                    : await sourceSession.GetRawMessageAsync(rawRequest, cancellationToken).ConfigureAwait(false);
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
                    using var buffered = raw.ContentStream == null
                        ? new MemoryStream(raw.Content, writable: false)
                        : null;
                    write = await _writer.WriteAsync(
                        raw.ContentStream ?? buffered!,
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
                item.BytesWritten = write.BytesWritten;
                item.Sha256 = write.Sha256;
                item.UsedPreservedSource = write.UsedPreservedSource;
                item.DiagnosticCodes = write.DiagnosticCodes;
                if (request.ExpectedProviderScope != null && profile.Kind == MailProfileKind.Imap) {
                    var currentScope = await ((IRawMailMessageScopeSession)sourceSession)
                        .GetScopeAsync(request.MailboxId, request.FolderId, cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(currentScope, request.ExpectedProviderScope, StringComparison.Ordinal))
                        throw new InvalidOperationException("The provider mailbox identity changed during this archive run.");
                }
                item.Succeeded = true;
                item.Message = $"Exported message '{messageId}'.";
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

    private sealed class ArchiveBatchSession : IMailEmlArchiveBatchSession {
        private readonly MailEmlExportService _owner;
        private readonly MailProfile _profile;
        private readonly IRawMailMessageSession _sourceSession;
        private string? _validatedRemoteProviderScope;

        internal ArchiveBatchSession(MailEmlExportService owner, MailProfile profile,
            IRawMailMessageSession sourceSession) {
            _owner = owner;
            _profile = profile;
            _sourceSession = sourceSession;
        }

        public async Task<MailEmlExportResult> ExportAsync(MailEmlExportRequest request,
            CancellationToken cancellationToken) {
            var isRemoteIdentityProvider = _profile.Kind == MailProfileKind.Graph ||
                _profile.Kind == MailProfileKind.Gmail;
            var validateInitialScope = !isRemoteIdentityProvider ||
                !string.Equals(_validatedRemoteProviderScope, request.ExpectedProviderScope,
                    StringComparison.Ordinal);
            var result = await _owner.ExportWithSessionAsync(request, _profile, _sourceSession,
                cancellationToken, validateInitialScope).ConfigureAwait(false);
            if (isRemoteIdentityProvider && request.ExpectedProviderScope != null)
                _validatedRemoteProviderScope = request.ExpectedProviderScope;
            return result;
        }

        public void Dispose() => _sourceSession.Dispose();
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
                mailbox = GraphMailReadHandler.CanonicalizeUserIdForStorage(
                    profile,
                    GraphMailReadHandler.ResolveUserId(profile, request.MailboxId));
                break;
            case MailProfileKind.Gmail:
                mailbox = GmailMailReadHandler.CanonicalizeUserIdForStorage(
                    profile,
                    GmailMailReadHandler.ResolveUserId(profile, request.MailboxId));
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

    internal static string CanonicalizeMessageIdForStorage(MailProfile profile, string messageId) =>
        profile.Kind switch {
            MailProfileKind.Imap => ImapMailReadHandler.CanonicalizeUidForStorage(messageId),
            MailProfileKind.Pop3 => Pop3MailReadHandler.CanonicalizeMessageIdForStorage(messageId),
            _ => messageId
        };

    internal static IReadOnlyList<string> CanonicalizeMessageIds(
        MailProfile profile,
        IEnumerable<string> requestedMessageIds) {
        var messageIds = new List<string>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in requestedMessageIds) {
            if (string.IsNullOrWhiteSpace(value)) continue;
            var messageId = CanonicalizeMessageIdForStorage(profile, value.Trim());
            if (!identities.Add(messageId)) continue;
            if (identities.Count > MaximumBatchMessageCount) {
                throw new ArgumentOutOfRangeException(
                    nameof(MailEmlExportRequest.MessageIds),
                    $"A single EML export may contain at most {MaximumBatchMessageCount} distinct message ids.");
            }
            messageIds.Add(messageId);
        }
        return messageIds;
    }
}
