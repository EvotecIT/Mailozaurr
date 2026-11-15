BeforeAll { . (Join-Path $PSScriptRoot 'TestHelpers/Add-MailozaurrTestType.ps1') }

Describe 'Test-SmtpConnection' {
    It 'Returns capabilities object' {
        $refs = @(
            [Mailozaurr.Smtp].Assembly.Location,
            [Mailozaurr.ClientSmtp].Assembly.Location,
            [MailKit.Security.SecureSocketOptions].Assembly.Location
        )
        if (-not ('FakeClientPs3' -as [type])) {
            Add-MailozaurrTestType -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701,1702' -TypeDefinition @"
using Mailozaurr;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using System.Threading;
public class FakeClientPs3 : ClientSmtp {
    public override bool IsConnected { get { return true; } }
    public override SmtpCapabilities GetCapabilitiesSnapshot() { return new SmtpCapabilities(new[] { SmtpCapability.Pipelining }); }
    public override void Connect(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default(CancellationToken)) {}
    public override void Disconnect(bool quit, CancellationToken cancellationToken = default(CancellationToken)) {}
    public override void NoOp(CancellationToken cancellationToken = default(CancellationToken)) {}
}
public static class FakeClientPs3Factory {
    public static ClientSmtp Create(ProtocolLogger logger) { return new FakeClientPs3(); }
    public static void Install() { Smtp.ClientFactory = Create; }
}
"@
        }
        try {
            [FakeClientPs3Factory]::Install()
            $result = Test-SmtpConnection -Server 'h'
            $result | Should -BeOfType 'Mailozaurr.SmtpConnectionInfo'
            $result.Capabilities.Contains([MailKit.Net.Smtp.SmtpCapability]::Pipelining) | Should -BeTrue
        } finally {
            [Mailozaurr.Smtp]::ResetClientFactory()
        }
    }
}
