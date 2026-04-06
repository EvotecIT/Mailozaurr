Describe 'Test-SmtpConnection' {
    It 'Returns capabilities object' {
        $refs = @(
            [Mailozaurr.Smtp].Assembly.Location,
            [Mailozaurr.ClientSmtp].Assembly.Location,
            [MailKit.Security.SecureSocketOptions].Assembly.Location
        )
        if (-not ('FakeClientPs3' -as [type])) {
            Add-Type -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701,1702' -TypeDefinition @"
using Mailozaurr;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using System.Threading;
public class FakeClientPs3 : ClientSmtp {
    public override bool IsConnected => true;
    public override SmtpCapabilities GetCapabilitiesSnapshot() => SmtpCapabilities.Pipelining;
    public override void Connect(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) {}
    public override void Disconnect(bool quit, CancellationToken cancellationToken = default) {}
    public override void NoOp(CancellationToken cancellationToken = default) {}
}
public static class FakeClientPs3Factory {
    public static ClientSmtp Create(ProtocolLogger logger) => new FakeClientPs3();
    public static void Install() => Smtp.ClientFactory = Create;
}
"@
        }
        try {
            [FakeClientPs3Factory]::Install()
            $result = Test-SmtpConnection -Server 'h'
            $result | Should -BeOfType 'Mailozaurr.SmtpConnectionInfo'
            $result.Capabilities | Should -Match 'Pipelining'
        } finally {
            [Mailozaurr.Smtp]::ResetClientFactory()
        }
    }
}
