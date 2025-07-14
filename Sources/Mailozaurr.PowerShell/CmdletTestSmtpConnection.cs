using System.Management.Automation;
using System.Threading.Tasks;
using MailKit.Security;
using MailKit.Net.Smtp;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Tests SMTP connectivity and reports server capabilities.</para>
/// <para type="description">The <c>Test-SmtpConnection</c> cmdlet connects to an
/// SMTP server and returns information about supported features. It also checks
/// if the connection remains open after a NOOP command which indicates support
/// for persistent connections.</para>
/// <example>
///   <summary>Check capabilities of an SMTP server</summary>
///   <code>Test-SmtpConnection -Server "smtp.example.com" -Port 25</code>
/// </example>
/// </summary>
[Cmdlet(VerbsDiagnostic.Test, "SmtpConnection")]
public sealed class CmdletTestSmtpConnection : AsyncPSCmdlet {
    [Parameter(Mandatory = true)]
    public string? Server { get; set; }

    [Parameter]
    public int Port { get; set; } = 587;

    [Parameter]
    public SwitchParameter UseSsl { get; set; }

    protected override Task ProcessRecordAsync() {
        var info = Smtp.TestConnection(Server!, Port, SecureSocketOptions.Auto, UseSsl.IsPresent);
        WriteObject(info);
        return Task.CompletedTask;
    }
}
