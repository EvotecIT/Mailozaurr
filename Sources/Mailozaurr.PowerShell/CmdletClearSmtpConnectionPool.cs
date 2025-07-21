using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Clears all cached SMTP connections used for connection pooling.</para>
/// <para type="description">The <c>Clear-SmtpConnectionPool</c> cmdlet removes any
/// pooled SMTP connections maintained by the <see cref="Smtp"/> class. Use this
/// when you want to force new connections, for example after changing credentials
/// or server settings.</para>
/// <example>
///   <summary>Reset the SMTP connection pool</summary>
///   <code>Clear-SmtpConnectionPool</code>
/// </example>
/// </summary>
[Cmdlet(VerbsCommon.Clear, "SmtpConnectionPool")]
public sealed class CmdletClearSmtpConnectionPool : AsyncPSCmdlet {
    /// <summary>Clears all connections from the pool.</summary>
    protected override Task ProcessRecordAsync() {
        SmtpConnectionPool.ClearConnectionPool();
        return Task.CompletedTask;
    }
}
