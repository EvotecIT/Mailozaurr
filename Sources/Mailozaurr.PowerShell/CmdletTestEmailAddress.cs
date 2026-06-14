namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Validates one or more email addresses for format and standards compliance.</para>
/// <para type="description">The <c>Test-EmailAddress</c> cmdlet checks if one or more email addresses are valid according to standard email address rules. Supports validation for international addresses and top-level domains. Returns validation results for each address.</para>
/// <example>
///   <summary>Check if an email address is valid</summary>
///   <code>Test-EmailAddress -EmailAddress "test@example.com"</code>
/// </example>
/// <example>
///   <summary>Check if an email address is valid using pipeline input</summary>
///   <code>"test@example.com" | Test-EmailAddress</code>
/// </example>
/// <example>
///   <summary>Check if an email address is a valid international email address</summary>
///   <code>Test-EmailAddress -EmailAddress "test@exámple.com" -AllowInternational</code>
/// </example>
/// <example>
///   <summary>Check if an email address is valid with a top level domain</summary>
///   <code>Test-EmailAddress -EmailAddress "test@email" -AllowTopLevelDomains</code>
/// </example>
/// <remarks>
/// Use this cmdlet to validate email addresses before sending, importing, or processing them in automation scenarios.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsDiagnostic.Test, "EmailAddress")]
public sealed class CmdletTestEmailAddress : AsyncPSCmdlet {

    /// <summary>
    /// <para type="description">Specifies the email addresses to check. Accepts an array of strings. This parameter is mandatory.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string[]? EmailAddress { get; set; }

    /// <summary>
    /// <para type="description">If set, the cmdlet will use the newer international email standards to validate the email addresses.</para>
    /// </summary>
    [Parameter(Mandatory = false, Position = 1)]
    public SwitchParameter AllowInternational { get; set; }

    /// <summary>
    /// <para type="description">If set, the cmdlet will allow top level domains in the email addresses (such as test@email).</para>
    /// </summary>
    [Parameter(Mandatory = false, Position = 2)]
    public SwitchParameter AllowTopLevelDomains { get; set; }

    private InternalLogger _logger = null!;
    private InternalLoggerPowerShell? _listener;

    /// <summary>
    /// Initializes the logger for verbose, warning, debug, error, progress, and information messages.
    /// </summary>
    protected override Task BeginProcessingAsync() {
        // Initialize the logger to be able to see verbose, warning, debug, error, progress, and information messages.
        _logger = new InternalLogger(false);
        _listener = new InternalLoggerPowerShell(_logger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes each email address and checks if it is valid. Writes results to the output pipeline.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        if (EmailAddress is null) {
            return Task.CompletedTask;
        }
        foreach (var email in EmailAddress) {
            if (string.IsNullOrWhiteSpace(email)) {
                continue;
            }
            _logger.WriteVerbose("Processing email: {0}", email);
            WriteObject(Validator.ValidateEmail(email, AllowInternational, AllowTopLevelDomains));
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up resources used by the cmdlet.
    /// </summary>
    protected override Task EndProcessingAsync() {
        _listener?.Dispose();
        _listener = null;
        return Task.CompletedTask;
    }
}