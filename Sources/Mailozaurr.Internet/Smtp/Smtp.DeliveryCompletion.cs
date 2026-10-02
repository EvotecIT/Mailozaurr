namespace Mailozaurr;

public partial class Smtp {
    private async Task<SmtpResult> CompleteAcceptedSendAsync(SmtpResult result, CancellationToken cancellationToken) {
        await RecordPostSendErrorAsync(result, "Sent-log persistence", () => SaveSentMessageAsync(result.MessageId!, CancellationToken.None)).ConfigureAwait(false);
        await RecordPostSendErrorAsync(result, "Queue acknowledgement", () => RemoveAcceptedPendingMessageAsync(result.MessageId!)).ConfigureAwait(false);
        await RecordPostSendErrorAsync(result, "Webhook notification", () => Helpers.PostWebhookAsync(WebhookUrl, result,
            MailozaurrJsonContext.Default.SmtpResult, cancellationToken, throwOnFailure: true)).ConfigureAwait(false);
        return result;
    }

    private async Task RemoveAcceptedPendingMessageAsync(string messageId) {
        if (PendingMessageRepository == null) return;
        var existing = await PendingMessageRepository.GetByMessageIdAsync(messageId, CancellationToken.None).ConfigureAwait(false);
        if (existing != null) {
            var accepted = existing.Clone();
            accepted.DeliveryAcceptedAt = DateTimeOffset.UtcNow;
            accepted.NextAttemptAt = accepted.DeliveryAcceptedAt.Value;
            try {
                await PendingMessageRepository.SaveAsync(accepted, CancellationToken.None).ConfigureAwait(false);
            } catch (Exception markerFailure) {
                try {
                    await PendingMessageRepository.RemoveAsync(messageId, CancellationToken.None).ConfigureAwait(false);
                    return;
                } catch (Exception removalFailure) {
                    throw new AggregateException("Delivery was accepted, but queue acknowledgement failed.", markerFailure, removalFailure);
                }
            }
        }
        await RemovePendingMessageAsync(messageId, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task RecordPostSendErrorAsync(SmtpResult result, string stage, Func<Task> action) {
        try {
            await action().ConfigureAwait(false);
        } catch (Exception exception) {
            var error = $"{stage}: {exception.Message}";
            result.PostSendErrors.Add(error);
            LogWarning($"Send-EmailMessage - {error}");
        }
    }
}
