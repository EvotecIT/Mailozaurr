using System.Management.Automation;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Watches the SMTP connection pool for size changes and invokes <see cref="Action"/> for each update.
/// </summary>
[Cmdlet("Watch", "SmtpConnectionPool")]
[OutputType(typeof(PSEventSubscriber))]
public sealed class CmdletWatchSmtpConnectionPool : PSCmdlet {
    /// <summary>Script block executed for each snapshot.</summary>
    [Parameter(Mandatory = true)]
    public ScriptBlock Action { get; set; } = null!;

    /// <inheritdoc />
    protected override void ProcessRecord() {
        var subscriber = Events.SubscribeEvent(
            source: typeof(SmtpConnectionPool),
            eventName: nameof(SmtpConnectionPool.PoolSizeChanged),
            sourceIdentifier: null,
            data: null,
            action: Action,
            supportEvent: true);

        Action.Invoke(SmtpConnectionPool.GetSnapshot());
        WriteObject(subscriber);
    }
}
