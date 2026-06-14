using System;
using System.Management.Automation;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Removes a watcher returned by <c>Watch-SmtpConnectionPool</c>.
/// </summary>
[Cmdlet(VerbsLifecycle.Unregister, "SmtpConnectionPoolWatcher")]
public sealed class CmdletUnregisterSmtpConnectionPoolWatcher : PSCmdlet {
    /// <summary>Watcher returned by <c>Watch-SmtpConnectionPool</c>.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [ValidateNotNull]
    public Action<int>? Watcher { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        SmtpConnectionPool.PoolSizeChanged -= Watcher;
    }
}
