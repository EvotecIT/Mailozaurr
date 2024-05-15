namespace Mailozaurr.PowerShell;

[Cmdlet(VerbsCommunications.Send, "EmailMessageTemporary")]
public sealed class CmdletSendEmailMessage : AsyncPSCmdlet {
    private InternalLogger logger;
    private Smtp SmtpClient;

    protected override Task BeginProcessingAsync() {
        // Initialize the logger to be able to see verbose, warning, debug, error, progress, and information messages.
        logger = new InternalLogger(false);
        var internalLoggerPowerShell = new InternalLoggerPowerShell(logger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);

        SmtpClient = new Smtp();

        return Task.CompletedTask;
    }
    protected override async Task ProcessRecordAsync() {

    }

}
