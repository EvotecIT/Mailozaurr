using MailKit.Net.Smtp;
using MailKit.Security;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Tests SMTP connectivity and reports server capabilities.</para>
/// <para type="description">The <c>Test-SmtpConnection</c> cmdlet connects to an
/// SMTP server and returns information about supported features. It also checks
/// if the connection remains open after a NOOP command which indicates support
/// for persistent connections. It can also perform an envelope-only recipient
/// probe or send an explicit validation message for authorized mail-flow testing.</para>
/// <example>
///   <summary>Check capabilities of an SMTP server</summary>
///   <code>Test-SmtpConnection -Server "smtp.example.com" -Port 25</code>
/// </example>
/// </summary>
[Cmdlet(VerbsDiagnostic.Test, "SmtpConnection", DefaultParameterSetName = "Connection", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public sealed class CmdletTestSmtpConnection : AsyncPSCmdlet {
    /// <summary>
    /// SMTP server hostname to test.
    /// </summary>
    [Parameter(ParameterSetName = "Connection")]
    [Parameter(ParameterSetName = "RecipientProbe")]
    [Parameter(ParameterSetName = "ValidationMessage")]
    public string? Server { get; set; }

    /// <summary>
    /// External email domain used to infer the direct Exchange Online Protection target.
    /// </summary>
    [Parameter(ParameterSetName = "Connection")]
    [Parameter(ParameterSetName = "RecipientProbe")]
    [Parameter(ParameterSetName = "ValidationMessage")]
    public string? Domain { get; set; }

    /// <summary>
    /// Infers the direct Exchange Online Protection target from the domain instead of requiring -Server.
    /// </summary>
    [Parameter(ParameterSetName = "Connection")]
    [Parameter(ParameterSetName = "RecipientProbe")]
    [Parameter(ParameterSetName = "ValidationMessage")]
    public SwitchParameter ExchangeOnlineDirect { get; set; }

    /// <summary>
    /// TCP port used for the SMTP connection.
    /// </summary>
    [Parameter(ParameterSetName = "Connection")]
    [Parameter(ParameterSetName = "RecipientProbe")]
    [Parameter(ParameterSetName = "ValidationMessage")]
    public int Port { get; set; } = 587;

    /// <summary>
    /// Indicates whether to test SSL connectivity.
    /// </summary>
    [Parameter(ParameterSetName = "Connection")]
    [Parameter(ParameterSetName = "RecipientProbe")]
    [Parameter(ParameterSetName = "ValidationMessage")]
    public SwitchParameter UseSsl { get; set; }

    /// <summary>
    /// Optional recipient address to validate with RCPT TO without sending message DATA.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "RecipientProbe")]
    [Parameter(Mandatory = true, ParameterSetName = "ValidationMessage")]
    public string? Recipient { get; set; }

    /// <summary>
    /// Envelope sender address used with MAIL FROM during recipient probing.
    /// </summary>
    [Parameter(ParameterSetName = "RecipientProbe")]
    [Parameter(ParameterSetName = "ValidationMessage")]
    public string Sender { get; set; } = "probe@example.com";

    /// <summary>
    /// EHLO/HELO hostname used during recipient probing.
    /// </summary>
    [Parameter(ParameterSetName = "RecipientProbe")]
    [Parameter(ParameterSetName = "ValidationMessage")]
    public string HeloHost { get; set; } = "localhost";

    /// <summary>
    /// Sends a neutral validation message after the connection and recipient probe.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "ValidationMessage")]
    public SwitchParameter SendValidationMessage { get; set; }

    /// <summary>
    /// Explicit acknowledgement that the validation mode sends an email message.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "ValidationMessage")]
    public SwitchParameter IUnderstandThisSendsEmail { get; set; }

    /// <summary>
    /// Optional validation test identifier used in the subject, body, and custom header.
    /// </summary>
    [Parameter(ParameterSetName = "ValidationMessage")]
    public string? TestId { get; set; }

    /// <summary>
    /// Optional subject used for the validation message.
    /// </summary>
    [Parameter(ParameterSetName = "ValidationMessage")]
    public string? ValidationSubject { get; set; }

    /// <summary>
    /// Optional plain-text body used for the validation message.
    /// </summary>
    [Parameter(ParameterSetName = "ValidationMessage")]
    public string? ValidationBody { get; set; }

    /// <summary>
    /// Marks the validation message as high priority.
    /// </summary>
    [Parameter(ParameterSetName = "ValidationMessage")]
    public SwitchParameter HighPriority { get; set; }

    /// <summary>
    /// Tests the SMTP server connection and outputs capability information.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override Task ProcessRecordAsync() {
        var effectiveServer = ResolveServer();
        var effectivePort = ExchangeOnlineDirect.IsPresent && !MyInvocation.BoundParameters.ContainsKey(nameof(Port))
            ? 25
            : Port;

        SmtpValidationMessageRequest? validationMessage = null;
        if (SendValidationMessage.IsPresent) {
            if (!IUnderstandThisSendsEmail.IsPresent) {
                ThrowTerminatingError(new ErrorRecord(
                    new InvalidOperationException("Validation message mode sends a real email. Supply -IUnderstandThisSendsEmail to continue."),
                    "ValidationSendConfirmationRequired",
                    ErrorCategory.InvalidOperation,
                    Recipient));
            }

            if (!ShouldProcess(Recipient!, "Sending SMTP validation message")) {
                return Task.CompletedTask;
            }

            validationMessage = new SmtpValidationMessageRequest {
                Sender = Sender,
                Recipient = Recipient!,
                HeloHost = HeloHost,
                TestId = TestId,
                Subject = ValidationSubject,
                Body = ValidationBody,
                HighPriority = HighPriority.IsPresent
            };
        }

        var info = Smtp.TestConnection(effectiveServer, effectivePort, SecureSocketOptions.Auto, UseSsl.IsPresent, Recipient, Sender, HeloHost, validationMessage);
        WriteObject(info);
        return Task.CompletedTask;
    }

    private string ResolveServer() {
        if (!ExchangeOnlineDirect.IsPresent) {
            if (string.IsNullOrWhiteSpace(Server)) {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException("Server is required unless -ExchangeOnlineDirect is used."),
                    "SmtpServerRequired",
                    ErrorCategory.InvalidArgument,
                    Server));
            }

            return Server!.Trim();
        }

        var domain = Domain;
        if (string.IsNullOrWhiteSpace(domain) && !string.IsNullOrWhiteSpace(Recipient)) {
            var atIndex = Recipient!.LastIndexOf('@');
            if (atIndex >= 0 && atIndex < Recipient.Length - 1) {
                domain = Recipient.Substring(atIndex + 1);
            }
        }

        if (string.IsNullOrWhiteSpace(domain)) {
            ThrowTerminatingError(new ErrorRecord(
                new ArgumentException("Domain is required for -ExchangeOnlineDirect when it cannot be inferred from -Recipient."),
                "ExchangeOnlineDirectDomainRequired",
                ErrorCategory.InvalidArgument,
                Domain));
        }

        return Smtp.GetExchangeOnlineProtectionHost(domain!);
    }
}
