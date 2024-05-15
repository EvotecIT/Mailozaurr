using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

public class InitializeSmtp : AsyncPSCmdlet {

    public InitializeSmtp() {
        // Initialize the logger
        var internalLogger = new InternalLogger(false);
        // Initialize the PowerShell logger, and subscribe to the verbose message event
        var internalLoggerPowerShell = new InternalLoggerPowerShell(internalLogger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);

        // Initialize the SMTP client
        var smtpClient = new Smtp();
    }

}

