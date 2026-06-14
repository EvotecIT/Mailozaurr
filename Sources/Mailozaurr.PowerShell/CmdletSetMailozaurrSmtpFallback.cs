using MailKit.Security;
using System;
using System.Management.Automation;
using System.Net;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Configures the SMTP sender used when Graph sending falls back to SMTP.
/// </summary>
[Cmdlet(VerbsCommon.Set, "MailozaurrSmtpFallback")]
public sealed class CmdletSetMailozaurrSmtpFallback : PSCmdlet {
    /// <summary>SMTP server used by the fallback sender.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Configure")]
    [ValidateNotNullOrEmpty]
    public string? Server { get; set; }

    /// <summary>SMTP server port.</summary>
    [Parameter(ParameterSetName = "Configure")]
    [ValidateRange(1, 65535)]
    public int Port { get; set; } = 587;

    /// <summary>Credential used to authenticate the fallback sender.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Configure")]
    [ValidateNotNull]
    public PSCredential? Credential { get; set; }

    /// <summary>SSL/TLS mode used for the fallback connection.</summary>
    [Parameter(ParameterSetName = "Configure")]
    public SecureSocketOptions SecureSocketOptions { get; set; } = SecureSocketOptions.Auto;

    /// <summary>Clears the configured fallback sender.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Clear")]
    public SwitchParameter Clear { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        if (Clear.IsPresent) {
            MailozaurrOptions.SmtpFallbackFactory = null;
            return;
        }

        var server = Server!;
        var port = Port;
        var secureSocketOptions = SecureSocketOptions;
        var credential = Credential!.GetNetworkCredential();
        MailozaurrOptions.SmtpFallbackFactory = _ => CreateFallbackSender(server, port, secureSocketOptions, credential);
    }

    private static Smtp CreateFallbackSender(string server, int port, SecureSocketOptions secureSocketOptions, NetworkCredential credential) {
        var smtp = new Smtp();
        var connect = smtp.Connect(server, port, secureSocketOptions);
        if (!connect.Status) {
            throw new InvalidOperationException(connect.Error ?? "SMTP fallback connection failed.");
        }

        var authenticate = smtp.Authenticate(credential);
        if (!authenticate.Status) {
            throw new InvalidOperationException(authenticate.Error ?? "SMTP fallback authentication failed.");
        }

        return smtp;
    }
}
