using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

public sealed partial class GraphMailboxBrowser {
    /// <summary>
    /// Sends a MIME message by creating a Graph draft and dispatching it.
    /// </summary>
    /// <param name="message">MIME message to send.</param>
    /// <param name="maxInlineAttachmentBytes">Maximum inline-attachment budget in bytes.</param>
    /// <param name="idempotencyHeaderName">Optional idempotency header name to preserve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Send result metadata.</returns>
    public async Task<GraphMailboxSendResult> SendMessageAsync(
        MimeMessage message,
        int maxInlineAttachmentBytes = GraphMimePreparation.DefaultMaxInlineAttachmentBytes,
        string? idempotencyHeaderName = null,
        CancellationToken cancellationToken = default) {
        var draft = await ImportMessageAsync(
            message,
            folder: "Drafts",
            maxInlineAttachmentBytes: maxInlineAttachmentBytes,
            idempotencyHeaderName: idempotencyHeaderName,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var draftId = NormalizeOptional(draft.NativeId);
        if (draftId == null) {
            throw new InvalidDataException("Graph draft created but response did not include message id.");
        }

        await _graph.SendDraftMessageAsync(draftId, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new GraphMailboxSendResult {
            DraftId = draftId,
            MessageId = draft.MessageId
        };
    }

    /// <summary>
    /// Probes a Graph folder for a message with a matching RFC822 <c>Message-Id</c> token.
    /// </summary>
    /// <param name="messageIdToken">Message-Id token (with or without angle brackets).</param>
    /// <param name="folder">Folder alias/id. Defaults to <c>Sent Items</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Duplicate probe result.</returns>
    public async Task<GraphMailboxDuplicateProbeResult> FindMessageByInternetMessageIdAsync(
        string messageIdToken,
        string? folder = "Sent Items",
        CancellationToken cancellationToken = default) {
        var normalizedToken = NormalizeMessageIdValue(messageIdToken);
        if (normalizedToken == null) {
            throw new ArgumentException("messageIdToken is required.", nameof(messageIdToken));
        }

        var folderSelector = ResolveFolderSelector(folder);
        var bracketedToken = "<" + normalizedToken + ">";
        var filter = "internetMessageId eq '" + EscapeODataStringLiteral(bracketedToken) +
                     "' or internetMessageId eq '" + EscapeODataStringLiteral(normalizedToken) + "'";
        var page = await _graph.ListMessagesAsync(
            folderSelector,
            top: 1,
            skip: null,
            select: "id,internetMessageId",
            orderBy: null,
            filter: filter,
            search: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var match = page.Items.FirstOrDefault(x =>
            string.Equals(
                NormalizeMessageIdValue(x.InternetMessageId),
                normalizedToken,
                StringComparison.OrdinalIgnoreCase));
        if (match == null) {
            return new GraphMailboxDuplicateProbeResult {
                IsMatch = false,
                FolderSelector = folderSelector
            };
        }

        return new GraphMailboxDuplicateProbeResult {
            IsMatch = true,
            FolderSelector = folderSelector,
            NativeId = NormalizeOptional(match.Id),
            MessageId = NormalizeMessageIdValue(match.InternetMessageId)
        };
    }

    /// <summary>
    /// Creates a Graph webhook subscription for message changes in a selected folder.
    /// </summary>
    public async Task<GraphMailboxSubscriptionResult> CreateMessageSubscriptionAsync(
        string notificationUrl,
        string folder = "INBOX",
        DateTimeOffset? expirationDateTime = null,
        string changeType = "created,updated,deleted",
        string? clientState = null,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(notificationUrl)) {
            throw new ArgumentException("notificationUrl is required.", nameof(notificationUrl));
        }
        if (string.IsNullOrWhiteSpace(changeType)) {
            throw new ArgumentException("changeType is required.", nameof(changeType));
        }

        var resource = BuildMessageSubscriptionResource(folder);
        var request = new GraphApiClient.GraphCreateSubscriptionRequest {
            ChangeType = changeType.Trim(),
            NotificationUrl = notificationUrl.Trim(),
            Resource = resource,
            ExpirationDateTime = expirationDateTime ?? DateTimeOffset.UtcNow.AddHours(8),
            ClientState = string.IsNullOrWhiteSpace(clientState) ? null : clientState!.Trim()
        };

        var created = await _graph.CreateSubscriptionAsync(request, cancellationToken).ConfigureAwait(false);
        return MapSubscription(created, resource);
    }

    /// <summary>
    /// Renews an existing Graph webhook subscription.
    /// </summary>
    public async Task<GraphMailboxSubscriptionResult> RenewSubscriptionAsync(
        string subscriptionId,
        DateTimeOffset expirationDateTime,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(subscriptionId)) {
            throw new ArgumentException("subscriptionId is required.", nameof(subscriptionId));
        }

        var renewed = await _graph.RenewSubscriptionAsync(
            subscriptionId.Trim(),
            expirationDateTime,
            cancellationToken).ConfigureAwait(false);
        return MapSubscription(renewed, NormalizeOptional(renewed.Resource));
    }

    /// <summary>
    /// Renews an existing Graph webhook subscription with stale-remote handling.
    /// </summary>
    public async Task<GraphMailboxSubscriptionRenewResult> RenewSubscriptionSafeAsync(
        string subscriptionId,
        DateTimeOffset expirationDateTime,
        bool treatMissingAsStale = true,
        CancellationToken cancellationToken = default) {
        try {
            var renewed = await RenewSubscriptionAsync(subscriptionId, expirationDateTime, cancellationToken).ConfigureAwait(false);
            return new GraphMailboxSubscriptionRenewResult {
                Renewed = true,
                Missing = false,
                Subscription = renewed
            };
        } catch (GraphApiException ex) when (treatMissingAsStale &&
                                             (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.Gone)) {
            return new GraphMailboxSubscriptionRenewResult {
                Renewed = false,
                Missing = true,
                Subscription = null
            };
        }
    }

    /// <summary>
    /// Deletes an existing Graph webhook subscription.
    /// </summary>
    public async Task<GraphMailboxSubscriptionDeleteResult> DeleteSubscriptionAsync(
        string subscriptionId,
        bool treatMissingAsSuccess = true,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(subscriptionId)) {
            throw new ArgumentException("subscriptionId is required.", nameof(subscriptionId));
        }

        try {
            await _graph.DeleteSubscriptionAsync(subscriptionId.Trim(), cancellationToken).ConfigureAwait(false);
            return new GraphMailboxSubscriptionDeleteResult { Deleted = true };
        } catch (GraphApiException ex) when (treatMissingAsSuccess &&
                                             (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.Gone)) {
            return new GraphMailboxSubscriptionDeleteResult {
                Deleted = true,
                AlreadyDeleted = true
            };
        }
    }

    /// <summary>
    /// Builds Graph subscription resource for folder message notifications.
    /// </summary>
    public static string BuildMessageSubscriptionResource(string folder) {
        var selector = ResolveFolderSelector(folder);
        return "me/mailFolders('" + EscapeGraphLiteral(selector) + "')/messages";
    }
}