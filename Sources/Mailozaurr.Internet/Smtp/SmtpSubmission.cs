namespace Mailozaurr;

/// <summary>Identifies a submission whose final SMTP acknowledgement was not received.</summary>
internal sealed class SmtpDeliveryUnknownException(Exception innerException)
    : IOException("SMTP submission lost confirmation; do not resubmit without reconciling delivery.", innerException);

internal static class SmtpSubmission {
    internal static async Task<MailKit.SendResult> SendAsync(ClientSmtp client, MimeMessage message, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        try {
            return await client.SendAsync(client.CreateSendRequest(message), cancellationToken).ConfigureAwait(false);
        } catch (Exception exception) when (exception is IOException || exception is System.Net.Sockets.SocketException ||
            exception is TimeoutException || exception is SmtpProtocolException || exception is OperationCanceledException) {
            throw new SmtpDeliveryUnknownException(exception);
        }
    }
}
