using MsgKit;
using System.IO;

namespace Mailozaurr;

/// <summary>
/// Provides helper methods for converting EML messages to MSG format.
/// </summary>
public static class EmailMessage {
    /// <summary>
    /// Converts one or more EML files to MSG format.
    /// </summary>
    /// <param name="emlFile">Paths to the EML files to convert.</param>
    /// <param name="outputFolder">The folder where MSG files should be saved.</param>
    /// <param name="force">If set to <c>true</c>, existing MSG files will be overwritten.</param>
    /// <returns>A collection of conversion results for each processed file.</returns>
    public static IEnumerable<EmlConversionResult> ConvertEmlToMsg(string[] emlFile, string outputFolder, bool force) {
        LoggingMessages.Logger.WriteVerbose($"Converting {emlFile.Length} EML file(s) to MSG file(s)...");
        return ConvertFiles(emlFile, outputFolder, ".msg", ConvertEmlToMsg, force);
    }

    /// <summary>
    /// Converts a single EML file to MSG format.
    /// </summary>
    /// <param name="emlFile">The input EML file.</param>
    /// <param name="msgFile">The target MSG file.</param>
    /// <param name="force">If set to <c>true</c>, an existing MSG file will be overwritten.</param>
    /// <returns>The result of the conversion.</returns>
    public static EmlConversionResult ConvertEmlToMsg(FileInfo emlFile, FileInfo msgFile, bool force) {
        if (File.Exists(emlFile.FullName)) {
            LoggingMessages.Logger.WriteVerbose("Processing EML file: {0}", emlFile);
            var dir = msgFile.DirectoryName;
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }
            var msgFilePath = msgFile.FullName;
            var tempFile = CreateTempOutputPath(msgFile);
            try {
                Converter.ConvertEmlToMsg(emlFile.FullName, tempFile);
                if (TryFinalizeConvertedFile(tempFile, msgFilePath, force, "MSG file already exists", out var finalizeError)) {
                    return new EmlConversionResult() { EmlFile = emlFile.FullName, MsgFile = msgFilePath, Status = true };
                }

                return new EmlConversionResult() { EmlFile = emlFile.FullName, MsgFile = msgFilePath, Status = false, Error = finalizeError };
            } catch (IOException ex) {
                LoggingMessages.Logger.WriteWarning("Error converting EML to MSG: {0}", ex.Message);
                return new EmlConversionResult() { EmlFile = emlFile.FullName, MsgFile = msgFilePath, Status = false, Error = ex.Message };
            } finally {
                SafeDelete(tempFile);
            }
        }
        return new EmlConversionResult() { EmlFile = emlFile.FullName, MsgFile = msgFile.FullName, Status = false, Error = "EML file does not exist" };
    }

    /// <summary>
    /// Converts one or more MSG files to EML format.
    /// </summary>
    /// <param name="msgFile">Paths to the MSG files to convert.</param>
    /// <param name="outputFolder">The folder where EML files should be saved.</param>
    /// <param name="force">If set to <c>true</c>, existing EML files will be overwritten.</param>
    /// <returns>A collection of conversion results for each processed file.</returns>
    public static IEnumerable<MsgConversionResult> ConvertMsgToEml(string[] msgFile, string outputFolder, bool force) {
        LoggingMessages.Logger.WriteVerbose($"Converting {msgFile.Length} MSG file(s) to EML file(s)...");
        return ConvertFiles(msgFile, outputFolder, ".eml", ConvertMsgToEml, force);
    }

    /// <summary>
    /// Converts a single MSG file to EML format.
    /// </summary>
    /// <param name="msgFile">The input MSG file.</param>
    /// <param name="emlFile">The target EML file.</param>
    /// <param name="force">If set to <c>true</c>, an existing EML file will be overwritten.</param>
    /// <returns>The result of the conversion.</returns>
    public static MsgConversionResult ConvertMsgToEml(FileInfo msgFile, FileInfo emlFile, bool force) {
        if (File.Exists(msgFile.FullName)) {
            LoggingMessages.Logger.WriteVerbose("Processing MSG file: {0}", msgFile);
            var dir = emlFile.DirectoryName;
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }
            var emlFilePath = emlFile.FullName;
            var tempFile = CreateTempOutputPath(emlFile);
            try {
                Converter.ConvertMsgToEml(msgFile.FullName, tempFile);
                if (TryFinalizeConvertedFile(tempFile, emlFilePath, force, "EML file already exists", out var finalizeError)) {
                    return new MsgConversionResult() { MsgFile = msgFile.FullName, EmlFile = emlFilePath, Status = true };
                }

                return new MsgConversionResult() { MsgFile = msgFile.FullName, EmlFile = emlFilePath, Status = false, Error = finalizeError };
            } catch (IOException ex) {
                LoggingMessages.Logger.WriteWarning("Error converting MSG to EML: {0}", ex.Message);
                return new MsgConversionResult() { MsgFile = msgFile.FullName, EmlFile = emlFilePath, Status = false, Error = ex.Message };
            } finally {
                SafeDelete(tempFile);
            }
        }
        return new MsgConversionResult() { MsgFile = msgFile.FullName, EmlFile = emlFile.FullName, Status = false, Error = "MSG file does not exist" };
    }

    private static IEnumerable<TResult> ConvertFiles<TResult>(string[] inputFiles, string outputFolder, string targetExtension, Func<FileInfo, FileInfo, bool, TResult> converter, bool force) {
        if (!Directory.Exists(outputFolder)) {
            Directory.CreateDirectory(outputFolder);
        }
        foreach (var file in inputFiles) {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var targetFile = Path.Combine(outputFolder, $"{fileName}{targetExtension}");
            yield return converter(new FileInfo(file), new FileInfo(targetFile), force);
        }
    }

    private static string CreateTempOutputPath(FileInfo targetFile) {
        var directory = targetFile.DirectoryName ?? Path.GetTempPath();
        var baseName = Path.GetFileNameWithoutExtension(targetFile.Name);
        var extension = targetFile.Extension;
        var tempName = $"{baseName}.{Guid.NewGuid():N}{extension}.tmp";
        return Path.Combine(directory, tempName);
    }

    private static bool TryFinalizeConvertedFile(string tempFile, string targetFile, bool force, string existingFileError, out string? error) {
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
            // Fall through to delete + move.
        } catch (IOException) {
            // Fall through to delete + move.
        } catch (UnauthorizedAccessException) {
            // Fall through to delete + move.
        }

        try {
            if (File.Exists(destinationFile)) {
                File.Delete(destinationFile);
            }
            File.Move(sourceFile, destinationFile);
            return true;
        } catch (Exception ex) {
            error = ex.Message;
            return false;
        }
    }

    private static void SafeDelete(string path) {
        try {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) {
                File.Delete(path);
            }
        } catch (IOException) {
            // Best effort cleanup only.
        } catch (UnauthorizedAccessException) {
            // Best effort cleanup only.
        }
    }
}
