using OfficeIMO.Email;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Converts MSG files to EML format for interoperability with other clients.</para>
/// <para type="description">The <c>ConvertFrom-MsgToEml</c> cmdlet converts one or more MSG files to EML format. Provide input MSG file paths and the destination folder. Existing files can be overwritten with the <c>-Force</c> switch.</para>
/// <para type="description">Unsupported source metadata blocks conversion by default. Use AllowLoss to accept its omission when exporting a portable message.</para>
/// </summary>
/// <example>
///   <summary>Convert multiple MSG files</summary>
///   <code>ConvertFrom-MsgToEml -InputPath "C:\Mail\mail1.msg","C:\Mail\mail2.msg" -OutputFolder "C:\Converted"</code>
/// </example>
/// <example>
///   <summary>Convert an Outlook message while accepting metadata loss</summary>
///   <code>ConvertFrom-MsgToEml -InputPath './invoice.msg' -OutputFolder './portable' -AllowLoss</code>
///   <para>The message content is converted to EML while Outlook metadata that EML cannot represent may be omitted.</para>
/// </example>
/// <remarks>
/// Use this cmdlet to archive or migrate Outlook messages to the portable EML format. Conversion blocks unsupported metadata loss unless AllowLoss is specified.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
[Cmdlet(VerbsData.ConvertFrom, "MsgToEml")]
public sealed class CmdletConvertFromMsgToEml : AsyncPSCmdlet {
    private InternalLogger? _logger;
    private InternalLoggerPowerShell? _listener;
    /// <summary>
    /// <para type="description">Paths to the MSG files to convert.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string[]? InputPath { get; set; }

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

    /// <summary>Allows unsupported source metadata to be omitted during conversion.</summary>
    [Parameter]
    public SwitchParameter AllowLoss { get; set; }

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
        if (InputPath is null || OutputFolder is null) {
            return Task.CompletedTask;
        }

        var options = new EmailWriterOptions(AllowLoss.IsPresent ? EmailConversionLossPolicy.Warn : EmailConversionLossPolicy.Block);
        var outputMessage = EmailMessage.ConvertMsgToEml(InputPath, OutputFolder, Force, options);
        foreach (var obj in outputMessage) {
            WriteObject(obj);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up logging resources.
    /// </summary>
    protected override Task EndProcessingAsync() {
        _listener?.Dispose();
        _listener = null;
        return Task.CompletedTask;
    }
}
