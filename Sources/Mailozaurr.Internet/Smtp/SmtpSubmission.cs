namespace Mailozaurr;

internal sealed class SmtpSubmissionRequest(MimeMessage message) : SmtpSendRequest(message) {
    internal MailKit.SendResult? CompletedResult { get; private set; }
    public override void OnCompleted(IMailTransport transport, MailKit.SendResult result) => CompletedResult = result;
}

internal static class SmtpSubmission {
    internal static async Task<MailKit.SendResult> SendAsync(ClientSmtp client, MimeMessage message, CancellationToken cancellationToken, List<string> postSendErrors) {
        cancellationToken.ThrowIfCancellationRequested();
        var request = client.CreateSendRequest(message);
        try {
            return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        } catch (Exception exception) when (request.CompletedResult != null) {
            // MailKit calls OnCompleted before raising MessageSent. A failing
            // observer cannot undo the server's confirmed acceptance.
            postSendErrors.Add("SMTP completion notification: " + exception.Message);
            return request.CompletedResult;
        } catch (Exception exception) when (exception is IOException || exception is System.Net.Sockets.SocketException ||
            exception is TimeoutException || exception is SmtpProtocolException || exception is OperationCanceledException) {
            throw new SmtpDeliveryUnknownException(exception);
        }
    }
}
