using MimeKit;
using OfficeIMO.Email;

namespace Mailozaurr;

/// <summary>Bridges OfficeIMO email artifacts to Mailozaurr's MimeKit transport and security owner.</summary>
public static class MailFileMimeAdapter {
    /// <summary>Creates a MimeKit message from an OfficeIMO email or Outlook message document.</summary>
    public static MimeMessage ToMimeMessage(EmailDocument document) {
        if (document == null) throw new ArgumentNullException(nameof(document));
        using var stream = new MemoryStream();
        new EmailDocumentWriter().Write(document, stream, EmailFileFormat.Eml);
        stream.Position = 0;
        return MimeMessage.Load(stream);
    }

    /// <summary>Asynchronously creates a MimeKit message from an OfficeIMO document.</summary>
    public static async Task<MimeMessage> ToMimeMessageAsync(EmailDocument document,
        CancellationToken cancellationToken = default) {
        if (document == null) throw new ArgumentNullException(nameof(document));
        using var stream = new MemoryStream();
        await new EmailDocumentWriter().WriteAsync(document, stream, EmailFileFormat.Eml, cancellationToken)
            .ConfigureAwait(false);
        stream.Position = 0;
        return await MimeMessage.LoadAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Attempts to parse a retained protected MSG payload as a MimeKit entity.</summary>
    public static bool TryGetProtectedMimeEntity(EmailDocument document, out MimeEntity? entity) {
        if (document == null) throw new ArgumentNullException(nameof(document));
        entity = null;
        EmailAttachment? payload = document.Protection.PayloadAttachment;
        string? contentTypeValue = payload?.ContentType;
        if (payload?.Content == null || string.IsNullOrWhiteSpace(contentTypeValue)) return false;
        try {
            ContentType contentType = ContentType.Parse(contentTypeValue!);
            using var stream = new MemoryStream(payload.Content, writable: false);
            entity = MimeEntity.Load(contentType, stream);
            return true;
        } catch (FormatException) {
            return false;
        } catch (IOException) {
            return false;
        }
    }
}
