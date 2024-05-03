namespace Mailozaurr.PowerShell;

/// <summary>
/// Checks if email address matches conditions to be valid email address
/// <example>
/// <code>
/// Test-EmailAddress -EmailAddress "test@example.com"
/// </code>
/// Checks if "test@example.com" is a valid email address.
/// </example>
/// <example>
/// <code>
/// "test@example.com" | Test-EmailAddress
/// </code>
/// Checks if "test@example.com" is a valid email address using pipeline input.
/// </example>
/// <example>
/// <code>
/// Test-EmailAddress -EmailAddress "test@example.com" -AllowInternational
/// </code>
/// Checks if "test@example.com" is a valid international email address.
/// </example>
/// <example>
/// <code>
/// Test-EmailAddress -EmailAddress "test@example" -AllowTopLevelDomains
/// </code>
/// Checks if "test@example" is a valid email address with a top level domain.
/// </example>
/// </summary>
[Cmdlet(VerbsDiagnostic.Test, "EmailAddress")]
public sealed class CmdletTestEmailAddress : AsyncPSCmdlet {
    /// <summary>
    /// The email address to check if it is valid
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string[] EmailAddress;

    /// <summary>
    /// If parameter is set then the validator will use the newer International Email standards for validating the email address
    /// </summary>
    [Parameter(Mandatory = false, Position = 1)]
    public SwitchParameter AllowInternational { get; set; }

    /// <summary>
    /// If parameter is set then the validator will allow top level domains in the email address (such as test@email)
    /// </summary>
    [Parameter(Mandatory = false, Position = 2)]
    public SwitchParameter AllowTopLevelDomains { get; set; }

    private InternalLogger _logger;

    protected override Task BeginProcessingAsync() {
        // Initialize the logger to be able to see verbose, warning, debug, error, progress, and information messages.
        _logger = new InternalLogger(false);
        var internalLoggerPowerShell = new InternalLoggerPowerShell(_logger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);
        return Task.CompletedTask;
    }
    protected override async Task ProcessRecordAsync() {
        foreach (var email in EmailAddress) {
            _logger.WriteVerbose("Processing email: {0}", email);
            WriteObject(Validator.ValidateEmail(email, AllowInternational, AllowTopLevelDomains));
        }
    }
}
