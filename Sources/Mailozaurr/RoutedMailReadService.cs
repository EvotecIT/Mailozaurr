namespace Mailozaurr;

/// <summary>
/// Resolves a profile and routes read operations to the matching provider handler.
/// </summary>
public sealed class RoutedMailReadService : IMailReadService {
    private const MailCapability HandlerCapabilities = MailCapability.ListFolders
        | MailCapability.SearchMessages
        | MailCapability.ReadMessages
        | MailCapability.SaveAttachments;
    private readonly IMailProfileStore _profileStore;
    private readonly IReadOnlyDictionary<MailProfileKind, IMailReadHandler> _handlers;

    /// <summary>
    /// Creates a new routed read service.
    /// </summary>
    public RoutedMailReadService(IMailProfileStore profileStore, IEnumerable<IMailReadHandler> handlers) {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        if (handlers == null) {
            throw new ArgumentNullException(nameof(handlers));
        }

        _handlers = handlers.ToDictionary(handler => handler.Kind);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FolderRef>> GetFoldersAsync(MailFolderQuery query, CancellationToken cancellationToken = default) {
        var profile = await GetProfileAsync(query.ProfileId, cancellationToken).ConfigureAwait(false);
        EnsureCapability(profile, MailCapability.ListFolders);
        return await GetHandler(profile.Kind).GetFoldersAsync(profile, query, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FolderRefCompact>> GetFoldersCompactAsync(MailFolderQuery query, CancellationToken cancellationToken = default) {
        var folders = await GetFoldersAsync(query, cancellationToken).ConfigureAwait(false);
        return folders.Select(ToCompact).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MessageSummary>> SearchAsync(MailSearchRequest request, CancellationToken cancellationToken = default) {
        var profile = await GetProfileAsync(request.ProfileId, cancellationToken).ConfigureAwait(false);
        EnsureCapability(profile, MailCapability.SearchMessages);
        return await GetHandler(profile.Kind).SearchAsync(profile, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MessageSummaryCompact>> SearchCompactAsync(MailSearchRequest request, CancellationToken cancellationToken = default) {
        var results = await SearchAsync(request, cancellationToken).ConfigureAwait(false);
        return results.Select(ToCompact).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttachmentSummary>> GetAttachmentsAsync(ListAttachmentsRequest request, CancellationToken cancellationToken = default) {
        var detail = await GetMessageAsync(new GetMessageRequest {
            ProfileId = request.ProfileId,
            MailboxId = request.MailboxId,
            FolderId = request.FolderId,
            MessageId = request.MessageId,
            IncludeRawContent = false
        }, cancellationToken).ConfigureAwait(false);

        return detail?.Attachments?.ToArray() ?? Array.Empty<AttachmentSummary>();
    }

    /// <inheritdoc />
    public async Task<SaveAttachmentsResult> SaveAttachmentsAsync(SaveAttachmentsRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var attachments = await GetAttachmentsAsync(new ListAttachmentsRequest {
            ProfileId = request.ProfileId,
            MailboxId = request.MailboxId,
            FolderId = request.FolderId,
            MessageId = request.MessageId
        }, cancellationToken).ConfigureAwait(false);

        var selectedAttachments = FilterAttachments(attachments, request).ToArray();
        if (selectedAttachments.Length == 0) {
            return new SaveAttachmentsResult {
                Succeeded = false,
                Code = "attachments_not_found",
                Message = "No attachments matched the requested filters.",
                ProfileId = request.ProfileId,
                MessageId = request.MessageId,
                MatchedCount = 0
            };
        }

        var result = new SaveAttachmentsResult {
            ProfileId = request.ProfileId,
            MessageId = request.MessageId,
            MatchedCount = selectedAttachments.Length
        };

        foreach (var attachment in selectedAttachments) {
            cancellationToken.ThrowIfCancellationRequested();

            var saveResult = await SaveAttachmentAsync(new SaveAttachmentRequest {
                ProfileId = request.ProfileId,
                MailboxId = request.MailboxId,
                FolderId = request.FolderId,
                MessageId = request.MessageId,
                AttachmentId = attachment.Id,
                DestinationPath = request.DestinationPath,
                Overwrite = request.Overwrite
            }, cancellationToken).ConfigureAwait(false);

            result.AttemptedCount++;
            if (saveResult.Succeeded) {
                result.SavedCount++;
            } else {
                result.FailedCount++;
            }

            result.Results.Add(new SavedAttachmentResult {
                Succeeded = saveResult.Succeeded,
                Code = saveResult.Code,
                Message = saveResult.Message,
                AttachmentId = attachment.Id,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType
            });
        }

        result.Succeeded = result.FailedCount == 0 && result.SavedCount > 0;
        result.Code = result.Succeeded ? null : "attachment_save_failed";
        result.Message = result.Succeeded
            ? $"Saved {result.SavedCount} attachment(s)."
            : $"Saved {result.SavedCount} attachment(s); {result.FailedCount} failed.";
        return result;
    }

    /// <inheritdoc />
    public async Task<SaveAttachmentsManyResult> SaveAttachmentsManyAsync(SaveAttachmentsManyRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var messageIds = request.MessageIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (messageIds.Length == 0) {
            return new SaveAttachmentsManyResult {
                Succeeded = false,
                Code = "messages_not_found",
                Message = "No messages were provided for attachment export.",
                ProfileId = request.ProfileId,
                RequestedMessageCount = 0
            };
        }

        var result = new SaveAttachmentsManyResult {
            ProfileId = request.ProfileId,
            RequestedMessageCount = messageIds.Length
        };

        foreach (var messageId in messageIds) {
            cancellationToken.ThrowIfCancellationRequested();

            var messageResult = await SaveAttachmentsAsync(new SaveAttachmentsRequest {
                ProfileId = request.ProfileId,
                MailboxId = request.MailboxId,
                FolderId = request.FolderId,
                MessageId = messageId,
                DestinationPath = request.DestinationPath,
                AttachmentIds = request.AttachmentIds.ToList(),
                FileNameContains = request.FileNameContains,
                ContentTypeContains = request.ContentTypeContains,
                Overwrite = request.Overwrite
            }, cancellationToken).ConfigureAwait(false);

            result.AttemptedMessageCount++;
            if (messageResult.Succeeded) {
                result.SucceededMessageCount++;
            } else {
                result.FailedMessageCount++;
            }

            result.MatchedCount += messageResult.MatchedCount;
            result.AttemptedCount += messageResult.AttemptedCount;
            result.SavedCount += messageResult.SavedCount;
            result.FailedCount += messageResult.FailedCount;
            result.MessageResults.Add(messageResult);
        }

        result.Succeeded = result.FailedMessageCount == 0 && result.SavedCount > 0;
        result.Code = result.Succeeded ? null : "attachment_save_failed";
        result.Message = result.Succeeded
            ? $"Saved {result.SavedCount} attachment(s) across {result.SucceededMessageCount} message(s)."
            : $"Saved {result.SavedCount} attachment(s) across {result.AttemptedMessageCount} message(s); {result.FailedMessageCount} message(s) failed.";
        return result;
    }

    /// <inheritdoc />
    public async Task<MessageDetail?> GetMessageAsync(GetMessageRequest request, CancellationToken cancellationToken = default) {
        var profile = await GetProfileAsync(request.ProfileId, cancellationToken).ConfigureAwait(false);
        EnsureCapability(profile, MailCapability.ReadMessages);
        return await GetHandler(profile.Kind).GetMessageAsync(profile, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MessageDetail>> GetMessagesAsync(GetMessagesRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var results = new List<MessageDetail>();
        foreach (var messageId in request.MessageIds.Where(id => !string.IsNullOrWhiteSpace(id))) {
            cancellationToken.ThrowIfCancellationRequested();

            var detail = await GetMessageAsync(new GetMessageRequest {
                ProfileId = request.ProfileId,
                MailboxId = request.MailboxId,
                FolderId = request.FolderId,
                MessageId = messageId.Trim(),
                IncludeRawContent = request.IncludeRawContent
            }, cancellationToken).ConfigureAwait(false);

            if (detail != null) {
                results.Add(detail);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<MessageDetailCompact?> GetMessageCompactAsync(GetMessageRequest request, CancellationToken cancellationToken = default) {
        var detail = await GetMessageAsync(request, cancellationToken).ConfigureAwait(false);
        return detail == null ? null : ToCompact(detail);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MessageDetailCompact>> GetMessagesCompactAsync(GetMessagesRequest request, CancellationToken cancellationToken = default) {
        var details = await GetMessagesAsync(request, cancellationToken).ConfigureAwait(false);
        return details.Select(ToCompact).ToArray();
    }

    /// <inheritdoc />
    public async Task<OperationResult> SaveAttachmentAsync(SaveAttachmentRequest request, CancellationToken cancellationToken = default) {
        var profile = await GetProfileAsync(request.ProfileId, cancellationToken).ConfigureAwait(false);
        EnsureCapability(profile, MailCapability.SaveAttachments);
        return await GetHandler(profile.Kind).SaveAttachmentAsync(profile, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MailProfile> GetProfileAsync(string profileId, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        var profile = await _profileStore.GetByIdAsync(profileId, cancellationToken).ConfigureAwait(false);
        return profile ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");
    }

    private IMailReadHandler GetHandler(MailProfileKind kind) =>
        _handlers.TryGetValue(kind, out var handler)
            ? handler
            : throw new NotSupportedException($"No read handler is registered for profile kind '{kind}'.");

    private static void EnsureCapability(MailProfile profile, MailCapability capability) {
        if (!profile.GetCapabilities(HandlerCapabilities).Supports(capability)) {
            throw new NotSupportedException($"Profile '{profile.Id}' does not support '{capability}'.");
        }
    }

    private static MessageSummaryCompact ToCompact(MessageSummary summary) => new() {
        ProfileId = summary.ProfileId,
        Id = summary.Id,
        FolderId = summary.FolderId,
        Subject = summary.Subject,
        Preview = summary.Preview,
        From = summary.From.Count > 0 ? FormatRecipient(summary.From[0]) : null,
        ReceivedAt = summary.ReceivedAt,
        IsRead = summary.IsRead,
        HasAttachments = summary.HasAttachments,
        Summary = $"{summary.Id} {summary.Subject ?? "(no subject)"}"
    };

    private static MessageDetailCompact ToCompact(MessageDetail detail) => new() {
        ProfileId = detail.ProfileId,
        Id = detail.Id,
        Summary = detail.Summary == null ? null : ToCompact(detail.Summary),
        TextBodyPreview = CreatePreview(detail.TextBody),
        HtmlBodyPreview = CreatePreview(detail.HtmlBody),
        Attachments = detail.Attachments.Select(ToCopy).ToList(),
        HasRawContent = !string.IsNullOrWhiteSpace(detail.RawContent),
        SummaryText = $"{detail.Id} {detail.Summary?.Subject ?? "(no subject)"}"
    };

    private static IEnumerable<AttachmentSummary> FilterAttachments(
        IEnumerable<AttachmentSummary> attachments,
        SaveAttachmentsRequest request) {
        var explicitIds = new HashSet<string>(
            request.AttachmentIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!.Trim()),
            StringComparer.OrdinalIgnoreCase);
        var fileNameContains = request.FileNameContains?.Trim();
        var contentTypeContains = request.ContentTypeContains?.Trim();
        var hasFileNameFilter = !string.IsNullOrWhiteSpace(fileNameContains);
        var hasContentTypeFilter = !string.IsNullOrWhiteSpace(contentTypeContains);
        var requiredFileName = fileNameContains ?? string.Empty;
        var requiredContentType = contentTypeContains ?? string.Empty;

        foreach (var attachment in attachments) {
            var attachmentFileName = attachment.FileName ?? string.Empty;
            if (explicitIds.Count > 0 &&
                !explicitIds.Contains(attachment.Id) &&
                (attachmentFileName.Length == 0 || !explicitIds.Contains(attachmentFileName))) {
                continue;
            }

            if (hasFileNameFilter) {
                if (attachmentFileName.Length == 0 ||
                    attachmentFileName.IndexOf(requiredFileName, StringComparison.OrdinalIgnoreCase) < 0) {
                    continue;
                }
            }

            if (hasContentTypeFilter) {
                var contentType = attachment.ContentType ?? string.Empty;
                if (contentType.Length == 0) {
                    continue;
                }

                if (contentType.IndexOf(requiredContentType, StringComparison.OrdinalIgnoreCase) < 0) {
                    continue;
                }
            }

            yield return attachment;
        }
    }

    private static FolderRefCompact ToCompact(FolderRef folder) => new() {
        ProfileId = folder.ProfileId,
        MailboxId = folder.MailboxId,
        Id = folder.Id,
        DisplayName = folder.DisplayName,
        Path = folder.Path,
        SpecialUse = folder.SpecialUse,
        MessageCount = folder.MessageCount,
        UnreadCount = folder.UnreadCount,
        Summary = $"{folder.Id} {folder.Path ?? folder.DisplayName}"
    };

    private static AttachmentSummary ToCopy(AttachmentSummary attachment) => new() {
        MessageId = attachment.MessageId,
        Id = attachment.Id,
        FileName = attachment.FileName,
        ContentType = attachment.ContentType,
        SizeInBytes = attachment.SizeInBytes,
        IsInline = attachment.IsInline,
        ContentId = attachment.ContentId
    };

    private static string? CreatePreview(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return value;
        }

        const int maxLength = 512;
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed.Substring(0, maxLength);
    }

    private static string? FormatRecipient(MessageRecipient recipient) {
        if (!string.IsNullOrWhiteSpace(recipient.Name) && !string.IsNullOrWhiteSpace(recipient.Address)) {
            return $"{recipient.Name} <{recipient.Address}>";
        }

        if (!string.IsNullOrWhiteSpace(recipient.Name)) {
            return recipient.Name;
        }

        return string.IsNullOrWhiteSpace(recipient.Address) ? null : recipient.Address;
    }
}
