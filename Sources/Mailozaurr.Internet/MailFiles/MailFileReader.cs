using OfficeIMO.Email;

namespace Mailozaurr;

/// <summary>Reads EML, MSG, OFT, and TNEF files into compatibility fields and their rich owner models.</summary>
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
        try {
            EnsureExpectedFormat(result.Document, format, fileInfo.FullName);
            return Project(fileInfo.FullName, format, result, options, officeOptions);
        } catch {
            result.Dispose();
            throw;
        }
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
        try {
            EnsureExpectedFormat(result.Document, format, fileInfo.FullName);
            return Project(fileInfo.FullName, format, result, options, officeOptions);
        } catch {
            result.Dispose();
            throw;
        }
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
                candidate.Dispose();
                return false;
            }
            message = candidate;
            return true;
        } catch (NotSupportedException) {
            error = $"File {path} is not a supported EML, MSG, OFT, or TNEF file.";
            return false;
        } catch (Exception ex) {
            error = $"File {path} is not a supported mail file or another error occurred. Error: {ex.Message}";
            return false;
        }
    }

    private static MailFileMessage Project(string path, MailFileFormat format, EmailReadResult result,
        MailFileReaderOptions options, EmailReaderOptions officeOptions) {
        EmailDocument document = result.Document;
        MailFileSignatureInfo signature = options.VerifySignature
            ? MailFileSignatureProjection.Evaluate(document, ResolveSecurityProvider(options), officeOptions)
            : default;
        return new MailFileMessage(path, format, result, signature, options);
    }

    private static OfficeIMO.Security.IOfficeSecurityProvider ResolveSecurityProvider(
        MailFileReaderOptions options) => options.SecurityProvider ?? throw new InvalidOperationException(
        "Mail-file signature verification requires an explicit IOfficeSecurityProvider.");

    private static EmailReaderOptions ResolveOfficeOptions(MailFileReaderOptions options) =>
        options.OfficeReaderOptions ?? new EmailReaderOptions(
            includeAttachmentContent: options.IncludeAttachments && options.IncludeAttachmentContent);

    private static void ValidateFile(FileInfo fileInfo) {
        if (fileInfo == null) throw new ArgumentNullException(nameof(fileInfo));
        if (!File.Exists(fileInfo.FullName)) throw new FileNotFoundException("Mail file not found.", fileInfo.FullName);
    }

    private static MailFileFormat ResolveFormat(FileInfo fileInfo) {
        if (fileInfo.Extension.Equals(".msg", StringComparison.OrdinalIgnoreCase)) return MailFileFormat.Msg;
        if (fileInfo.Extension.Equals(".oft", StringComparison.OrdinalIgnoreCase)) return MailFileFormat.OutlookTemplate;
        if (fileInfo.Extension.Equals(".eml", StringComparison.OrdinalIgnoreCase)) return MailFileFormat.Eml;
        if (fileInfo.Extension.Equals(".tnef", StringComparison.OrdinalIgnoreCase) ||
            fileInfo.Extension.Equals(".dat", StringComparison.OrdinalIgnoreCase)) return MailFileFormat.Tnef;
        throw new NotSupportedException($"Unsupported mail file extension '{fileInfo.Extension}'.");
    }

    private static void EnsureExpectedFormat(EmailDocument document, MailFileFormat format, string path) {
        bool valid = format switch {
            MailFileFormat.Msg => document.Format == EmailFileFormat.OutlookMsg,
            MailFileFormat.Eml => document.Format == EmailFileFormat.Eml,
            MailFileFormat.OutlookTemplate => document.Format == EmailFileFormat.OutlookTemplate,
            MailFileFormat.Tnef => document.Format == EmailFileFormat.Tnef,
            _ => false
        };
        if (!valid) throw new FormatException($"File '{path}' content does not match its {format} extension.");
    }
}
