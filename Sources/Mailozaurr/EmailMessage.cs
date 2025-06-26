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
        foreach (var eml in emlFile) {
            var fileName = Path.GetFileNameWithoutExtension(eml);
            var targetFile = Path.Combine(outputFolder, $"{fileName}.msg");
            var msg = ConvertEmlToMsg(new FileInfo(eml), new FileInfo(targetFile), force);
            yield return msg;
        }
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
}
