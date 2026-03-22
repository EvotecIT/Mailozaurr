using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MimeKit;

namespace Mailozaurr;

/// <summary>
/// Sends MIME messages through Microsoft Graph draft and attachment APIs.
/// </summary>
public static class GraphMimeMessageSender {
    /// <summary>
    /// Sends a MIME message by creating a Graph draft, uploading large attachments, and sending the draft.
    /// </summary>
    public static async Task<GraphMessage> SendAsync(
        GraphApiClient client,
        string userId,
        MimeMessage message,
        CancellationToken cancellationToken = default) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }

        var prepared = GraphMimePreparation.PrepareMessage(message);
        try {
            var created = await client.CreateMessageAsync(
                prepared.Message,
                userId: userId,
                folderIdOrWellKnownName: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            await UploadAttachmentsAsync(client, userId, created, prepared.UploadAttachments, cancellationToken).ConfigureAwait(false);
            await client.SendDraftMessageAsync(created.Id!, userId, cancellationToken).ConfigureAwait(false);
            return created;
        } finally {
            foreach (var uploadAttachment in prepared.UploadAttachments) {
                uploadAttachment.Dispose();
            }
        }
    }

    private static async Task UploadAttachmentsAsync(
        GraphApiClient client,
        string userId,
        GraphMessage draft,
        IReadOnlyList<DecodedMimeAttachment> attachments,
        CancellationToken cancellationToken) {
        if (attachments.Count == 0 || string.IsNullOrWhiteSpace(draft.Id)) {
            return;
        }

        foreach (var attachment in attachments) {
            var uploadSession = await client.CreateAttachmentUploadSessionAsync(
                draft.Id!,
                new GraphAttachmentItem("file", attachment.Name, attachment.Length) {
                    ContentType = attachment.ContentType,
                    IsInline = attachment.IsInline,
                    ContentId = attachment.ContentId
                },
                userId: userId,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            await UploadAttachmentAsync(client, uploadSession, attachment, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task UploadAttachmentAsync(
        GraphApiClient client,
        GraphUploadSessionResult uploadSession,
        DecodedMimeAttachment attachment,
        CancellationToken cancellationToken) {
        const int chunkSize = Graph.MaxChunkSize;
        using var stream = attachment.OpenRead();
        var buffer = new byte[chunkSize];
        long position = 0;

        while (true) {
            var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            if (read <= 0) {
                break;
            }

            var chunk = new byte[read];
            Buffer.BlockCopy(buffer, 0, chunk, 0, read);
            var startInclusive = position;
            var endInclusive = position + read - 1L;

            await client.UploadAttachmentChunkAsync(
                uploadSession.UploadUrl!,
                chunk,
                startInclusive,
                endInclusive,
                attachment.Length,
                cancellationToken).ConfigureAwait(false);

            position += read;
        }
    }
}
