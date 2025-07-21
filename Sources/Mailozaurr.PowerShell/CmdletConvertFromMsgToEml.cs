using System.IO;
using MsgReader.Outlook.Storage;
using MimeKit;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Converts MSG files to EML format for interoperability with other clients.</para>
/// <para type="description">The <c>ConvertFrom-MsgToEml</c> cmdlet converts one or more MSG files to EML format. Provide input MSG file paths and the destination folder. Existing files can be overwritten with the <c>-Force</c> switch.</para>
/// <example>
///   <summary>Convert multiple MSG files</summary>
///   <code>ConvertFrom-MsgToEml -InputPath "C:\Mail\mail1.msg","C:\Mail\mail2.msg" -OutputFolder "C:\Converted"</code>
/// </example>
/// <remarks>
/// Use this cmdlet to archive or migrate Outlook messages to the portable EML format.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsData.ConvertFrom, "MsgToEml")]
public sealed class CmdletConvertFromMsgToEml : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">Paths to the MSG files to convert.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string[]? InputPath;

    /// <summary>
    /// <para type="description">Destination folder for the converted EML files.</para>
    /// </summary>
    [Alias("OutputPath")]
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string? OutputFolder { get; set; }

    /// <summary>
    /// <para type="description">Overwrite existing files.</para>
    /// </summary>
    [Parameter(Mandatory = false, Position = 2)]
    public SwitchParameter Force { get; set; }

    /// <summary>
    /// Initializes logging for the conversion process.
    /// </summary>
    protected override Task BeginProcessingAsync() {
        var internalLogger = new InternalLogger();
        var internalLoggerPowerShell = new InternalLoggerPowerShell(internalLogger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);
        LoggingMessages.Logger = internalLogger;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Converts the specified MSG files to EML format.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        if (InputPath == null || string.IsNullOrEmpty(OutputFolder)) {
            return Task.CompletedTask;
        }

        foreach (var msgPath in InputPath) {
            var fileName = Path.GetFileNameWithoutExtension(msgPath);
            var targetFile = Path.Combine(OutputFolder, $"{fileName}.eml");

            try {
                if (File.Exists(targetFile)) {
                    if (!Force) {
                        WriteObject(new MsgConversionResult {
                            MsgFile = msgPath,
                            EmlFile = targetFile,
                            Status = false,
                            Error = "EML file already exists"
                        });
                        continue;
                    }
                    File.Delete(targetFile);
                }

                using (var message = new Message(msgPath)) {
                    using var stream = File.Create(targetFile);
                    var mime = message.ToMimeMessage();
                    mime.WriteTo(stream);
                }

                WriteObject(new MsgConversionResult {
                    MsgFile = msgPath,
                    EmlFile = targetFile,
                    Status = true
                });
            } catch (IOException ex) {
                WriteObject(new MsgConversionResult {
                    MsgFile = msgPath,
                    EmlFile = targetFile,
                    Status = false,
                    Error = ex.Message
                });
            }
        }

        return Task.CompletedTask;
    }
}
