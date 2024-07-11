namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Converts EML files to MSG format.</para>
/// <para type="description">This cmdlet converts EML files to MSG format. It takes an array of input paths and an output folder as parameters. The cmdlet will process each EML file and save the converted MSG file in the specified output folder.</para>
/// <example>
/// <para>Convert EML files to MSG format</para>
/// <code>ConvertFrom-EmlToMsg -InputPath "path\to\file1.eml","path\to\file2.eml" -OutputFolder "path\to\output"</code>
/// </example>
/// </summary>
[Cmdlet(VerbsData.ConvertFrom, "EmlToMsg")]
public sealed class CmdletConvertFromEmlToMsg : AsyncPSCmdlet {

    /// <summary>
    /// <para type="description">Specifies the paths to the EML files to convert. This parameter accepts an array of strings and is mandatory.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string[] InputPath;

    /// <summary>
    /// <para type="description">Specifies the folder where the converted MSG files will be saved. This parameter is mandatory.</para>
    /// </summary>
    [Alias("OutputPath")]
    [Parameter(Mandatory = true, Position = 1)]
    public string OutputFolder { get; set; }

    /// <summary>
    /// <para type="description">If this parameter is set, the cmdlet will overwrite existing MSG files without prompting.</para>
    /// </summary>
    [Parameter(Mandatory = false, Position = 2)]
    public SwitchParameter Force { get; set; }

    protected override Task BeginProcessingAsync() {
        // Initialize the logger to be able to see verbose, warning, debug, error, progress, and information messages.
        var internalLogger = new InternalLogger();
        var internalLoggerPowerShell = new InternalLoggerPowerShell(internalLogger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);
        LoggingMessages.Logger = internalLogger;
        return Task.CompletedTask;
    }
    protected override Task ProcessRecordAsync() {
        var outputMessage = EmailMessage.ConvertEmlToMsg(InputPath, OutputFolder, Force);
        foreach (var obj in outputMessage) {
            WriteObject(obj);
        }
        return Task.CompletedTask;
    }
}