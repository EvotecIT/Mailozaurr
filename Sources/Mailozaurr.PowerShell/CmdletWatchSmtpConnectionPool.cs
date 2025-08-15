using System;
using System.Management.Automation;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Subscribes to SMTP connection pool updates.</para>
/// <para type="description">The <c>Watch-SmtpConnectionPool</c> cmdlet registers a handler
/// that executes a provided script block whenever the SMTP connection pool
/// changes. The handler is returned so it can be removed when no longer
/// needed.</para>
/// <example>
///   <summary>Watch pool changes</summary>
///   <code>Watch-SmtpConnectionPool -Action { param($s) $s.CurrentPoolSize }</code>
/// </example>
/// </summary>
[Cmdlet("Watch", "SmtpConnectionPool")]
[OutputType(typeof(Action<int>))]
public sealed class CmdletWatchSmtpConnectionPool : PSCmdlet {
    /// <summary>Script block executed for each snapshot.</summary>
    [Parameter(Mandatory = true)]
    public ScriptBlock Action { get; set; } = null!;

    /// <summary>Registers the handler and writes it to the pipeline.</summary>
    protected override void ProcessRecord() {
        Action<int> handler = _ => Action.Invoke(SmtpConnectionPool.GetSnapshot());
        SmtpConnectionPool.PoolSizeChanged += handler;
        Action.Invoke(SmtpConnectionPool.GetSnapshot());
        WriteObject(handler);
    }
}

            eventName: nameof(SmtpConnectionPool.PoolSizeChanged),
            sourceIdentifier: null,
            data: null,
            action: Action,
            supportEvent: true);

        Action.Invoke(SmtpConnectionPool.GetSnapshot());
        WriteObject(subscriber);
    }
}
