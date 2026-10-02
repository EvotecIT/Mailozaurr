namespace Mailozaurr;

/// <summary>Uploads decoded Graph attachments using the shared draft upload implementation.</summary>
public static class GraphLargeAttachmentUploader {
    /// <summary>Uploads decoded attachments to an existing draft message.</summary>
    /// <returns>Error text when Graph rejects the upload or its session response is invalid; otherwise null.</returns>
    public static async Task<string?> UploadAsync(HttpClient client, string accessToken, string messageId,
        IReadOnlyList<DecodedMimeAttachment> attachments, CancellationToken cancellationToken = default) {
        if (client == null) throw new ArgumentNullException(nameof(client));
        if (string.IsNullOrWhiteSpace(accessToken)) throw new ArgumentException("accessToken is required.", nameof(accessToken));
        if (string.IsNullOrWhiteSpace(messageId)) throw new ArgumentException("messageId is required.", nameof(messageId));
        if (attachments == null) throw new ArgumentNullException(nameof(attachments));
        using var graph = new GraphApiClient(client, new OAuthCredential { AccessToken = accessToken }, new Uri("https://graph.microsoft.com/v1.0/"));
        foreach (var attachment in attachments) {
            if (attachment == null) continue;
            cancellationToken.ThrowIfCancellationRequested();
            if (attachment.Length <= 0) return $"Graph upload failed: attachment '{attachment.Name}' has no content.";
            try {
                var session = await graph.CreateAttachmentUploadSessionAsync(messageId,
                    new GraphAttachmentItem("file", attachment.Name, attachment.Length) {
                        ContentType = attachment.ContentType, IsInline = attachment.IsInline, ContentId = attachment.ContentId
                    }, cancellationToken: cancellationToken).ConfigureAwait(false);
                await GraphMimeMessageSender.UploadAttachmentAsync(graph, session, attachment, cancellationToken).ConfigureAwait(false);
            } catch (GraphApiException exception) {
                return $"Graph upload failed (HTTP {(int)exception.StatusCode}) for '{attachment.Name}': {exception.Message}";
            } catch (InvalidDataException exception) {
                return $"Graph upload session creation failed: {exception.Message}";
            }
        }
        return null;
    }
}
