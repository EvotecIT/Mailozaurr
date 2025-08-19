namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Converts EML files to MSG format for compatibility with Microsoft Outlook and other clients.</para>
/// <para type="description">The <c>ConvertFrom-EmlToMsg</c> cmdlet converts one or more EML files to MSG format. Specify the input EML file paths and the output folder. The cmdlet processes each EML file and saves the converted MSG file in the specified output folder. Supports overwriting existing files with the <c>-Force</c> parameter.</para>
/// <example>
///   <summary>Convert multiple EML files to MSG format</summary>
///   <code>ConvertFrom-EmlToMsg -InputPath "C:\Mail\mail1.eml","C:\Mail\mail2.eml" -OutputFolder "C:\Converted"</code>
/// </example>
/// <example>
///   <summary>Convert EML files and overwrite existing MSG files</summary>
///   <code>ConvertFrom-EmlToMsg -InputPath "C:\Mail\*.eml" -OutputFolder "C:\Converted" -Force</code>
/// </example>
/// <remarks>
/// MSG format is commonly used by Microsoft Outlook. Use this cmdlet to migrate or archive EML messages for Outlook compatibility.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsData.ConvertFrom, "EmlToMsg")]
public sealed class CmdletConvertFromEmlToMsg : AsyncPSCmdlet {
    private InternalLogger? _logger;
    private InternalLoggerPowerShell? _listener;

    /// <summary>
    /// <para type="description">Specifies the paths to the EML files to convert. Accepts an array of strings. This parameter is mandatory.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string[]? InputPath;

    /// <summary>
    /// <para type="description">Specifies the folder where the converted MSG files will be saved. This parameter is mandatory.</para>
    /// </summary>
    [Alias("OutputPath")]
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string? OutputFolder { get; set; }

    /// <summary>
    /// <para type="description">If set, the cmdlet will overwrite existing MSG files without prompting.</para>
    /// </summary>
    [Parameter(Mandatory = false, Position = 2)]
    public SwitchParameter Force { get; set; }

    /// <summary>
    /// Initializes logging for the conversion process.
    /// </summary>
    protected override Task BeginProcessingAsync() {
        // Initialize the logger to be able to see verbose, warning, debug, error, progress, and information messages.
        _logger = new InternalLogger();
        _listener = new InternalLoggerPowerShell(_logger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);
        LoggingMessages.Logger = _logger;
        return Task.CompletedTask;
    }
    /// <summary>
    /// Converts the specified EML files to MSG format and writes the results to the output folder.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        var outputMessage = EmailMessage.ConvertEmlToMsg(InputPath!, OutputFolder!, Force);
        foreach (var obj in outputMessage) {
            WriteObject(obj);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Releases logging resources.
    /// </summary>
    protected override Task EndProcessingAsync() {
        _listener?.Dispose();
        _listener = null;
        return Task.CompletedTask;
    }
}