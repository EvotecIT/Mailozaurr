using MimeKit;
using OfficeIMO.Email;

namespace Mailozaurr;

/// <summary>Reads MSG and EML files into compatibility fields and their rich owner models.</summary>
public static class MailFileReader {
    /// <summary>Reads a mail file from a path.</summary>
    public static MailFileMessage Read(string path, MailFileReaderOptions? options = null) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(path));
        }
        return Read(new FileInfo(path), options);
    }

    /// <summary>Reads a mail file from a file descriptor.</summary>
    public static MailFileMessage Read(FileInfo fileInfo, MailFileReaderOptions? options = null) {
        ValidateFile(fileInfo);
        options ??= new MailFileReaderOptions();
        MailFileFormat format = ResolveFormat(fileInfo);
        EmailReaderOptions officeOptions = ResolveOfficeOptions(options);
        EmailReadResult result = new EmailDocumentReader(officeOptions).Read(fileInfo.FullName);
        EnsureExpectedFormat(result.Document, format, fileInfo.FullName);

        MimeMessage? mimeMessage = options.VerifySignature && format == MailFileFormat.Eml
            ? LoadMimeMessage(fileInfo.FullName, options.MimeParserOptions)
            : null;
        return Project(fileInfo.FullName, format, result, mimeMessage, options);
    }

    /// <summary>Asynchronously reads a mail file from a path.</summary>
    public static Task<MailFileMessage> ReadAsync(string path, MailFileReaderOptions? options = null,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(path));
        }
        return ReadAsync(new FileInfo(path), options, cancellationToken);
    }

    /// <summary>Asynchronously reads a mail file from a file descriptor.</summary>
    public static async Task<MailFileMessage> ReadAsync(FileInfo fileInfo, MailFileReaderOptions? options = null,
        CancellationToken cancellationToken = default) {
        ValidateFile(fileInfo);
        options ??= new MailFileReaderOptions();
        MailFileFormat format = ResolveFormat(fileInfo);
        EmailReaderOptions officeOptions = ResolveOfficeOptions(options);
        EmailReadResult result = await new EmailDocumentReader(officeOptions)
            .ReadAsync(fileInfo.FullName, cancellationToken).ConfigureAwait(false);
        EnsureExpectedFormat(result.Document, format, fileInfo.FullName);

        MimeMessage? mimeMessage = options.VerifySignature && format == MailFileFormat.Eml
            ? await LoadMimeMessageAsync(fileInfo.FullName, options.MimeParserOptions, cancellationToken)
                .ConfigureAwait(false)
            : null;
        return Project(fileInfo.FullName, format, result, mimeMessage, options);
    }

    /// <summary>Attempts to read a mail file and returns an error message on failure.</summary>
    public static bool TryRead(string path, out MailFileMessage? message, out string? error,
        MailFileReaderOptions? options = null) {
        message = null;
        error = null;
        if (string.IsNullOrWhiteSpace(path)) {
            error = "File path is empty.";
            return false;
        }
        if (!File.Exists(path)) {
            error = $"File {path} doesn't exist.";
            return false;
        }
        try {
            MailFileMessage candidate = Read(path, options);
            if (MailFileDiagnostics.TryGetError(
                candidate.Diagnostics,
                "The mail file could not be read",
                out error)) {
                return false;
            }
            message = candidate;
            return true;
        } catch (NotSupportedException) {
            error = $"File {path} is not a .msg or .eml file.";
            return false;
        } catch (Exception ex) {
            error = $"File {path} is not a .msg or .eml file or another error occurred. Error: {ex.Message}";
            return false;
        }
    }

    private static MailFileMessage Project(string path, MailFileFormat format, EmailReadResult result,
        MimeMessage? mimeMessage, MailFileReaderOptions options) {
        EmailDocument document = result.Document;
        MailFileSignatureInfo signature = options.VerifySignature
            ? MailFileSignatureProjection.Evaluate(document, mimeMessage)
            : default;
        return new MailFileMessage(path, format, document, result.Diagnostics, signature, options);
    }

    private static EmailReaderOptions ResolveOfficeOptions(MailFileReaderOptions options) =>
        options.OfficeReaderOptions ?? new EmailReaderOptions(
            includeAttachmentContent: options.IncludeAttachments && options.IncludeAttachmentContent);

    private static MimeMessage LoadMimeMessage(string path, ParserOptions? options) => options == null
        ? MimeMessage.Load(path)
        : MimeMessage.Load(options, path);

    private static Task<MimeMessage> LoadMimeMessageAsync(string path, ParserOptions? options,
        CancellationToken cancellationToken) => options == null
        ? MimeMessage.LoadAsync(path, cancellationToken)
        : MimeMessage.LoadAsync(options, path, cancellationToken);

    private static void ValidateFile(FileInfo fileInfo) {
        if (fileInfo == null) throw new ArgumentNullException(nameof(fileInfo));
        if (!File.Exists(fileInfo.FullName)) throw new FileNotFoundException("Mail file not found.", fileInfo.FullName);
    }

    private static MailFileFormat ResolveFormat(FileInfo fileInfo) {
        if (fileInfo.Extension.Equals(".msg", StringComparison.OrdinalIgnoreCase)) return MailFileFormat.Msg;
        if (fileInfo.Extension.Equals(".eml", StringComparison.OrdinalIgnoreCase)) return MailFileFormat.Eml;
        throw new NotSupportedException($"Unsupported mail file extension '{fileInfo.Extension}'.");
    }

    private static void EnsureExpectedFormat(EmailDocument document, MailFileFormat format, string path) {
        bool valid = format == MailFileFormat.Msg
            ? document.Format == EmailFileFormat.OutlookMsg
            : document.Format == EmailFileFormat.Eml;
        if (!valid) throw new FormatException($"File '{path}' content does not match its {format} extension.");
    }
}
