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
    private InternalLogger? _logger;
    private InternalLoggerPowerShell? _listener;
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
        _logger = new InternalLogger();
        _listener = new InternalLoggerPowerShell(_logger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);
        LoggingMessages.Logger = _logger;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Converts the specified MSG files to EML format.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        var outputMessage = EmailMessage.ConvertMsgToEml(InputPath, OutputFolder, Force);
        foreach (var obj in outputMessage) {
            WriteObject(obj);
        }
        return Task.CompletedTask;
    }

    protected override Task EndProcessingAsync() {
        _listener?.Dispose();
        _listener = null;
        return Task.CompletedTask;
    }
}
