using OfficeIMO.Email;

namespace Mailozaurr;

public sealed partial class MailFileMessage {
    /// <summary>Loads one MSG or EML file with the default Mailozaurr projection.</summary>
    public static MailFileMessage Load(string filePath, MailFileReaderOptions? options = null) =>
        GetMessageOrThrow(MailFileReader.Read(filePath, options));

    /// <summary>Loads one MSG or EML file from a file descriptor.</summary>
    public static MailFileMessage Load(FileInfo fileInfo, MailFileReaderOptions? options = null) =>
        GetMessageOrThrow(MailFileReader.Read(fileInfo, options));

    /// <summary>Asynchronously loads one MSG or EML file with the default Mailozaurr projection.</summary>
    public static async Task<MailFileMessage> LoadAsync(string filePath, MailFileReaderOptions? options = null,
        CancellationToken cancellationToken = default) {
        MailFileMessage message = await MailFileReader.ReadAsync(filePath, options, cancellationToken)
            .ConfigureAwait(false);
        return GetMessageOrThrow(message);
    }

    /// <summary>Asynchronously loads one MSG or EML file from a file descriptor.</summary>
    public static async Task<MailFileMessage> LoadAsync(FileInfo fileInfo, MailFileReaderOptions? options = null,
        CancellationToken cancellationToken = default) {
        MailFileMessage message = await MailFileReader.ReadAsync(fileInfo, options, cancellationToken)
            .ConfigureAwait(false);
        return GetMessageOrThrow(message);
    }

    /// <summary>Saves the message as EML, MSG, or TNEF, inferred from the destination filename.</summary>
    public EmailWriteResult Save(string filePath, EmailWriterOptions? options = null) =>
        OfficeDocument.Save(filePath, options);

    /// <summary>Saves the message in the explicitly selected artifact format.</summary>
    public EmailWriteResult Save(string filePath, EmailFileFormat format, EmailWriterOptions? options = null) =>
        OfficeDocument.Save(filePath, format, options);

    /// <summary>Asynchronously saves the message, inferring the format from the destination filename.</summary>
    public Task<EmailWriteResult> SaveAsync(string filePath, EmailWriterOptions? options = null,
        CancellationToken cancellationToken = default) =>
        OfficeDocument.SaveAsync(filePath, options, cancellationToken);

    /// <summary>Asynchronously saves the message in the explicitly selected artifact format.</summary>
    public Task<EmailWriteResult> SaveAsync(string filePath, EmailFileFormat format,
        EmailWriterOptions? options = null, CancellationToken cancellationToken = default) =>
        OfficeDocument.SaveAsync(filePath, format, options, cancellationToken);

    private static MailFileMessage GetMessageOrThrow(MailFileMessage message) {
        MailFileDiagnostics.ThrowIfErrors(message.Diagnostics, "The mail file could not be loaded");
        return message;
    }
}
