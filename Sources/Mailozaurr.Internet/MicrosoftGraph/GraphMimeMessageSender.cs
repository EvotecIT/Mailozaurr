using MimeKit;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
            if (attachment.Length < Graph.MinimumUploadSessionAttachmentSize) {
                await client.AddAttachmentAsync(
                    draft.Id!,
                    attachment.ToGraphAttachment(),
                    userId,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

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

    internal static async Task UploadAttachmentAsync(
        GraphApiClient client,
        GraphUploadSessionResult uploadSession,
        DecodedMimeAttachment attachment,
        CancellationToken cancellationToken) {
        // Keep Outlook upload requests below 4 MB, including providers using decimal MB.
        const int chunkSize = 3_932_160;
        if (string.IsNullOrWhiteSpace(uploadSession.UploadUrl)) throw new InvalidDataException("Graph upload session creation failed (empty uploadUrl).");
        using var stream = attachment.OpenRead();
        var buffer = new byte[chunkSize];
        long position = 0;

        while (position < attachment.Length) {
            var expected = (int)Math.Min(buffer.Length, attachment.Length - position);
            var read = 0;
            while (read < expected) {
                var count = await stream.ReadAsync(buffer, read, expected - read, cancellationToken).ConfigureAwait(false);
                if (count == 0) throw new EndOfStreamException("Attachment ended before its declared length.");
                read += count;
            }
            var startInclusive = position;
            var endInclusive = position + read - 1L;

            await client.UploadAttachmentChunkAsync(
                uploadSession.UploadUrl!,
                buffer, 0, read,
                startInclusive,
                endInclusive,
                attachment.Length,
                cancellationToken).ConfigureAwait(false);

            position += read;
        }
    }
}
