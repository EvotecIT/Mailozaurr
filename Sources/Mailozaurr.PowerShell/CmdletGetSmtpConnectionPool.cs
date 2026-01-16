using System;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Retrieves information about the SMTP connection pool.</para>
/// <para type="description">The <c>Get-SmtpConnectionPool</c> cmdlet returns a snapshot of
/// pooled SMTP connections or, when used with <c>-Watch</c>, continuously emits
/// updates as the pool changes. An optional <c>-Action</c> script block can be
/// executed for each update.</para>
/// </summary>
[Cmdlet(VerbsCommon.Get, "SmtpConnectionPool")]
[OutputType(typeof(SmtpConnectionPoolSnapshot))]
public sealed class CmdletGetSmtpConnectionPool : AsyncPSCmdlet {
    /// <summary>Continuously watch the pool for changes.</summary>
    [Parameter]
    public SwitchParameter Watch { get; set; }

    /// <summary>Script block executed for each snapshot when watching.</summary>
    [Parameter]
    public ScriptBlock? Action { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        void Emit() {
            var snapshot = SmtpConnectionPool.GetSnapshot();
            if (Action != null) {
                try {
                    Action.Invoke(snapshot);
                } catch (RuntimeException ex) {
                    WriteError(ex.ErrorRecord);
                }
            } else {
                WriteObject(snapshot);
            }
        }

        if (Watch) {
            void Handler(int _) => Emit();
            SmtpConnectionPool.PoolSizeChanged += Handler;
            Emit();
            try {
                await Task.Delay(-1, CancelToken);
            } catch (TaskCanceledException) {
            } finally {
                SmtpConnectionPool.PoolSizeChanged -= Handler;
            }
        } else {
            Emit();
        }
    }
}
