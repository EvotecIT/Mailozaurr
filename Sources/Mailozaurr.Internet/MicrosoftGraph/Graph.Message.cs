using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Threading;

namespace Mailozaurr;

public partial class Graph {
    /// <summary>
    /// Builds the <see cref="GraphMessageContainer"/> object that represents the email.
    /// </summary>
    public void CreateMessage() {
        CreateAttachments();
        PrepareAutoEmbeddedImages();
        if (From is null) {
            throw new InvalidOperationException("From address must be specified.");
        }
        // Note: The display name for the sender is controlled by Office 365 and may not reflect the value you provide here.
        // Office 365 will use the mailbox's configured display name for the sender, regardless of what is set in the payload.
        // Always use the email address for API calls and authentication.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        MessageContainer = new GraphMessageContainer {
            Message = new GraphMessage {
                From = ConvertToGraphEmailAddress(From),
                To = ConvertToGraphEmailAddressUnique(To, seen),
                Cc = ConvertToGraphEmailAddressUnique(Cc, seen),
                Bcc = ConvertToGraphEmailAddressUnique(Bcc, seen),
                ReplyTo = string.IsNullOrWhiteSpace(ReplyTo)
                    ? null
                    : new List<GraphEmailAddress> { ConvertToGraphEmailAddress(ReplyTo)! },
                Subject = Subject,
                Body = new GraphContent { Content = HTML, Type = ContentType },
                Importance = MapImportance(Priority),
                IsDeliveryReceiptRequested = RequestDeliveryReceipt,
                IsReadReceiptRequested = RequestReadReceipt
            },
            SaveToSentItems = !DoNotSaveToSentItems
        };
        if (ConvertedAttachments.Count > 0) {
            MessageContainer.Message.Attachments = ConvertedAttachments;
        }
        if (Headers != null && Headers.Count > 0) {
            MessageContainer.Message.InternetMessageHeaders = Headers.Select(kvp => new GraphInternetMessageHeader { Name = kvp.Key, Value = kvp.Value }).ToList();
        }

        MessageJson = JsonSerializer.Serialize(MessageContainer, GraphJsonContext.Default.GraphMessageContainer);
        if (Encoding.UTF8.GetByteCount(MessageJson) > GraphPayloadLimitBytes) {
            TryRouteConvertedFileAttachmentsThroughUploadSession();
            TryRouteEligibleAttachments(() => Encoding.UTF8.GetByteCount(MessageJson) > GraphPayloadLimitBytes);
            if (Encoding.UTF8.GetByteCount(MessageJson) > GraphPayloadLimitBytes) {
                throw new InvalidOperationException("The complete serialized Graph request exceeds the 4MB payload limit after draft attachments were removed. Reduce the message body, recipients, or headers.");
            }
        }
        //LoggingMessages.Logger.WriteVerbose(MessageJson);
    }

    private void PrepareAutoEmbeddedImages() {
        if (!AutoEmbedImages) {
            if (_autoEmbedRenderedHtml != null &&
                string.Equals(HTML, _autoEmbedRenderedHtml, StringComparison.Ordinal)) {
                HTML = _autoEmbedOriginalHtml ?? HTML;
            }
            ClearAutoEmbeddedImageState();
            return;
        }

        var sourceHtml = _autoEmbedRenderedHtml != null &&
                         string.Equals(HTML, _autoEmbedRenderedHtml, StringComparison.Ordinal)
            ? _autoEmbedOriginalHtml ?? HTML
            : HTML;
        HtmlUtils.LocalImage[] existingImages = (Attachments ?? Array.Empty<object>())
            .OfType<Definitions.AttachmentDescriptor>()
            .Where(descriptor => IsInlineDescriptor(descriptor) &&
                                 !string.IsNullOrWhiteSpace(descriptor.SourcePath))
            .Select(descriptor => new HtmlUtils.LocalImage(
                descriptor.SourcePath!,
                string.IsNullOrWhiteSpace(descriptor.ContentId)
                    ? Path.GetFileName(descriptor.SourcePath!)
                    : descriptor.ContentId!))
            .ToArray();
        var (renderedHtml, images) = HtmlUtils.ExtractLocalImages(
            sourceHtml,
            ConvertedAttachments
                .Where(attachment => attachment.IsInline)
                .Select(attachment => attachment.ContentId ?? string.Empty),
            existingImages);
        _autoEmbedOriginalHtml = sourceHtml;
        _autoEmbedRenderedHtml = renderedHtml;
        _autoEmbeddedImages.Clear();
        _autoEmbeddedImages.AddRange(images);
        HTML = renderedHtml;

        foreach (HtmlUtils.LocalImage image in _autoEmbeddedImages) {
            var attachment = GraphAttachment.FromFile(image.Path);
            attachment.IsInline = true;
            attachment.ContentId = image.ContentId;
            ConvertedAttachments.Add(attachment);
            var size = EstimateAttachmentSize(attachment);
            _inlineAttachmentSizeBytes += size;
            TotalAttachmentSizeBytes += size;
            RawAttachmentSizeBytes += EstimateRawAttachmentSize(attachment);
        }
    }

    private void ClearAutoEmbeddedImageState() {
        _autoEmbedOriginalHtml = null;
        _autoEmbedRenderedHtml = null;
        _autoEmbeddedImages.Clear();
    }

    private GraphEmailAddress? ConvertToGraphEmailAddress(object? email) {
        if (email == null) {
            return null;
        }
        var address = Helpers.GetEmailAddress(email);
        return new GraphEmailAddress { Email = new GraphEmail { Address = address } };
    }

    private List<GraphEmailAddress>? ConvertToGraphEmailAddress(object[]? emails) {
        if (emails == null) {
            return null;
        }
        return emails.Select(email => new GraphEmailAddress { Email = new GraphEmail { Address = Helpers.GetEmailAddress(email) } }).ToList();
    }

    private List<GraphEmailAddress>? ConvertToGraphEmailAddressUnique(object[]? emails, HashSet<string> seen) {
        if (emails == null) {
            return null;
        }

        var list = Helpers.UniqueAddresses(emails, seen)
            .Select(email => new GraphEmailAddress { Email = new GraphEmail { Address = Helpers.GetEmailAddress(email) } })
            .ToList();
        return list.Count == 0 ? null : list;
    }

}
