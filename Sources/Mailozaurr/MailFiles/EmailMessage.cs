using MimeKit;
using OfficeIMO.Email;

namespace Mailozaurr;

/// <summary>Converts EML and Outlook MSG artifacts through OfficeIMO.Email and MimeKit.</summary>
public static class EmailMessage {
    /// <summary>Converts one or more EML files to MSG format.</summary>
    public static IEnumerable<EmlConversionResult> ConvertEmlToMsg(string[] emlFile, string outputFolder,
        bool force) {
        if (emlFile == null) throw new ArgumentNullException(nameof(emlFile));
        LoggingMessages.Logger.WriteVerbose($"Converting {emlFile.Length} EML file(s) to MSG file(s)...");
        return ConvertFiles(emlFile, outputFolder, ".msg", ConvertEmlToMsg, force);
    }

    /// <summary>Converts one EML file to MSG format.</summary>
    public static EmlConversionResult ConvertEmlToMsg(FileInfo emlFile, FileInfo msgFile, bool force) {
        if (!emlFile.Exists) return MissingEml(emlFile, msgFile);
        LoggingMessages.Logger.WriteVerbose("Processing EML file: {0}", emlFile);
        EnsureOutputDirectory(msgFile);
        string tempFile = CreateTempOutputPath(msgFile);
        try {
            MailFileMessage source = MailFileReader.Read(emlFile, RichReadOptions());
            new EmailDocumentWriter().Write(source.OfficeDocument, tempFile, EmailFileFormat.OutlookMsg);
            return TryFinalizeConvertedFile(tempFile, msgFile.FullName, force, "MSG file already exists",
                out string? error)
                ? new EmlConversionResult { EmlFile = emlFile.FullName, MsgFile = msgFile.FullName, Status = true }
                : new EmlConversionResult { EmlFile = emlFile.FullName, MsgFile = msgFile.FullName, Error = error };
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning("Error converting EML to MSG: {0}", ex.Message);
            return new EmlConversionResult {
                EmlFile = emlFile.FullName,
                MsgFile = msgFile.FullName,
                Error = ex.Message
            };
        } finally {
            SafeDelete(tempFile);
        }
    }

    /// <summary>Asynchronously converts one EML file to MSG format.</summary>
    public static async Task<EmlConversionResult> ConvertEmlToMsgAsync(FileInfo emlFile, FileInfo msgFile,
        bool force, CancellationToken cancellationToken = default) {
        if (!emlFile.Exists) return MissingEml(emlFile, msgFile);
        LoggingMessages.Logger.WriteVerbose("Processing EML file: {0}", emlFile);
        EnsureOutputDirectory(msgFile);
        string tempFile = CreateTempOutputPath(msgFile);
        try {
            MailFileMessage source = await MailFileReader.ReadAsync(emlFile, RichReadOptions(), cancellationToken)
                .ConfigureAwait(false);
            await new EmailDocumentWriter().WriteAsync(source.OfficeDocument, tempFile,
                EmailFileFormat.OutlookMsg, cancellationToken).ConfigureAwait(false);
            return TryFinalizeConvertedFile(tempFile, msgFile.FullName, force, "MSG file already exists",
                out string? error)
                ? new EmlConversionResult { EmlFile = emlFile.FullName, MsgFile = msgFile.FullName, Status = true }
                : new EmlConversionResult { EmlFile = emlFile.FullName, MsgFile = msgFile.FullName, Error = error };
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning("Error converting EML to MSG: {0}", ex.Message);
            return new EmlConversionResult {
                EmlFile = emlFile.FullName,
                MsgFile = msgFile.FullName,
                Error = ex.Message
            };
        } finally {
            SafeDelete(tempFile);
        }
    }

    /// <summary>Converts one or more MSG files to EML format.</summary>
    public static IEnumerable<MsgConversionResult> ConvertMsgToEml(string[] msgFile, string outputFolder,
        bool force) {
        if (msgFile == null) throw new ArgumentNullException(nameof(msgFile));
        LoggingMessages.Logger.WriteVerbose($"Converting {msgFile.Length} MSG file(s) to EML file(s)...");
        return ConvertFiles(msgFile, outputFolder, ".eml", ConvertMsgToEml, force);
    }

    /// <summary>Converts one MSG file to EML format.</summary>
    public static MsgConversionResult ConvertMsgToEml(FileInfo msgFile, FileInfo emlFile, bool force) {
        if (!msgFile.Exists) return MissingMsg(msgFile, emlFile);
        LoggingMessages.Logger.WriteVerbose("Processing MSG file: {0}", msgFile);
        EnsureOutputDirectory(emlFile);
        string tempFile = CreateTempOutputPath(emlFile);
        try {
            MailFileMessage source = MailFileReader.Read(msgFile, RichReadOptions());
            MimeMessage mimeMessage = source.ToMimeMessage();
            mimeMessage.WriteTo(tempFile);
            return TryFinalizeConvertedFile(tempFile, emlFile.FullName, force, "EML file already exists",
                out string? error)
                ? new MsgConversionResult { MsgFile = msgFile.FullName, EmlFile = emlFile.FullName, Status = true }
                : new MsgConversionResult { MsgFile = msgFile.FullName, EmlFile = emlFile.FullName, Error = error };
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning("Error converting MSG to EML: {0}", ex.Message);
            return new MsgConversionResult {
                MsgFile = msgFile.FullName,
                EmlFile = emlFile.FullName,
                Error = ex.Message
            };
        } finally {
            SafeDelete(tempFile);
        }
    }

    /// <summary>Asynchronously converts one MSG file to EML format.</summary>
    public static async Task<MsgConversionResult> ConvertMsgToEmlAsync(FileInfo msgFile, FileInfo emlFile,
        bool force, CancellationToken cancellationToken = default) {
        if (!msgFile.Exists) return MissingMsg(msgFile, emlFile);
        LoggingMessages.Logger.WriteVerbose("Processing MSG file: {0}", msgFile);
        EnsureOutputDirectory(emlFile);
        string tempFile = CreateTempOutputPath(emlFile);
        try {
            MailFileMessage source = await MailFileReader.ReadAsync(msgFile, RichReadOptions(), cancellationToken)
                .ConfigureAwait(false);
            MimeMessage mimeMessage = await source.OfficeDocument.ToMimeMessageAsync(cancellationToken)
                .ConfigureAwait(false);
            await mimeMessage.WriteToAsync(tempFile, cancellationToken).ConfigureAwait(false);
            return TryFinalizeConvertedFile(tempFile, emlFile.FullName, force, "EML file already exists",
                out string? error)
                ? new MsgConversionResult { MsgFile = msgFile.FullName, EmlFile = emlFile.FullName, Status = true }
                : new MsgConversionResult { MsgFile = msgFile.FullName, EmlFile = emlFile.FullName, Error = error };
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning("Error converting MSG to EML: {0}", ex.Message);
            return new MsgConversionResult {
                MsgFile = msgFile.FullName,
                EmlFile = emlFile.FullName,
                Error = ex.Message
            };
        } finally {
            SafeDelete(tempFile);
        }
    }

    private static MailFileReaderOptions RichReadOptions() => new MailFileReaderOptions {
        IncludeAttachments = true,
        IncludeAttachmentContent = true,
        IncludeHeaders = true
    };

    private static IEnumerable<TResult> ConvertFiles<TResult>(string[] inputFiles, string outputFolder,
        string targetExtension, Func<FileInfo, FileInfo, bool, TResult> converter, bool force) {
        if (inputFiles == null) throw new ArgumentNullException(nameof(inputFiles));
        if (string.IsNullOrWhiteSpace(outputFolder)) throw new ArgumentException("Output folder is required.", nameof(outputFolder));
        if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);
        foreach (string file in inputFiles) {
            string targetFile = Path.Combine(outputFolder,
                string.Concat(Path.GetFileNameWithoutExtension(file), targetExtension));
            yield return converter(new FileInfo(file), new FileInfo(targetFile), force);
        }
    }

    private static void EnsureOutputDirectory(FileInfo targetFile) {
        string? directory = targetFile.DirectoryName;
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
    }

    private static EmlConversionResult MissingEml(FileInfo source, FileInfo target) => new EmlConversionResult {
        EmlFile = source.FullName,
        MsgFile = target.FullName,
        Error = "EML file does not exist"
    };

    private static MsgConversionResult MissingMsg(FileInfo source, FileInfo target) => new MsgConversionResult {
        MsgFile = source.FullName,
        EmlFile = target.FullName,
        Error = "MSG file does not exist"
    };

    private static string CreateTempOutputPath(FileInfo targetFile) {
        string directory = targetFile.DirectoryName ?? Path.GetTempPath();
        return Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(targetFile.Name)}.{Guid.NewGuid():N}{targetFile.Extension}.tmp");
    }

    private static bool TryFinalizeConvertedFile(string tempFile, string targetFile, bool force,
        string existingFileError, out string? error) {
        error = null;
        if (!force) {
            try {
                File.Move(tempFile, targetFile);
                return true;
            } catch (IOException ex) {
                error = File.Exists(targetFile) ? existingFileError : ex.Message;
                return false;
            } catch (UnauthorizedAccessException ex) {
                error = File.Exists(targetFile) ? existingFileError : ex.Message;
                return false;
            }
        }
        return TryReplaceFile(tempFile, targetFile, out error);
    }

    private static bool TryReplaceFile(string sourceFile, string destinationFile, out string? error) {
        error = null;
        try {
            File.Replace(sourceFile, destinationFile, null);
            return true;
        } catch (PlatformNotSupportedException) {
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
        try {
            if (File.Exists(destinationFile)) File.Delete(destinationFile);
            File.Move(sourceFile, destinationFile);
            return true;
        } catch (Exception ex) {
            error = ex.Message;
            return false;
        }
    }

    private static void SafeDelete(string path) {
        try {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path);
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }

    private static Task<MimeMessage> ToMimeMessageAsync(this EmailDocument document,
        CancellationToken cancellationToken) => MailFileMimeAdapter.ToMimeMessageAsync(document, cancellationToken);
}
