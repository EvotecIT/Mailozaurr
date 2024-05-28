namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Checks if an email address is valid.</para>
/// <para type="description">This cmdlet checks if an email address is valid. It takes an array of email addresses as input and checks each one. The cmdlet can also check if an email address is a valid international email address or if it has a top level domain.</para>
/// <example>
/// <para>Check if an email address is valid</para>
/// <code>Test-EmailAddress -EmailAddress "test@example.com"</code>
/// </example>
/// <example>
/// <para>Check if an email address is valid using pipeline input</para>
/// <code>"test@example.com" | Test-EmailAddress</code>
/// </example>
/// <example>
/// <para>Check if an email address is a valid international email address</para>
/// <code>Test-EmailAddress -EmailAddress "test@example.com" -AllowInternational</code>
/// </example>
/// <example>
/// <para>Check if an email address is valid with a top level domain</para>
/// <code>Test-EmailAddress -EmailAddress "test@example" -AllowTopLevelDomains</code>
/// </example>
/// </summary>
[Cmdlet(VerbsDiagnostic.Test, "EmailAddress")]
public sealed class CmdletTestEmailAddress : AsyncPSCmdlet {

    /// <summary>
    /// <para type="description">Specifies the email addresses to check. This parameter accepts an array of strings and is mandatory.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string[] EmailAddress;

    /// <summary>
    /// <para type="description">If this parameter is set, the cmdlet will use the newer international email standards to validate the email addresses.</para>
    /// </summary>
    [Parameter(Mandatory = false, Position = 1)]
    public SwitchParameter AllowInternational { get; set; }

    /// <summary>
    /// <para type="description">If this parameter is set, the cmdlet will allow top level domains in the email addresses (such as test@email).</para>
    /// </summary>
    [Parameter(Mandatory = false, Position = 2)]
    public SwitchParameter AllowTopLevelDomains { get; set; }

    private InternalLogger _logger;

    /// <summary>
    /// <para type="description">This method is called once for each cmdlet in the pipeline when the pipeline starts executing.</para>
    /// <para type="description">It initializes the logger to be able to see verbose, warning, debug, error, progress, and information messages.</para>
    /// </summary>
    protected override Task BeginProcessingAsync() {
        // Initialize the logger to be able to see verbose, warning, debug, error, progress, and information messages.
        _logger = new InternalLogger(false);
        var internalLoggerPowerShell = new InternalLoggerPowerShell(_logger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);
        return Task.CompletedTask;
    }

    /// <summary>
    /// <para type="description">This method is called once for each input record. It processes each email address and checks if it is valid.</para>
    /// <para type="description">The results are written to the output pipeline.</para>
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        foreach (var email in EmailAddress) {
            _logger.WriteVerbose("Processing email: {0}", email);
            WriteObject(Validator.ValidateEmail(email, AllowInternational, AllowTopLevelDomains));
        }
    }
}
