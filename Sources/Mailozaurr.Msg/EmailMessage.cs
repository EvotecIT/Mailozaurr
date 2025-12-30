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
            try {
                if (File.Exists(msgFile.FullName) && !force) {
                    LoggingMessages.Logger.WriteWarning("MSG file already exists: {0}", msgFile);
                    return new EmlConversionResult() { EmlFile = emlFile.FullName, MsgFile = msgFile.FullName, Status = false, Error = "MSG file already exists" };
                } else {
                    if (File.Exists(msgFile.FullName)) {
                        LoggingMessages.Logger.WriteVerbose("Removing existing MSG file: {0}", msgFile);
                        File.Delete(msgFile.FullName);
                    }
                    Converter.ConvertEmlToMsg(emlFile.FullName, msgFile.FullName);
                    return new EmlConversionResult() { EmlFile = emlFile.FullName, MsgFile = msgFile.FullName, Status = true };
                }
            } catch (IOException ex) {
                LoggingMessages.Logger.WriteWarning("Error converting EML to MSG: {0}", ex.Message);
                return new EmlConversionResult() { EmlFile = emlFile.FullName, MsgFile = msgFile.FullName, Status = false, Error = ex.Message };
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
            try {
                if (File.Exists(emlFile.FullName) && !force) {
                    LoggingMessages.Logger.WriteWarning("EML file already exists: {0}", emlFile);
                    return new MsgConversionResult() { MsgFile = msgFile.FullName, EmlFile = emlFile.FullName, Status = false, Error = "EML file already exists" };
                } else {
                    if (File.Exists(emlFile.FullName)) {
                        LoggingMessages.Logger.WriteVerbose("Removing existing EML file: {0}", emlFile);
                        File.Delete(emlFile.FullName);
                    }
                    Converter.ConvertMsgToEml(msgFile.FullName, emlFile.FullName);
                    return new MsgConversionResult() { MsgFile = msgFile.FullName, EmlFile = emlFile.FullName, Status = true };
                }
            } catch (IOException ex) {
                LoggingMessages.Logger.WriteWarning("Error converting MSG to EML: {0}", ex.Message);
                return new MsgConversionResult() { MsgFile = msgFile.FullName, EmlFile = emlFile.FullName, Status = false, Error = ex.Message };
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
}