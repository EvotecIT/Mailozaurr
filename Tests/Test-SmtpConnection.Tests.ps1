Describe 'Test-SmtpConnection' {
    It 'Returns capabilities object' {
        $refs = @(
            [Mailozaurr.ClientSmtp].Assembly.Location,
            [MailKit.Security.SecureSocketOptions].Assembly.Location
        )
        Add-Type -ReferencedAssemblies $refs -TypeDefinition @"
using Mailozaurr;
using MailKit.Security;
using System.Threading;
public class FakeClientPs3 : ClientSmtp {
    public override bool IsConnected => true;
    public override SmtpCapabilities Capabilities => SmtpCapabilities.Pipelining;
    public override void Connect(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) {}
    public override void Disconnect(bool quit, CancellationToken cancellationToken = default) {}
    public override void NoOp(CancellationToken cancellationToken = default) {}
}
"@
        [Mailozaurr.Smtp]::ClientFactory = { [FakeClientPs3]::new() }
        $result = Test-SmtpConnection -Server 'h'
        $result | Should -BeOfType 'Mailozaurr.SmtpConnectionInfo'
        $result.Capabilities | Should -Match 'Pipelining'
        [Mailozaurr.Smtp]::ClientFactory = { [Mailozaurr.ClientSmtp]::new() }
    }
}
