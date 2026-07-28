using Mailozaurr.Definitions;
using MimeKit;
using MimeKit.Utils;
using System.Globalization;

namespace Mailozaurr.Application;

internal static class ProviderMailSendHandlerSupport {
    public static void Validate(MailProfile profile, SendMessageRequest request, MailProfileKind expectedKind) {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (profile.Kind != expectedKind) {
            throw new InvalidOperationException($"Profile '{profile.Id}' is not a {expectedKind} profile.");
        }
        if (request.NotBefore.HasValue) {
            throw new NotSupportedException($"Scheduled {expectedKind} sends are not yet supported by the application send handler.");
        }
        if (!request.Message.To.Any(HasAddress) &&
            !request.Message.Cc.Any(HasAddress) &&
            !request.Message.Bcc.Any(HasAddress)) {
            throw new InvalidOperationException("At least one non-empty To, Cc, or Bcc recipient is required.");
        }
    }

    public static async Task<string> RequireSecretAsync(
        IMailSecretStore secretStore,
        MailProfile profile,
        string secretName,
        CancellationToken cancellationToken) {
        var value = await secretStore.GetSecretAsync(profile.Id, secretName, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(value)) {
            throw new InvalidOperationException($"Secret '{secretName}' is required for profile '{profile.Id}'.");
        }
        return value!.Trim();
    }

    public static MessageRecipient ResolveSender(MailProfile profile, DraftMessage message) {
        if (message.From is { } sender && HasAddress(sender)) return sender;
        if (string.IsNullOrWhiteSpace(profile.DefaultSender)) {
            throw new InvalidOperationException($"Profile '{profile.Id}' requires a default sender or message sender.");
        }
        var parsed = MailboxAddress.Parse(profile.DefaultSender!.Trim());
        return new MessageRecipient { Name = parsed.Name, Address = parsed.Address };
    }

    public static List<object> Recipients(IEnumerable<MessageRecipient> recipients) =>
        recipients
            .Where(recipient => !string.IsNullOrWhiteSpace(recipient.Address))
            .Select(recipient => (object)ToMailboxAddress(recipient))
            .ToList();

    public static List<object> SendGridRecipients(IEnumerable<MessageRecipient> recipients) =>
        recipients
            .Where(recipient => !string.IsNullOrWhiteSpace(recipient.Address))
            .Select(recipient => (object)ToSendGridAddress(recipient))
            .ToList();

    public static MailboxAddress ToMailboxAddress(MessageRecipient recipient) =>
        new(recipient.Name ?? string.Empty, recipient.Address.Trim());

    public static SendGridEmailAddress ToSendGridAddress(MessageRecipient recipient) => new() {
        Name = string.IsNullOrWhiteSpace(recipient.Name) ? null : recipient.Name!.Trim(),
        Email = recipient.Address.Trim()
    };

    public static MessageRecipient? ResolveReplyTo(DraftMessage message) =>
        message.ReplyTo.FirstOrDefault(HasAddress);

    public static List<AttachmentDescriptor> Attachments(DraftMessage message) =>
        message.Attachments
            .Where(attachment => !string.IsNullOrWhiteSpace(attachment.Path))
            .Select(CreateAttachment)
            .ToList();

    public static void ApplyRetrySettings(MailProfile profile, Action<int, int, double, int, int, bool> apply) {
        var retryCount = RequireNonNegative(profile, MailProfileSettingsKeys.RetryCount, GetInt(profile, MailProfileSettingsKeys.RetryCount) ?? 0);
        var retryDelay = RequireNonNegative(profile, MailProfileSettingsKeys.RetryDelayMilliseconds, GetInt(profile, MailProfileSettingsKeys.RetryDelayMilliseconds) ?? 0);
        var backoff = GetDouble(profile, MailProfileSettingsKeys.RetryDelayBackoff) ?? 1.0;
        if (double.IsNaN(backoff) || double.IsInfinity(backoff) || backoff < 1.0) {
            throw new InvalidOperationException(
                $"Profile '{profile.Id}' setting '{MailProfileSettingsKeys.RetryDelayBackoff}' must be a finite number greater than or equal to 1.");
        }
        var maxDelay = RequireNonNegative(profile, MailProfileSettingsKeys.MaxDelayMilliseconds, GetInt(profile, MailProfileSettingsKeys.MaxDelayMilliseconds) ?? 0);
        var jitter = RequireNonNegative(profile, MailProfileSettingsKeys.JitterMilliseconds, GetInt(profile, MailProfileSettingsKeys.JitterMilliseconds) ?? 0);

        apply(retryCount, retryDelay, backoff, maxDelay, jitter, GetBool(profile, MailProfileSettingsKeys.RetryAlways) ?? false);
    }

    public static string? GetSetting(MailProfile profile, string key) =>
        profile.Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    public static async Task<SendResult> ToResultAsync(
        MailProfile profile,
        SmtpResult result,
        IPendingMessageRepository? pendingMessageRepository,
        CancellationToken cancellationToken) {
        var queued = false;
        if (!result.Status &&
            pendingMessageRepository != null &&
            !string.IsNullOrWhiteSpace(result.MessageId)) {
            queued = await pendingMessageRepository
                .GetByMessageIdAsync(result.MessageId!, cancellationToken)
                .ConfigureAwait(false) != null;
        }
        return new SendResult {
            Succeeded = result.Status || queued,
            ProfileId = profile.Id,
            ProfileKind = profile.Kind,
            ProviderMessageId = result.Status ? result.MessageId : null,
            Queued = queued,
            QueueMessageId = queued ? result.MessageId : null,
            Message = result.Status
                ? "Message sent successfully."
                : queued
                    ? $"Message queued after send failure: {result.Error ?? result.Message ?? "Provider send failed."}"
                    : result.Error ?? result.Message ?? "Provider send failed."
        };
    }

    private static AttachmentDescriptor CreateAttachment(DraftAttachment attachment) {
        var fullPath = Path.GetFullPath(attachment.Path);
        if (!File.Exists(fullPath)) {
            throw new FileNotFoundException($"Attachment '{attachment.Path}' was not found.", fullPath);
        }

        var descriptor = new FileAttachmentDescriptor(fullPath) {
            ContentDisposition = new ContentDisposition(
                attachment.IsInline ? ContentDisposition.Inline : ContentDisposition.Attachment)
        };
        if (attachment.FileName is { } fileName && !string.IsNullOrWhiteSpace(fileName)) descriptor.FileName = fileName.Trim();
        if (attachment.ContentType is { } contentType && !string.IsNullOrWhiteSpace(contentType)) descriptor.ContentType = contentType.Trim();
        if (attachment.ContentId is { } contentId && !string.IsNullOrWhiteSpace(contentId)) {
            descriptor.ContentId = contentId.Trim();
        } else if (attachment.IsInline) {
            descriptor.ContentId = !string.IsNullOrWhiteSpace(descriptor.FileName)
                ? descriptor.FileName
                : MimeUtils.GenerateMessageId();
        }
        return descriptor;
    }

    private static bool HasAddress(MessageRecipient recipient) =>
        !string.IsNullOrWhiteSpace(recipient.Address);

    private static int? GetInt(MailProfile profile, string key) {
        var value = GetSetting(profile, key);
        if (value == null) return null;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        throw new InvalidOperationException($"Profile '{profile.Id}' has invalid integer setting '{key}'.");
    }

    private static double? GetDouble(MailProfile profile, string key) {
        var value = GetSetting(profile, key);
        if (value == null) return null;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        throw new InvalidOperationException($"Profile '{profile.Id}' has invalid numeric setting '{key}'.");
    }

    private static bool? GetBool(MailProfile profile, string key) {
        var value = GetSetting(profile, key);
        if (value == null) return null;
        if (bool.TryParse(value, out var parsed)) return parsed;
        throw new InvalidOperationException($"Profile '{profile.Id}' has invalid boolean setting '{key}'.");
    }

    private static int RequireNonNegative(MailProfile profile, string key, int value) {
        if (value < 0) {
            throw new InvalidOperationException($"Profile '{profile.Id}' setting '{key}' must not be negative.");
        }
        return value;
    }
}
