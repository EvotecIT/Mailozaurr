using MimeKit;
using OfficeIMO.Email;

namespace Mailozaurr;

/// <summary>Bridges OfficeIMO email artifacts to Mailozaurr's MimeKit transport and security owner.</summary>
public static class MailFileMimeAdapter {
    /// <summary>Projects an OfficeIMO document to MimeKit while retaining structured write diagnostics.</summary>
    public static MailFileMimeMessageConversionResult ConvertToMimeMessage(EmailDocument document,
        EmailWriterOptions? options = null) {
        if (document == null) throw new ArgumentNullException(nameof(document));
        using FileStream stream = MailFileMimeTemporaryStorage.Create(asynchronous: false);
        EmailWriteResult result = new EmailDocumentWriter(options ?? EmailWriterOptions.Default)
            .Write(document, stream, EmailFileFormat.Eml);
        if (result.HasErrors) return new MailFileMimeMessageConversionResult(null, result);
        stream.Position = 0;
        return new MailFileMimeMessageConversionResult(MimeMessage.Load(stream), result);
    }

    /// <summary>Asynchronously projects an OfficeIMO document to MimeKit while retaining write diagnostics.</summary>
    public static async Task<MailFileMimeMessageConversionResult> ConvertToMimeMessageAsync(
        EmailDocument document, EmailWriterOptions? options = null,
        CancellationToken cancellationToken = default) {
        if (document == null) throw new ArgumentNullException(nameof(document));
        using FileStream stream = MailFileMimeTemporaryStorage.Create(asynchronous: true);
        EmailWriteResult result = await new EmailDocumentWriter(options ?? EmailWriterOptions.Default)
            .WriteAsync(document, stream, EmailFileFormat.Eml, cancellationToken)
            .ConfigureAwait(false);
        if (result.HasErrors) return new MailFileMimeMessageConversionResult(null, result);
        stream.Position = 0;
        MimeMessage message = await MimeMessage.LoadAsync(stream, cancellationToken).ConfigureAwait(false);
        return new MailFileMimeMessageConversionResult(message, result);
    }

    /// <summary>Creates a MimeKit message from an OfficeIMO email or Outlook message document.</summary>
    public static MimeMessage ToMimeMessage(EmailDocument document) {
        MailFileMimeMessageConversionResult conversion = ConvertToMimeMessage(document);
        MailFileDiagnostics.ThrowIfErrors(
            conversion.Diagnostics, "The email document could not be projected to MIME");
        return conversion.Message ?? throw new InvalidDataException(
            "The email document did not produce a MIME message.");
    }

    /// <summary>Asynchronously creates a MimeKit message from an OfficeIMO document.</summary>
    public static async Task<MimeMessage> ToMimeMessageAsync(EmailDocument document,
        CancellationToken cancellationToken = default) {
        MailFileMimeMessageConversionResult conversion = await ConvertToMimeMessageAsync(
            document, cancellationToken: cancellationToken).ConfigureAwait(false);
        MailFileDiagnostics.ThrowIfErrors(
            conversion.Diagnostics, "The email document could not be projected to MIME");
        return conversion.Message ?? throw new InvalidDataException(
            "The email document did not produce a MIME message.");
    }

    /// <summary>Serializes a MimeKit message and parses it into an OfficeIMO document with structured diagnostics.</summary>
    /// <remarks>Dispose the result after using file-backed attachment content.</remarks>
    public static MailFileEmailDocumentConversionResult ConvertToEmailDocument(MimeMessage message,
        EmailReaderOptions? options = null, bool useFileBackedContent = true,
        CancellationToken cancellationToken = default) {
        if (message == null) throw new ArgumentNullException(nameof(message));
        EmailReaderOptions readerOptions = options ?? EmailReaderOptions.Default;
        using FileStream stream = MailFileMimeTemporaryStorage.Create(asynchronous: false);
        var bounded = new MailFileBoundedWriteStream(stream, readerOptions.MaxInputBytes);
        message.WriteTo(bounded, cancellationToken);
        bounded.Flush();
        stream.Position = 0;
        var reader = new EmailDocumentReader(readerOptions);
        EmailReadResult result = useFileBackedContent
            ? reader.ReadStreaming(stream, "message.eml", cancellationToken)
            : reader.Read(stream, "message.eml", cancellationToken);
        return new MailFileEmailDocumentConversionResult(result);
    }

    /// <summary>Asynchronously serializes a MimeKit message and parses it into an OfficeIMO document.</summary>
    /// <remarks>Dispose the result after using file-backed attachment content.</remarks>
    public static async Task<MailFileEmailDocumentConversionResult> ConvertToEmailDocumentAsync(
        MimeMessage message, EmailReaderOptions? options = null, bool useFileBackedContent = true,
        CancellationToken cancellationToken = default) {
        if (message == null) throw new ArgumentNullException(nameof(message));
        EmailReaderOptions readerOptions = options ?? EmailReaderOptions.Default;
        using FileStream stream = MailFileMimeTemporaryStorage.Create(asynchronous: true);
        var bounded = new MailFileBoundedWriteStream(stream, readerOptions.MaxInputBytes);
        await message.WriteToAsync(bounded, cancellationToken).ConfigureAwait(false);
        await bounded.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Position = 0;
        var reader = new EmailDocumentReader(readerOptions);
        EmailReadResult result = useFileBackedContent
            ? await reader.ReadStreamingAsync(stream, "message.eml", cancellationToken).ConfigureAwait(false)
            : await reader.ReadAsync(stream, "message.eml", cancellationToken).ConfigureAwait(false);
        return new MailFileEmailDocumentConversionResult(result);
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
        } catch (ParseException) {
            return false;
        } catch (FormatException) {
            return false;
        } catch (IOException) {
            return false;
        }
    }
}
